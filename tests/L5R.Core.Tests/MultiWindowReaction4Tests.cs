using L5R.Core;
using static RegressionAssertions;

// Issue #830, Reaction slice 4: each multi-window card's Reaction is opened through the real engine path that opens its window (a Dynasty purchase, an equip, a
// targeting action, a battle ...), must then be on offer and ACCEPTED for its own player with the printed effect, must NOT be on offer to the other player, and must
// NOT be on offer outside the window (an ordinary Action phase / Battle phase). The card's other Battle/Open/Limited actions are spot-checked after the Reaction.
internal static partial class MultiWindowReaction4Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Options(e, player, ability, cardId);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] sel) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, sel);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance c, params string[] keywords) { foreach (var k in keywords) c.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return c; }
    private static void Gold(MatchState s, int player, int n) { for (var i = 0; i < n; i++) Add(s, "training-marketplace", player, Zone.Home); }
    private static CardInstance Now(MatchEngine e, CardInstance c) => e.State.Cards[c.Id];
    private static string? Window(MatchEngine e) => e.State.PendingChoice?.Type;
    private static GameCommand Paid(MatchEngine e, int player, int cost, GameCommand command) => command with { SourceIds = e.PaymentOptions(player, cost).First().SourceIds };

    // Player 0 recruits `def` from province 0 in the Dynasty phase (the real entering-play path); returns the engine with the entering-play window open.
    private static (MatchEngine E, string Id) Enter(string def, int cost, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var id = s.Players[0].Provinces[0].CardId!;
        s.Cards[id].DefinitionId = def; s.Cards[id].FaceUp = true;
        Gold(s, 0, 12); s.Players[0].Honor = 20;
        setup?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("purchase", 0, e.Version, CardId: id, SourceIds: e.PaymentOptions(0, cost).First().SourceIds));
        Eq(Window(e), "entryReaction", "the entering-play window is open");
        return (e, id);
    }

    // The ability is nowhere on offer to its owner, nor accepted, in an ordinary Action phase or Battle phase with no window open (either player active).
    private delegate CardInstance Place(MatchState s, bool battle);
    private static void Outside(string def, string ability, Place? place = null, string? target = null)
    {
        foreach (var phase in new[] { Phase.Action, Phase.Battle })
            foreach (var actor in new[] { 0, 1 })
            {
                var s = Table(phase, actor);
                var battle = phase == Phase.Battle;
                var card = place is null ? Add(s, def, 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null) : place(s, battle);
                var e = MatchEngine.FromTestState(s);
                foreach (var player in new[] { 0, 1 })
                {
                    Check(!Options(e, player, ability, card.Id).Any(o => o.Enabled), $"{ability} is not on offer to player {player} in {phase} (actor {actor}) with no window open");
                    Reject(e, Use(e, player, ability, card.Id, target));
                }
            }
    }

    // ---- entering-play Reactions of Personalities, through a Dynasty purchase ----------------------------------------------------------------------

    private const string Tamasine = "oracle-l5r-4397", Hiroto = "oracle-l5r-10057", Izuna = "oracle-l5r-546", BrokenMan = "oracle-l5r-10866", Tsujiken = "oracle-l5r-9469";
    private const string CrabSword = "oracle-l5r-9876", PhoenixSword = "oracle-l5r-9878";

    private static void TamasineCreate()
    {
        var (e, id) = Enter(Tamasine, 8);
        Check(!CanUse(e, 1, "tamasine-create", id), "not offered to the other player");
        Reject(e, Use(e, 1, "tamasine-create", id));
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, Enabled(e, 0, "tamasine-create", id).Command!);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var def = CardCatalog.Get(made.DefinitionId);
        Check(def.Force == 2 && def.Chi == 2 && def.PersonalHonor == 3 && def.Keywords!.Contains("Ancestor") && made.OwnerId == 0 && made.Zone == Zone.Home, "the 2F/2C/3PH Lion Ancestor is created in her controller's home");
        Check(!CanUse(e, 0, "tamasine-create", id), "spent: not offered again");
        Outside(Tamasine, "tamasine-create");
        // Her Battle action still works afterwards: bow her, +4F to an Ancestor.
        var s = Table(Phase.Battle, 0);
        var tam = Add(s, Tamasine, 0, Zone.Battlefield, province: 0);
        var anc = Add(s, made.DefinitionId, 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        var force = b.CardForceForTest(anc.Id);
        Accept(b, Use(b, 0, "tamasine-boost-one", tam.Id, null, null, Pick("targets", anc.Id)));
        Eq(b.CardForceForTest(anc.Id), force + 4, "the Battle action gave +4F");
        Check(Now(b, tam).Bowed, "Tamasine bowed as its cost");
    }

    private static void HirotoTokens()
    {
        var (e, id) = Enter(Hiroto, 8);
        Check(!CanUse(e, 1, "hiroto-fire-tokens", id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "hiroto-fire-tokens", id).Command!);
        Eq(e.State.Cards[id].Tokens.Count(t => t.Type == "Fire"), 4, "four Fire tokens");
        Check(!CanUse(e, 0, "hiroto-fire-tokens", id), "spent: not offered again");
        Outside(Hiroto, "hiroto-fire-tokens");
        var s = Table(Phase.Battle, 0);
        var hiroto = Add(s, Hiroto, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 0, "hiroto-bow", hiroto.Id, enemy.Id));
        Check(Now(b, enemy).Bowed, "Hiroto's Battle action still bows an enemy card without attachments");
    }

    private static void SkinDonorHonorLoss()
    {
        var (e, id) = Enter("training-skin-donor", 3);
        var honor = Honor(e, 0);
        Check(!CanUse(e, 1, "tsudoken-exp2-entry", id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "tsudoken-exp2-entry", id).Command!);
        Eq(Honor(e, 0), honor - 3, "lost 3 Honor");
        Check(!CanUse(e, 0, "tsudoken-exp2-entry", id), "spent: not offered again");
        Outside("training-skin-donor", "tsudoken-exp2-entry");
        // His Open actions are independent of the Reaction: bow a Personality in an Action phase, either player's turn.
        foreach (var player in new[] { 0, 1 })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = player;
            var donor = Add(s, "training-skin-donor", player, Zone.Home);
            var victim = Add(s, "training-crane-samurai", 1 - player, Zone.Home);
            var a = MatchEngine.FromTestState(s);
            Accept(a, Offer(a, player, "training-bow-any", donor.Id, victim.Id));
            Check(Now(a, victim).Bowed, $"the Open action bowed the target for player {player}");
        }
    }

    private static void IzunaKolat()
    {
        var (e, id) = Enter(Izuna, 7, s =>
        {
            Add(s, "training-lion-samurai", 1, Zone.Home).Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-lion-samurai").PersonalHonor));
            Add(s, "training-lion-veteran", 1, Zone.Home).Modifiers.Add(new("test-source", "test", "PersonalHonor", 10));
            Add(s, "training-crane-samurai", 0, Zone.Home);
        });
        var victim = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.DefinitionId == "training-lion-samurai" && c.Zone == Zone.Home && c.Modifiers.Count > 0);
        var proud = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.DefinitionId == "training-lion-veteran" && c.Modifiers.Count > 0);
        Check(Options(e, 1, "izuna-kolat", id).All(o => !o.Enabled), "not offered to the other player");
        var o = Options(e, 0, "izuna-kolat", id).First(x => x.Enabled);
        Check(o.TargetIds!.Contains(victim.Id) && !o.TargetIds.Contains(proud.Id), "only another player's Personality of Personal Honor at most her Chi");
        Reject(e, Use(e, 0, "izuna-kolat", id, proud.Id));
        Accept(e, Offer(e, 0, "izuna-kolat", id, victim.Id));
        Check(Now(e, victim).Dishonorable && e.HasKeyword(victim.Id, "Kolat"), "dishonored and permanently Kolat");
        Check(!CanUse(e, 0, "izuna-ninja", id) && !CanUse(e, 0, "izuna-kolat", id), "the three keyword choices share one use");
        Outside(Izuna, "izuna-kolat", target: null);
    }

    private static void BrokenManInvest()
    {
        var (e, id) = Enter(BrokenMan, 5);
        var force = e.CardForceForTest(id);
        Check(!CanUse(e, 1, "wlpa-brokenman-invest", id), "not offered to the other player");
        Accept(e, Paid(e, 0, 5, Use(e, 0, "wlpa-brokenman-invest", id)));
        Eq(e.CardForceForTest(id), force + 2, "two +1F tokens");
        Outside(BrokenMan, "wlpa-brokenman-invest");
        var s = Table(Phase.Battle, 0);
        var man = Add(s, BrokenMan, 0, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 0, "wlpa-brokenman-bow", man.Id, weak.Id));
        Check(Now(b, weak).Bowed, "his Earth Battle action still bows an enemy card of lower Force");
    }

    private static void TsujikenWatch()
    {
        CardInstance farm = null!;
        var (e, id) = Enter(Tsujiken, 10, s => farm = Add(s, "training-farm", 0, Zone.Home));
        Check(!CanUse(e, 1, "tsujiken-watch-holding", id), "not offered to the other player");
        Enabled(e, 0, "tsujiken-watch-holding", id);
        Accept(e, Use(e, 0, "tsujiken-watch-holding", id, farm.Id));
        Check(e.State.TsujikenWatches.Any(w => w.TsujikenId == id && w.HoldingId == farm.Id), "the Holding is watched");
        Outside(Tsujiken, "tsujiken-watch-holding");
        var s = Table(Phase.Action, 0);
        var t = Add(s, Tsujiken, 0, Zone.Home);
        var holding = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var a = MatchEngine.FromTestState(s);
        Accept(a, Offer(a, 0, "tsujiken-straighten-holding", t.Id, holding.Id));
        Check(!Now(a, holding).Bowed && !Now(a, t).Bowed, "his Open action still straightens a Holding (his bow is waived for a Holding of base cost 4 or lower)");
    }

    // ---- Celestial Swords: entering play by equipping in the Action phase ---------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Sword, CardInstance Wielder) Equipped(string sword, string clan)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var wielder = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), clan);
        var card = Add(s, sword, 0, Zone.Hand);
        Gold(s, 0, 8);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == card.Id);
        Accept(e, equip.Command! with { TargetId = wielder.Id });
        Eq(Window(e), "entryReaction", "the sword's entering-play window is open");
        return (e, card, wielder);
    }

    private static void SwordHonor(string sword, string clan, string ability)
    {
        var (e, card, _) = Equipped(sword, clan);
        var honor = Honor(e, 0);
        Check(!CanUse(e, 1, ability, card.Id), "not offered to the other player");
        Accept(e, Enabled(e, 0, ability, card.Id).Command!);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor");
        Check(!CanUse(e, 0, ability, card.Id), "spent: not offered again");
        Outside(sword, ability, (s, battle) =>
        {
            var host = Add(s, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(s, sword, 0, Zone.Attached, province: battle ? 0 : null, parent: host.Id);
        });
    }

    private static void CrabSwordHonor()
    {
        SwordHonor(CrabSword, "Crab Clan", "celestial-sword-crab-honor");
        // Its BattleOrOpen straighten works in both phases, even bowed, independent of the Reaction.
        foreach (var phase in new[] { Phase.Battle, Phase.Action })
        {
            var s = Table(phase, 0); s.PriorityPlayerId = 0;
            var battle = phase == Phase.Battle;
            var host = Kw(Add(s, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, bowed: true, province: battle ? 0 : null), "Crab Clan");
            var sword = Add(s, CrabSword, 0, Zone.Attached, bowed: true, province: battle ? 0 : null, parent: host.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, "celestial-sword-crab-straighten", sword.Id).Command!);
            Check(!Now(e, host).Bowed && !Now(e, sword).Bowed, $"the unit straightened in {phase}");
        }
    }

    private static void PhoenixSwordHonor()
    {
        SwordHonor(PhoenixSword, "Phoenix Clan", "celestial-sword-phoenix-honor");
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var wielder = Kw(Add(b, "training-shugenja", 0, Zone.Battlefield, province: 0), "Phoenix Clan");
        var sword = Add(b, PhoenixSword, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var dead = Add(b, "training-shugenja", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(b);
        Accept(e, Offer(e, 0, "celestial-sword-phoenix-return", sword.Id, dead.Id));
        Check(Now(e, dead) is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "the Battle half still brings an honorably dead Phoenix Personality in at the battlefield");
    }

    // ---- Reactions to a card being targeted (the before-action window): the Attacker (player 1) acts, the Defender (player 0) reacts -------------------------

    private static void Close(MatchEngine e)
    {
        for (var guard = 0; Window(e) is "beforeActionReaction" or "attackReaction" && guard < 12; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(Window(e) is null, "the window closed: " + Window(e));
    }
    // Declines the held-action / Attack windows only (a later after-action window stays open for the test to read).
    private static void DeclineHeld(MatchEngine e)
    {
        for (var guard = 0; Window(e) is "beforeActionReaction" or "attackReaction" && guard < 12; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { } pending && guard < 12; guard++)
        {
            var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction")
                ?? e.QueryActions(pending.PlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction");
            if (pass is null) return;
            Accept(e, pass.Command!);
        }
    }
    // `by` acts (Offer of `ability` on `target`); the held action opens the targeted-by-action window for the other player.
    private static MatchEngine Struck(MatchState s, int by, CardInstance actor, string ability, CardInstance target)
    {
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, by, ability, actor.Id, target.Id));
        Eq(Window(e), "beforeActionReaction", "the targeting action is held in a Reaction window");
        return e;
    }
    private static GameCommand Reaction(MatchEngine e, int player, string ability, string cardId, string? performer = null) =>
        Options(e, player, ability, cardId).First(o => o.Enabled && o.Command is not null && (performer is null || o.Command.PerformerId == performer)).Command!;
    private const string Destroyer = "training-no-attachments-destroyer", Bower = "training-battle-bower", DestroyAbility = "training-destroy-no-attachments", BowAbility = "training-bow-at-battle";
    private static MatchState Defending(int actor = 1) { var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor; return s; }

    private const string Sakonoko = "oracle-l5r-3891", Guests = "oracle-l5r-5928", Riku = "oracle-l5r-10323", Tamahime = "oracle-l5r-10194", Aroru = "oracle-l5r-7089", SocialGrace = "oracle-l5r-9928", SubtleGames = "oracle-l5r-9932";
    private const string TamingBeast = "oracle-l5r-7767", Loyalty = "oracle-l5r-8207", Unsafe = "oracle-l5r-9008", Incapacitated = "oracle-l5r-3730", HundredFold = "oracle-l5r-3513", Conviction = "oracle-l5r-6247";
    private const string Ikke = "oracle-l5r-9809", Nobu = "oracle-l5r-9846", Kakihara = "oracle-l5r-223";

    private static void SakonokoSpell()
    {
        var s = Defending();
        var sak = Add(s, Sakonoko, 0, Zone.Battlefield, province: 0);
        var spell = Add(s, "training-spell", 0, Zone.Attached, province: 0, parent: sak.Id);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, enemy, DestroyAbility, ally);
        Check(!CanUse(e, 1, "sakonoko-negate-spell", sak.Id), "not offered to the acting player");
        Reject(e, Use(e, 1, "sakonoko-negate-spell", sak.Id));
        Accept(e, Reaction(e, 0, "sakonoko-negate-spell", sak.Id));
        Check(Now(e, spell).Bowed && !Now(e, sak).Bowed, "one of his Spells bowed as the cost");
        Close(e);
        Eq(Now(e, ally).Zone, Zone.Battlefield, "the other Personality's destruction was negated");
        // Not for an action that targets Sakonoko himself ("one of your OTHER Personalities").
        var t = Defending();
        var sak2 = Add(t, Sakonoko, 0, Zone.Battlefield, province: 0); sak2.Dishonorable = true; Add(t, "training-spell", 0, Zone.Attached, province: 0, parent: sak2.Id);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var conviction = Add(t, Conviction, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Use(e2, 1, "relentless-conviction-destroy", conviction.Id, sak2.Id));
        Check(!CanUse(e2, 0, "sakonoko-negate-spell", sak2.Id), "not for an action targeting Sakonoko himself");
        Outside(Sakonoko, "sakonoko-negate-spell", (st, battle) =>
        {
            var c = Add(st, Sakonoko, 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            Add(st, "training-spell", 0, Zone.Attached, province: battle ? 0 : null, parent: c.Id); return c;
        });
    }

    private static void PerplexingGuests()
    {
        var s = Defending();
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: mine.Id);
        var guests = Add(s, Guests, 0, Zone.Hand);
        var bower = Add(s, Bower, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, bower, BowAbility, mine);
        Check(!CanUse(e, 1, "p5-perplexing-guests-discard", guests.Id), "not offered to the acting player");
        Reject(e, Use(e, 1, "p5-perplexing-guests-discard", guests.Id));
        Accept(e, Enabled(e, 0, "p5-perplexing-guests-discard", guests.Id).Command!);
        Settle(e);
        Eq(Now(e, mine).Zone, Zone.DynastyDiscard, "the targeted Personality was discarded");
        Eq(Now(e, follower).Zone, Zone.FateDiscard, "with his unit");
        Outside(Guests, "p5-perplexing-guests-discard", (st, _) => Add(st, Guests, 0, Zone.Hand));
        // Its Battle action still works independently: a Guest is created at the battlefield.
        var b = Table(Phase.Battle, 0);
        var card = Add(b, Guests, 0, Zone.Hand); Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Enabled(be, 0, "p5-perplexing-guests-create", card.Id).Command!);
        Settle(be);
        Check(be.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.DefinitionId == "oracle-l5r-9998"), "the Guest was created at the battlefield");
    }

    private static void RikuRedirect()
    {
        var s = Defending();
        var riku = Add(s, Riku, 0, Zone.Battlefield, province: 0);
        var tamahime = Add(s, Tamahime, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, Bower, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, bower, BowAbility, tamahime);
        Check(!CanUse(e, 1, "wlpt-riku-redirect", riku.Id), "not offered to the acting player");
        Reject(e, Use(e, 1, "wlpt-riku-redirect", riku.Id));
        Accept(e, Enabled(e, 0, "wlpt-riku-redirect", riku.Id).Command!);
        Settle(e);
        Check(Now(e, riku).Bowed && !Now(e, tamahime).Bowed, "the action targets Riku instead of Tamahime");
        Outside(Riku, "wlpt-riku-redirect");
        // His Iaijutsu Battle duel is independent of the Reaction.
        var d = Table(Phase.Battle, 0);
        var r2 = Add(d, Riku, 0, Zone.Battlefield, province: 0); var enemy = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d);
        Accept(de, Offer(de, 0, "wlpt-riku-duel", r2.Id, enemy.Id));
        Check(de.State.PendingDuel is not null || de.State.LastDuel is not null, "the duel began");
    }

    private static void AroruRedirect()
    {
        var s = Defending();
        var aroru = Add(s, Aroru, 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, Bower, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, bower, BowAbility, aroru);
        Check(Options(e, 1, "aroru-exp2-redirect", aroru.Id).All(o => !o.Enabled), "not offered to the acting player");
        Accept(e, Options(e, 0, "aroru-exp2-redirect", aroru.Id).Single(o => o.Enabled && o.Command?.TargetId == other.Id).Command!);
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null and not { Type: "p3AroruMove" }; guard++) Settle1(e);
        Check(Now(e, other).Bowed && !Now(e, aroru).Bowed, "the action bowed the other card instead");
        Eq(Window(e), "p3AroruMove", "after the action resolves he may move");
        Outside(Aroru, "aroru-exp2-redirect");
        // His Ranged 5 Battle attack still works independently.
        var b = Table(Phase.Battle, 0);
        var ar = Add(b, Aroru, 0, Zone.Battlefield, province: 0); var target = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "aroru-exp2-attack", ar.Id, target.Id));
    }
    private static void Settle1(MatchEngine e)
    {
        var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction");
        Check(pass is not null, "nothing to pass with in " + Window(e)); Accept(e, pass!.Command!);
    }

    private static void SocialGraceNegate()
    {
        var s = Table(Phase.Action, 1);
        var subtle = Add(s, SubtleGames, 1, Zone.Hand);
        var lion = Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
        var crane = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var grace = Add(s, SocialGrace, 0, Zone.Hand);
        var d = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, d.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "subtle-games-dishonor", subtle.Id, crane.Id, lion.Id));
        Eq(Window(e), "beforeActionReaction");
        Check(!CanUse(e, 1, "social-grace-negate", grace.Id), "not offered to the acting player");
        Accept(e, Enabled(e, 0, "social-grace-negate", grace.Id).Command!);
        Close(e);
        Check(!Now(e, crane).Dishonorable, "his dishonoring was negated");
        Check(Now(e, lion).Bowed, "the cost of the action is still paid");
        Outside(SocialGrace, "social-grace-negate", (st, _) => Add(st, SocialGrace, 0, Zone.Hand));
        // Its Open action is independent of the Reaction.
        var o = Table(Phase.Action, 1); o.PriorityPlayerId = 0;
        var grace2 = Add(o, SocialGrace, 0, Zone.Hand);
        var mine = Kw(Add(o, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var attacker = Add(o, "training-lion-veteran", 1, Zone.Home);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 0, "social-grace-penalty", grace2.Id, attacker.Id, mine.Id));
        Check(oe.State.Cards[attacker.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount < 0), "the Open action gave a Force penalty");
    }

    private static void TamingBeastNegate()
    {
        var s = Table();
        var sh = Kw(Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0), "Earth");
        var spell = Add(s, TamingBeast, 0, Zone.Attached, province: 0, parent: sh.Id);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, enemy, DestroyAbility, defender);
        Check(!CanUse(e, 1, "taming-beast-negate", spell.Id), "not offered to the acting player");
        Accept(e, Enabled(e, 0, "taming-beast-negate", spell.Id).Command!);
        Close(e);
        Eq(Now(e, spell).Zone, Zone.FateDiscard, "the Spell destroyed itself as the cost");
        Check(Now(e, defender).Zone is Zone.Battlefield or Zone.Home, "the Personality's destruction was negated");
        Outside(TamingBeast, "taming-beast-negate", (st, battle) =>
        {
            var c = Kw(Add(st, "training-shugenja", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null), "Earth");
            return Add(st, TamingBeast, 0, Zone.Attached, province: battle ? 0 : null, parent: c.Id);
        });
        // Its BattleOrOpen straighten still works, in both phases: bow this Shugenja, straighten a bowed Nonhuman.
        foreach (var phase in new[] { Phase.Battle, Phase.Action })
        {
            var t = Table(phase, 0); t.PriorityPlayerId = 0;
            var battle = phase == Phase.Battle;
            var shug = Kw(Add(t, "training-shugenja", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null), "Earth");
            var sp = Add(t, TamingBeast, 0, Zone.Attached, province: battle ? 0 : null, parent: shug.Id);
            var beast = Kw(Add(t, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, bowed: true, province: battle ? 0 : null), "Nonhuman");
            var te = MatchEngine.FromTestState(t);
            Accept(te, Offer(te, 0, "taming-beast-straighten", sp.Id, beast.Id));
            Check(!Now(te, beast).Bowed && Now(te, shug).Bowed, $"the Nonhuman straightened in {phase}");
        }
    }

    private static void LoyaltyDestroy()
    {
        var s = Defending();
        var target = Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        var sacrifice = Add(s, "training-scorpion-veteran", 0, Zone.Home);
        var strategy = Add(s, Loyalty, 0, Zone.Hand);
        var actor = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, actor, DestroyAbility, target);
        Check(!CanUse(e, 1, "loyalty-scorpion-destroy", strategy.Id), "not offered to the acting player");
        Check(CanUse(e, 0, "loyalty-scorpion-destroy", strategy.Id) && !CanUse(e, 0, "loyalty-scorpion-bow", strategy.Id), "only the destruction half is on offer against a destroying action");
        Accept(e, Reaction(e, 0, "loyalty-scorpion-destroy", strategy.Id, sacrifice.Id));
        Close(e);
        Eq(Now(e, sacrifice).Zone, Zone.Dead, "the performing Scorpion Clan Personality was destroyed as the cost");
        Eq(Now(e, target).Zone, Zone.Battlefield, "the target's destruction was negated");
        Eq(Now(e, strategy).Zone, Zone.FateDiscard, "the Strategy is spent");
        Outside(Loyalty, "loyalty-scorpion-destroy", (st, battle) =>
        {
            Add(st, "training-scorpion-veteran", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(st, Loyalty, 0, Zone.Hand);
        });
    }

    private static void UnsafePassage()
    {
        var s = Table(Phase.Battle, 0);
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Incapacitated, 0, Zone.Hand);
        var target = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Commander");
        var unsafeCard = Add(s, Unsafe, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "incapacitated", card.Id, target.Id));
        Eq(Window(e), "beforeActionReaction");
        Check(!CanUse(e, 0, "unsafe-passage-negate", unsafeCard.Id), "not offered to the acting player");
        Accept(e, Enabled(e, 1, "unsafe-passage-negate", unsafeCard.Id).Command!);
        Close(e);
        Eq(Now(e, target).Zone, Zone.Battlefield, "his movement was negated");
        Eq(Now(e, unsafeCard).Zone, Zone.FateDiscard, "the Strategy is spent");
        Outside(Unsafe, "unsafe-passage-negate", (st, _) => Add(st, Unsafe, 0, Zone.Hand));
        // Its Battle action is independent of the Reaction: an additional action for a bowed Follower's ability.
        var b = Table(Phase.Battle, 0);
        var c2 = Add(b, Unsafe, 0, Zone.Hand);
        var host = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(b, "training-bow-follower", 0, Zone.Attached, bowed: true, province: 0, parent: host.Id);
        var enemy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "unsafe-passage-grant", c2.Id, follower.Id));
        Accept(be, Offer(be, 0, BowAbility, follower.Id, enemy.Id));
        Check(Now(be, enemy).Bowed, "the granted additional action resolved");
    }

    private static void HundredFoldCutBow()
    {
        var s0 = Table(Phase.Battle, 1);
        var kensai = Kw(Add(s0, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Kensai"); kensai.Dishonorable = true;
        var weapon = Add(s0, "training-costly-weapon", 0, Zone.Attached, province: 0, parent: kensai.Id);
        Add(s0, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var conviction = Add(s0, Conviction, 1, Zone.Hand);
        var cut = Add(s0, HundredFold, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s0);
        Accept(e, Use(e, 1, "relentless-conviction-destroy", conviction.Id, kensai.Id));
        Eq(Window(e), "beforeActionReaction");
        Check(!CanUse(e, 1, "hundred-fold-cut-bow", cut.Id), "not offered to the acting player");
        Accept(e, Enabled(e, 0, "hundred-fold-cut-bow", cut.Id).Command!);
        Check(Now(e, weapon).Bowed, "the Weapon was bowed as the cost");
        Close(e);
        Eq(Now(e, kensai).Zone, Zone.Battlefield, "his destruction was negated");
        Outside(HundredFold, "hundred-fold-cut-bow", (st, _) => Add(st, HundredFold, 0, Zone.Hand));
        // Its Battle action (bow the Kensai or a Weapon: draw two) is independent of the Reaction.
        var s = Table(Phase.Battle, 0);
        var k = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Kensai");
        var blade = Add(s, "training-costly-weapon", 0, Zone.Attached, province: 0, parent: k.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var cut2 = Add(s, HundredFold, 0, Zone.Hand);
        var b = MatchEngine.FromTestState(s);
        var deck = b.State.Players[0].FateDeck.Count;
        Accept(b, Enabled(b, 0, "hundred-fold-cut-draw", cut2.Id, blade.Id).Command!);
        Eq(b.State.Players[0].FateDeck.Count, deck - 2, "two cards drawn");
    }

    private static void IkkeNegate()
    {
        var s = Table(Phase.Battle, 1);
        var ikke = Add(s, Ikke, 0, Zone.Battlefield, province: 0);
        var nobu = Add(s, Nobu, 1, Zone.Battlefield, province: 0);
        Gold(s, 0, 2);
        var e = Struck(s, 1, nobu, "nobu-move-home", ikke);
        Check(!CanUse(e, 1, "ikke-negate", ikke.Id), "not offered to the acting player");
        var negate = Enabled(e, 0, "ikke-negate", ikke.Id).Command!;
        Eq(e.QuoteAbilityGold(negate)!.Cost, 2, "pay 2 Gold");
        Accept(e, negate with { SourceIds = e.QuoteAbilityGold(negate)!.Payments.First().SourceIds });
        Close(e);
        Eq(Now(e, ikke).Zone, Zone.Battlefield, "the move home was negated");
        Outside(Ikke, "ikke-negate");
        // His Battle action is independent of the Reaction: move a Merchant home.
        var m = Table(Phase.Battle, 0);
        var home = Add(m, Ikke, 0, Zone.Home); var merchant = Add(m, Ikke, 0, Zone.Battlefield, province: 0);
        Add(m, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Offer(me, 0, "ikke-move-home", home.Id, merchant.Id));
        Eq(Now(me, merchant).Zone, Zone.Home, "his Merchant moved home");
    }

    private static void KakiharaForce()
    {
        var s = Defending();
        var kak = Add(s, Kakihara, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, Bower, 1, Zone.Battlefield, province: 0);
        var e = Struck(s, 1, bower, BowAbility, kak);
        var force = e.CardForceForTest(kak.Id);
        Check(!CanUse(e, 1, "kakihara-force-bonus", kak.Id), "not offered to the acting player");
        Check(!CanUse(e, 0, "kakihara-attack-force-bonus", kak.Id), "the Attack-window twin is not offered in an ordinary action's window");
        Accept(e, Enabled(e, 0, "kakihara-force-bonus", kak.Id).Command!);
        Eq(e.CardForceForTest(kak.Id), force + CardCatalog.Get(Kakihara).PersonalHonor, "a Force bonus equal to his Personal Honor");
        Check(!CanUse(e, 0, "kakihara-force-bonus", kak.Id), "spent");
        Close(e);
        Outside(Kakihara, "kakihara-force-bonus");
    }

    // ---- Reactions after an action / a battle resolved (afterActionResolvedReaction, afterResolutionReaction) --------------------------------------------------

    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({Window(e)})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({Window(e)})");
    }
    private static CardInstance PutIn(MatchState s, string def, int owner, Zone zone)
    {
        var c = Add(s, def, owner, zone);
        if (zone == Zone.FateDeck) s.Players[owner].FateDeck.Insert(0, c.Id); else if (zone == Zone.DynastyDeck) s.Players[owner].DynastyDeck.Insert(0, c.Id);
        return c;
    }
    private static CardInstance StrongholdOf(MatchState s, int player, string def) { var c = s.Cards[s.Players[player].StrongholdId]; c.DefinitionId = def; return c; }

    private const string Kikugoro = "oracle-l5r-556", Centering = "oracle-l5r-1277", Chukage = "oracle-l5r-10072", Sensei = "oracle-l5r-9899", Kanpeki = "oracle-l5r-1733", GojuKaxt = "oracle-l5r-10336", Rokuta = "oracle-l5r-7156";
    private const string Words = "oracle-l5r-9377", TacticalAssault = "oracle-l5r-9934", Otomo = "oracle-l5r-4646", Keepers = "oracle-l5r-5018", ImperialGift = "oracle-l5r-3694", Empress = "oracle-l5r-8059";
    private const string Carpenter = "oracle-l5r-1218";

    private static void KikugoroRing()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var k = Add(s, Kikugoro, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-void", 0, Zone.Hand);
        s.AdditionalActions.Add(new AdditionalActionGrant(0, [ring.Id], Battle: s.BattleNumber)); // the grant a fourth Strategy action in a phase hands out
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "kikugoro-ring-entry", k.Id), "not offered before a Ring entered play");
        Accept(e, Enabled(e, 0, "ring-of-void-recruit", ring.Id).Command!);
        PassUntil(e, () => CanUse(e, 0, "kikugoro-ring-entry", k.Id) || e.State.PendingChoice is null);
        Check(Now(e, ring).Zone == Zone.Home && CanUse(e, 0, "kikugoro-ring-entry", k.Id), "his Ring entered play: offered");
        Eq(Window(e), "afterActionResolvedReaction");
        Check(!CanUse(e, 1, "kikugoro-ring-entry", k.Id), "not offered to the other player");
        var top = e.State.Players[0].DynastyDeck.Take(2).ToArray();
        Accept(e, Enabled(e, 0, "kikugoro-ring-entry", k.Id).Command!);
        Eq(Window(e), "lookArrange", "the look at the top two Dynasty cards");
        Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: ["1:top", "0:bottom"]));
        Eq(e.State.Players[0].DynastyDeck[0], top[1], "the one kept on top"); Eq(e.State.Players[0].DynastyDeck[^1], top[0], "the other went to the bottom");
        Outside(Kikugoro, "kikugoro-ring-entry");
    }

    private static void CenteringSoul()
    {
        var s = Table(Phase.Action, 0);
        var sh = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Shugenja", "Void");
        var centering = Add(s, Centering, 0, Zone.Attached, parent: sh.Id);
        var chukage = Add(s, Chukage, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-void", 0, Zone.Home);
        for (var i = 0; i < 6; i++) PutIn(s, "training-focus-strategy", 0, Zone.FateDeck);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "centering-soul-straighten-destroy", centering.Id), "not offered with no Ring bowed by an action");
        Accept(e, Offer(e, 0, "chukage-draw", chukage.Id, ring.Id));
        Eq(Window(e), "afterActionResolvedReaction", "an action bowed a Ring");
        Check(!CanUse(e, 1, "centering-soul-straighten-destroy", centering.Id), "not offered to the other player");
        Accept(e, Offer(e, 0, "centering-soul-straighten-destroy", centering.Id, ring.Id));
        Eq(Now(e, centering).Zone, Zone.FateDiscard, "the Void Shugenja's Spell was destroyed");
        Check(!Now(e, ring).Bowed, "the Ring was straightened");
        // Outside the window: a Ring that is merely bowed (no action just bowed it) does not offer it.
        Outside(Centering, "centering-soul-straighten-destroy", (st, battle) =>
        {
            var h = Kw(Add(st, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null), "Shugenja", "Void");
            Add(st, "training-ring-of-void", 0, Zone.Home, bowed: true);
            return Add(st, Centering, 0, Zone.Attached, province: battle ? 0 : null, parent: h.Id);
        });
    }

    private static void CrippledSenseiAction()
    {
        var s = Table(Phase.Battle, 1);
        var destroyer = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sensei = Add(s, Sensei, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "crippled-sensei-create-action", sensei.Id), "not offered before one of his Personalities is destroyed");
        Accept(e, Offer(e, 1, DestroyAbility, destroyer.Id, victim.Id));
        Eq(Now(e, victim).Zone, Zone.Dead);
        Eq(Window(e), "afterActionResolvedReaction");
        Check(!CanUse(e, 1, "crippled-sensei-create-action", sensei.Id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "crippled-sensei-create-action", sensei.Id).Command!);
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == "user-scan-maimed-retainer" && c.OwnerId == 0 && c.Zone == Zone.Home), 1, "Maimed Retainer was created at home");
        Outside(Sensei, "crippled-sensei-create-action", (st, _) => Add(st, Sensei, 0, Zone.Hand));
    }

    private static void KanpekiAdditional()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var k = Add(s, Kanpeki, 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-performer-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-performer-boost", strategy.Id, null, k.Id));
        Eq(Window(e), "afterActionResolvedReaction", "the after-action window opens for Kanpeki");
        Check(!CanUse(e, 1, "kanpeki-additional-action", k.Id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "kanpeki-additional-action", k.Id).Command!);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");
        Outside(Kanpeki, "kanpeki-additional-action");
        // Not when another Personality performed the Strategy's action.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        Add(t, Kanpeki, 0, Zone.Battlefield, province: 0);
        var o = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var st2 = Add(t, "training-performer-strategy", 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Use(te, 0, "training-performer-boost", st2.Id, null, o.Id));
        Check(!te.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "kanpeki-additional-action"), "another performer's Strategy action does not trigger him");
        // His own Battle action is independent of the Reaction.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var k2 = Add(b, Kanpeki, 0, Zone.Battlefield, province: 0); var weak = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "kanpeki-destroy", k2.Id, weak.Id));
        Eq(Now(be, weak).Zone, Zone.Dead, "kanpeki-destroy still destroys a weaker enemy");
        _ = other;
    }

    private static void KaxtCreates()
    {
        var s = Table(Phase.Battle, 1);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var kaxt = Add(s, GojuKaxt, 0, Zone.Attached, province: 0, parent: host.Id);
        var destroyer = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "proxy677-kaxt-create-home", kaxt.Id), "not offered while he is in play");
        Accept(e, Offer(e, 1, DestroyAbility, destroyer.Id, kaxt.Id));
        Eq(Now(e, kaxt).Zone, Zone.FateDiscard, "Goju Kaxt was destroyed");
        Eq(Window(e), "afterActionResolvedReaction");
        Check(!CanUse(e, 1, "proxy677-kaxt-create-home", kaxt.Id), "not offered to the other player");
        Accept(e, Use(e, 0, "proxy677-kaxt-create-home", kaxt.Id));
        var made = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10813");
        Check(made.OwnerId == 0 && made.Zone == Zone.Home, "a Kaxt Ninja Personality was created in his controller's home");
        Check(!CanUse(e, 0, "proxy677-kaxt-create-home", kaxt.Id), "once");
        Outside(GojuKaxt, "proxy677-kaxt-create-home", (st, battle) =>
        {
            var h = Add(st, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(st, GojuKaxt, 0, Zone.Attached, province: battle ? 0 : null, parent: h.Id);
        });
    }

    private static void RokutaCreates()
    {
        var s = Table(Phase.Battle, 1);
        var rokuta = Add(s, Rokuta, 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, Destroyer, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "rokuta-action-create", rokuta.Id), "alive, he offers nothing");
        Accept(e, Offer(e, 1, DestroyAbility, destroyer.Id, rokuta.Id));
        Eq(Now(e, rokuta).Zone, Zone.Dead);
        Eq(Window(e), "afterActionResolvedReaction", "his Reaction window opens from the dead pile");
        Check(!CanUse(e, 1, "rokuta-action-create", rokuta.Id), "not offered to the other player");
        var enemyHonor = Honor(e, 1); var own = Honor(e, 0);
        Accept(e, Enabled(e, 0, "rokuta-action-create", rokuta.Id).Command! with { TargetPlayerId = 1 });
        Eq(Honor(e, 1), enemyHonor - 3, "the chosen player lost 3 Honor"); Eq(Honor(e, 0), own);
        Check(e.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Home && c.DefinitionId == "oracle-l5r-7157" || c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Name == "Rokuta's Apprentice"), "the Apprentice was created at home");
        Outside(Rokuta, "rokuta-action-create");
    }

    private static void WordsHaveStrength()
    {
        var s = Table(Phase.Battle, 1);
        var assault = Add(s, TacticalAssault, 1, Zone.Hand);
        var tactician = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Tactician");
        var magistrate = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Magistrate");
        var word = Add(s, Words, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "words-strength-action", word.Id), "not offered before a Courtier or Magistrate is destroyed");
        Accept(e, Offer(e, 1, "tactical-assault-destroy", assault.Id, magistrate.Id, tactician.Id));
        DeclineHeld(e);
        Eq(Now(e, magistrate).Zone, Zone.Dead, "the Magistrate was destroyed by the Battle action");
        Eq(Window(e), "afterActionResolvedReaction");
        Check(!CanUse(e, 1, "words-strength-action", word.Id), "not offered to the acting player");
        var honor = Honor(e, 1);
        Accept(e, Enabled(e, 0, "words-strength-action", word.Id).Command! with { TargetPlayerId = 1 });
        Eq(Honor(e, 1), honor - 3, "the chosen player lost 3 Honor");
        Outside(Words, "words-strength-action", (st, _) => Add(st, Words, 0, Zone.Hand));
    }

    private static void OtomoFavor()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var otomo = StrongholdOf(s, 0, Otomo);
        s.Players[0].Honor = s.Players[1].Honor + 5;
        var lobbyist = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "otomo-favor-token", otomo.Id), "not offered before taking the Favor");
        var lobby = e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby").Command!;
        Accept(e, lobby with { TargetId = lobbyist.Id });
        Eq(e.State.FavorPlayerId, 0, "Lobby took the Imperial Favor");
        Eq(Window(e), "afterActionResolvedReaction");
        Check(!CanUse(e, 1, "otomo-favor-token", otomo.Id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "otomo-favor-token", otomo.Id).Command!);
        Eq(e.State.FavorPlayerId, null, "the Favor was discarded"); Eq(Now(e, otomo).Tokens.Count(t => t.Type == "Favor"), 1, "and a Favor token added to the Stronghold");
        // Another player taking the Favor is not "you take the Imperial Favor".
        var so = Table(Phase.Action, 1); so.PriorityPlayerId = 1;
        var otomoNo = StrongholdOf(so, 0, Otomo); StrongholdOf(so, 1, Carpenter);
        so.Players[1].Honor = so.Players[0].Honor + 5;
        var l2 = Add(so, "training-lion-samurai", 1, Zone.Home);
        var eo = MatchEngine.FromTestState(so);
        Accept(eo, eo.QueryActions(1).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "lobby").Command! with { TargetId = l2.Id });
        Check(!CanUse(eo, 0, "otomo-favor-token", otomoNo.Id), "another player taking the Favor does not offer it");
        // Outside any window: ordinary Action phase / Battle phase.
        Outside(Otomo, "otomo-favor-token", (st, _) => StrongholdOf(st, 0, Otomo));
        // The Political Open action is independent of the Reaction.
        var sc = Table(Phase.Action, 0); sc.PriorityPlayerId = 0;
        var oc = StrongholdOf(sc, 0, Otomo); var courtier = Add(sc, "training-courtier-personality", 0, Zone.Home);
        var ec = MatchEngine.FromTestState(sc);
        Accept(ec, Enabled(ec, 0, "otomo-courtier-token", oc.Id, courtier.Id).Command!);
        Eq(Now(ec, oc).Tokens.Count(t => t.Type == "Favor"), 1, "a Favor token was added by the Open action");
    }

    private static void KeepersSearch()
    {
        var s = Table(Phase.Action, 0);
        var ring = Add(s, "training-ring-of-void", 0, Zone.Home);
        var strategy = Add(s, Keepers, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) PutIn(s, "training-focus-strategy", 0, Zone.FateDeck);
        var ev = PutIn(s, ImperialGift, 0, Zone.DynastyDeck);
        PutIn(s, "training-crane-samurai", 0, Zone.DynastyDeck);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "keepers-search", strategy.Id), "not offered before a Ring's action");
        Accept(e, Enabled(e, 0, "ring-of-void-draw", ring.Id).Command!);
        Eq(Window(e), "afterActionResolvedReaction", "an action from one of his Rings resolved");
        Check(!CanUse(e, 1, "keepers-search", strategy.Id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "keepers-search", strategy.Id).Command!);
        Eq(Window(e), "searchSelect");
        Accept(e, new("selectSearchCard", 0, e.Version, Choice: ImperialGift));
        Eq(e.State.Players[0].DynastyDeck[0], ev.Id, "the Event is on top of the Dynasty deck after the shuffle");
        Eq(Now(e, strategy).Zone, Zone.FateDiscard, "the Strategy was spent");
        Outside(Keepers, "keepers-search", (st, _) => Add(st, Keepers, 0, Zone.Hand));
        // Its Open action is independent of the Reaction: bow a Ring, draw two.
        var o = Table(Phase.Action, 0);
        var r2 = Add(o, "training-ring-of-air", 0, Zone.Home); var st2 = Add(o, Keepers, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) PutIn(o, "training-focus-strategy", 0, Zone.FateDeck);
        var oe = MatchEngine.FromTestState(o);
        var deck = oe.State.Players[0].FateDeck.Count;
        Accept(oe, Offer(oe, 0, "keepers-draw", st2.Id, r2.Id));
        Eq(oe.State.Players[0].FateDeck.Count, deck - 2, "two cards drawn");
    }

    private static void EmpressAddress()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var card = Add(s, Empress, 0, Zone.Hand);
        var empress = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Empress");
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var back = Add(s, "training-crane-veteran", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "empress-address-battle", card.Id), "not offered before a battle resolved");
        PassUntil(e, () => e.QueryActions(0).Any(o => o.Command?.AbilityId == "empress-address-battle" && o.Enabled));
        Eq(Window(e), "afterResolutionReaction");
        Check(!CanUse(e, 1, "empress-address-battle", card.Id), "not offered to the Attacker");
        var honor = Honor(e, 0);
        Accept(e, e.QueryActions(0).First(o => o.Command?.AbilityId == "empress-address-battle" && o.Enabled && (o.TargetIds ?? []).Contains(back.Id)).Command! with { TargetId = back.Id });
        Check(Now(e, back).Zone == Zone.Home && Now(e, empress).Bowed && Honor(e, 0) == honor + 2, "brought into play at home, the Empress bowed, 2 Honor");
        Eq(Now(e, defender).Zone, Zone.Dead, "the defender died in the resolution");
        Outside(Empress, "empress-address-battle", (st, _) => Add(st, Empress, 0, Zone.Hand));
    }

    // ---- before-action Reactions that are not about a particular target ---------------------------------------------------------------------------------------

    private const string Mahatsu = "oracle-l5r-10093", Interrogation = "oracle-l5r-10239", Fields = "oracle-l5r-9888", IndomitableHome = "oracle-l5r-3737";

    private static void GiveAbility(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));

    private static void MahatsuNegate()
    {
        // Player 1 (active) bows his own Personality with an Open action carried by a Water Shugenja: Mahatsu (player 0's, at home in the Action phase) negates it.
        var s = Table(Phase.Action, 1);
        var mahatsu = Add(s, Mahatsu, 0, Zone.Home);
        var shugenja = Add(s, "training-water-shugenja", 1, Zone.Home); GiveAbility(shugenja, "training-bow-any");
        var victim = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "mahatsu-negate", mahatsu.Id), "not offered with no action held");
        Accept(e, Offer(e, 1, "training-bow-any", shugenja.Id, victim.Id));
        Eq(Window(e), "beforeActionReaction", "the before-action window opened for Mahatsu");
        Check(!CanUse(e, 1, "mahatsu-negate", mahatsu.Id), "not offered to the acting player");
        Accept(e, Enabled(e, 0, "mahatsu-negate", mahatsu.Id).Command!);
        Close(e);
        Check(!Now(e, victim).Bowed, "the Water action's effects were negated");
        // A plain action opens nothing for him; in a battle he must be at the current battlefield.
        var p = Table(Phase.Action, 1);
        var m2 = Add(p, Mahatsu, 0, Zone.Home); var plain = Add(p, "training-bow-any-source", 1, Zone.Home); var v2 = Add(p, "training-lion-samurai", 1, Zone.Home);
        var pe = MatchEngine.FromTestState(p);
        Accept(pe, Offer(pe, 1, "training-bow-any", plain.Id, v2.Id));
        Check(Window(pe) != "beforeActionReaction" && Now(pe, v2).Bowed, "a plain action: no window");
        var b = Table(Phase.Battle, 1);
        var m3 = Add(b, Mahatsu, 0, Zone.Home); var def = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var fire = Kw(Add(b, Bower, 1, Zone.Battlefield, province: 0), "Fire");
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, BowAbility, fire.Id, def.Id));
        Check(!CanUse(be, 0, "mahatsu-negate", m3.Id), "at home he is not offered in a battle");
        Outside(Mahatsu, "mahatsu-negate");
        // His Limited search is independent of the Reaction.
        var q = Table(Phase.Action, 0);
        foreach (var pl in q.Players) { foreach (var c in q.Cards.Values.Where(c => c.Zone is Zone.FateDeck && c.OwnerId == pl.Id).ToArray()) q.Cards.Remove(c.Id); pl.FateDeck.Clear(); }
        var m4 = Add(q, Mahatsu, 0, Zone.Home);
        var air = PutIn(q, "training-false-ring-of-air", 0, Zone.FateDeck);
        var qe = MatchEngine.FromTestState(q);
        Accept(qe, Enabled(qe, 0, "mahatsu-search", m4.Id).Command!);
        DeclineHeld(qe);
        Eq(Window(qe), "searchSelect");
        Accept(qe, new("selectSearchCard", 0, qe.Version, Choice: air.DefinitionId));
        Check(qe.State.Players[0].Hand.Contains(air.Id), "the Air card is in his hand");
        _ = m2;
    }

    private static void InterrogationNegate()
    {
        // A Terrain's Open action bows player 0's Holding; player 0 holds Interrogation.
        var u = Table(Phase.Action, 1);
        var sh = Add(u, "training-shugenja", 1, Zone.Home); var fields = Add(u, Fields, 1, Zone.Attached, parent: sh.Id);
        var holding = Add(u, IndomitableHome, 0, Zone.Home); var card = Add(u, Interrogation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(u);
        Check(!CanUse(e, 0, "pe-interrogation-negate", card.Id), "not offered with no action held");
        Accept(e, Offer(e, 1, "set-fields-holding", fields.Id, holding.Id));
        Eq(Window(e), "beforeActionReaction");
        Check(!CanUse(e, 1, "pe-interrogation-negate", card.Id), "not offered to the acting player");
        Accept(e, Enabled(e, 0, "pe-interrogation-negate", card.Id).Command!);
        Eq(Now(e, card).Zone, Zone.FateDiscard, "the Strategy was played");
        Close(e);
        Check(!Now(e, holding).Bowed, "the Holding is not bowed");
        // Not against an action on a Personality, and not outside a window.
        var n = Table(Phase.Action, 1);
        var bower = Add(n, "training-bow-any-source", 1, Zone.Home); var foe = Add(n, "training-crane-samurai", 0, Zone.Home); var c2 = Add(n, Interrogation, 0, Zone.Hand);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 1, "training-bow-any", bower.Id, foe.Id));
        Check(Window(ne) != "beforeActionReaction" || !CanUse(ne, 0, "pe-interrogation-negate", c2.Id), "an action on a Personality is not answered");
        Outside(Interrogation, "pe-interrogation-negate", (st, _) => Add(st, Interrogation, 0, Zone.Hand));
        // Its Open action is independent of the Reaction: pay 2 Gold, draw a card (either player's Action phase).
        var o = Table(Phase.Action, 1); o.PriorityPlayerId = 0;
        var c3 = Add(o, Interrogation, 0, Zone.Hand); Gold(o, 0, 3); PutIn(o, "training-focus-strategy", 0, Zone.FateDeck);
        var oe = MatchEngine.FromTestState(o);
        var hand = oe.State.Players[0].Hand.Count;
        Accept(oe, Paid(oe, 0, 2, Enabled(oe, 0, "pe-interrogation-draw", c3.Id).Command!));
        Eq(oe.State.Players[0].Hand.Count, hand, "the Strategy left the hand and a card was drawn");
    }

    // ---- Engage window (selectBattle): Reactions "after engaging" ---------------------------------------------------------------------------------------

    private const string ForestCover = "oracle-l5r-10372", FalseVoid = "oracle-l5r-10534", Scouts = "oracle-l5r-10519";
    private static MatchState EngageState()
    {
        var t = Table(Phase.BattleSelection, 0); t.PriorityPlayerId = 0; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        return t;
    }
    private static void Engage(MatchEngine e) { Accept(e, new("selectBattle", 0, e.Version, Province: 0)); Eq(Window(e), "engageReaction", "the Engage window opens"); }

    private static void ForestCoverRecon()
    {
        var t = EngageState();
        StrongholdOf(t, 0, Carpenter); Gold(t, 0, 4);
        var scout = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Scout"); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(t, ForestCover, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(t);
        Check(!CanUse(e, 0, "wl-forest-cover-recon", card.Id), "not offered before engaging");
        Engage(e);
        Check(!CanUse(e, 1, "wl-forest-cover-recon", card.Id), "not offered to the other player");
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[0], "no Reconnaissance before");
        Accept(e, Enabled(e, 0, "wl-forest-cover-recon", card.Id, scout.Id).Command!);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance at the battlefield");
        Check(!CanUse(e, 0, "wl-forest-cover-attach", card.Id), "the two halves share one use");
        Outside(ForestCover, "wl-forest-cover-recon", (st, _) => Add(st, ForestCover, 0, Zone.Hand));
        // A Personality that is not a Scout is no performer.
        var p = EngageState(); StrongholdOf(p, 0, Carpenter);
        var plain = Add(p, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(p, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var c2 = Add(p, ForestCover, 0, Zone.Hand);
        var pe = MatchEngine.FromTestState(p); Accept(pe, new("selectBattle", 0, pe.Version, Province: 0)); // with no Scout there is no Reaction: no window need open
        Check(!Options(pe, 0, "wl-forest-cover-recon", c2.Id).Any(o => o.Enabled && o.Command?.PerformerId == plain.Id), "a non-Scout may not perform it");
    }

    private static void FalseRingOfVoidLock()
    {
        var s = EngageState();
        var ring = Add(s, FalseVoid, 0, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "wl-false-ring-of-void-lock", ring.Id), "not offered before engaging");
        Engage(e);
        Check(!CanUse(e, 1, "wl-false-ring-of-void-lock", ring.Id), "not offered to the other player");
        var o = Enabled(e, 0, "wl-false-ring-of-void-lock", ring.Id);
        Check(o.TargetIds!.Contains(foe.Id) && o.TargetIds.Contains(mine.Id), "any Personality at any location");
        Accept(e, Offer(e, 0, "wl-false-ring-of-void-lock", ring.Id, foe.Id));
        Check(Now(e, ring).Bowed, "the Ring bowed as the cost");
        Check(Now(e, foe).MovementLocks.Any(l => l.Turn == e.State.TurnNumber), "he will not move this turn");
        Outside(FalseVoid, "wl-false-ring-of-void-lock");
        // A Ring still in the hand cannot Engage.
        var h = EngageState(); var hand = Add(h, FalseVoid, 0, Zone.Hand); Add(h, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var he = MatchEngine.FromTestState(h); Accept(he, new("selectBattle", 0, he.Version, Province: 0)); // a Ring in the hand is no Reaction: no window need open
        Check(!CanUse(he, 0, "wl-false-ring-of-void-lock", hand.Id), "only from play");
    }

    private static void ReconScouts()
    {
        MatchEngine Build(out CardInstance scouts, out CardInstance enemy, bool atBattlefield = true)
        {
            var t = EngageState();
            var host = Add(t, "training-crane-samurai", 0, atBattlefield ? Zone.Battlefield : Zone.Home, province: atBattlefield ? 0 : null);
            scouts = Add(t, Scouts, 0, Zone.Attached, province: atBattlefield ? 0 : null, parent: host.Id);
            enemy = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(t);
            if (atBattlefield) Engage(e); else Accept(e, new("selectBattle", 0, e.Version, Province: 0)); // away from the battlefield no Reaction exists: no window need open
            return e;
        }
        var e = Build(out var scouts, out var enemy);
        Check(!CanUse(e, 1, "wl-scouts-engage-penalty", scouts.Id), "not offered to the other player");
        var force = e.CardForceForTest(enemy.Id);
        Accept(e, Offer(e, 0, "wl-scouts-engage-penalty", scouts.Id, enemy.Id));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance at this battlefield");
        Eq(e.CardForceForTest(enemy.Id), force - 3, "-3F to the target");
        Check(!CanUse(e, 0, "wl-scouts-engage", scouts.Id), "one use for the printed Engage");
        var away = Build(out var far, out _, atBattlefield: false);
        Check(!CanUse(away, 0, "wl-scouts-engage-penalty", far.Id), "not at the engaged battlefield: not offered");
        Outside(Scouts, "wl-scouts-engage-penalty", (st, battle) =>
        {
            var host = Add(st, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(st, Scouts, 0, Zone.Attached, province: battle ? 0 : null, parent: host.Id);
        });
    }

    // ---- Attack windows, battle-action windows, bow windows, payment, entry-of-another-card windows -----------------------------------------------------------

    private const string Nikutai = "oracle-l5r-9872", Nubane = "oracle-l5r-4429", EasyPersuasion = "oracle-l5r-9905", Profits = "oracle-l5r-6075", StealCandle = "oracle-l5r-10704", StormlessFury = "oracle-l5r-7512";
    private const string KaiuVillage = "oracle-l5r-4148", KaiuWeapon = "oracle-l5r-10006", SpidersWeb = "oracle-l5r-8352", Jakuei = "oracle-l5r-4416";

    private static void NikutaiStraighten()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var nikutai = Add(s, Nikutai, 0, Zone.Attached, bowed: true, parent: host.Id);
        var sibling = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id);
        var nubane = Add(s, Nubane, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "nikutai-straighten", nikutai.Id), "not offered before a Follower of the unit bows");
        Accept(e, Use(e, 0, "nubane-bow", nubane.Id, sibling.Id));
        Check(Now(e, sibling).Bowed, "the sibling Follower bowed");
        Eq(Window(e), "nikutaiReaction");
        Check(!CanUse(e, 1, "nikutai-straighten", nikutai.Id), "not offered to the other player");
        Check(e.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "nikutai-straighten") && e.QueryActions(0).Any(a => a.Type == "declineReaction"), "listed to its player with a pass (issue #833: the window listed neither)");
        Check(!e.QueryActions(1).Any(a => a.Command?.AbilityId == "nikutai-straighten"), "and not listed to the other player");
        Accept(e, Use(e, 0, "nikutai-straighten", nikutai.Id));
        Check(!Now(e, sibling).Bowed && Now(e, nikutai).Bowed, "the sibling Follower straightened (Nikutai stays bowed)");
        // A Follower of another unit bowing opens nothing.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var h1 = Add(t, "training-crane-samurai", 0, Zone.Home); var n2 = Add(t, Nikutai, 0, Zone.Attached, parent: h1.Id);
        var h2 = Add(t, "training-lion-samurai", 0, Zone.Home); var f2 = Add(t, "training-follower", 0, Zone.Attached, parent: h2.Id); var nb = Add(t, Nubane, 0, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Use(te, 0, "nubane-bow", nb.Id, f2.Id));
        Check(Window(te) != "nikutaiReaction" && !CanUse(te, 0, "nikutai-straighten", n2.Id), "another unit's Follower: no window");
        Outside(Nikutai, "nikutai-straighten", (st, battle) =>
        {
            var h = Add(st, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(st, Nikutai, 0, Zone.Attached, bowed: true, province: battle ? 0 : null, parent: h.Id);
        });
        // His Battle attack is independent of the Reaction.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var bh = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var nk = Add(b, Nikutai, 0, Zone.Attached, province: 0, parent: bh.Id);
        var weak = Add(b, "training-follower-payer", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "nikutai-attack", nk.Id, weak.Id));
    }

    private static void EasyPersuasionHonor()
    {
        MatchEngine Purchase(bool discount, bool merchant, out CardInstance entering, out CardInstance card, out CardInstance trader)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
            s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            entering = s.Cards[s.Players[0].Provinces[0].CardId!];
            entering.DefinitionId = "training-crane-samurai"; entering.FaceUp = true;
            Gold(s, 0, 3);
            card = Add(s, EasyPersuasion, 1, Zone.Hand);
            trader = Add(s, "training-lion-courtier", 1, Zone.Home);
            if (merchant) Kw(trader, "Merchant");
            var e = MatchEngine.FromTestState(s);
            var cost = e.QuotePurchase(0, entering.Id, clanDiscount: discount).Cost;
            Accept(e, new("purchase", 0, e.Version, CardId: entering.Id, ClanDiscount: discount, SourceIds: e.PaymentOptions(0, cost).First().SourceIds));
            for (var guard = 0; guard < 6 && Window(e) is "proclaim" or "entryReaction"; guard++) Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        var e = Purchase(true, true, out var entering, out var card, out var trader);
        Eq(Window(e), "p3EntryReaction", "the window after his entry, for the other player");
        Check(!CanUse(e, 0, "easy-persuasion-honor", card.Id), "not offered to the entering Personality's controller");
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 1, "easy-persuasion-honor", card.Id, trader.Id).Command!);
        Eq(Honor(e, 0), honor - 2, "his controller lost his base Personal Honor (2)");
        Check(Now(e, trader).Bowed, "the Merchant bowed as the cost");
        Check(Purchase(false, true, out _, out _, out _).State.PendingChoice?.Type != "p3EntryReaction", "full Gold Cost paid: no window");
        Check(Purchase(true, false, out _, out _, out _).State.PendingChoice?.Type != "p3EntryReaction", "no Merchant: no window");
        Outside(EasyPersuasion, "easy-persuasion-honor", (st, _) => Add(st, EasyPersuasion, 0, Zone.Hand));
        // Its Battle action is independent of the Reaction: pay Gold equal to a unit's cost minus 5, move it home.
        var s2 = Table(Phase.Battle, 0);
        var c2 = Add(s2, EasyPersuasion, 0, Zone.Hand); Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        Gold(s2, 0, 3);
        var e2 = MatchEngine.FromTestState(s2);
        var o = Options(e2, 0, "easy-persuasion-move", c2.Id).First(x => x.Enabled);
        Accept(e2, o.Command! with { TargetId = enemy.Id, SourceIds = o.TargetGold![enemy.Id].Payments.First().SourceIds });
        Eq(Now(e2, enemy).Zone, Zone.Home, "the unit moved home");
    }

    private static void ProfitsReduce()
    {
        var s = Table(Phase.Action, 0);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); var k1 = Add(s, "training-border-keep", 0, Zone.Home); var k2 = Add(s, "training-border-keep", 0, Zone.Home);
        var profits = Add(s, Profits, 0, Zone.Home); var item = Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var reduce = profits.Id + "#profits-reduce";
        var paying = new PaymentSubject(item, PaymentKind.Equip, Wielder: host);
        Check(e.PaymentSourcesForTest(0, paying).Any(x => x.Id == reduce && x.Produced == -1), "offered as a source while paying for a card with a base Gold Cost of 1 or more");
        Check(!e.PaymentSourcesForTest(1, paying).Any(x => x.Id == reduce), "not offered to the other player");
        Check(!e.PaymentSourcesForTest(0, new PaymentSubject(profits, PaymentKind.Play)).Any(x => x.Id == reduce), "not for a card with a base Gold Cost of 0");
        Check(!e.PaymentSourcesForTest(0, new PaymentSubject(profits, PaymentKind.Action)).Any(x => x.Id == reduce), "not for an action's cost");
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "pay-profits-reduce"), "never a standalone action in the ordinary phases");
        Accept(e, new("equip", 0, e.Version, CardId: item.Id, TargetId: host.Id, SourceIds: [k1.Id, k2.Id, reduce]));
        Eq(Now(e, item).AttachedTo, host.Id, "the item was paid for with 3 Gold net");
        Eq(Now(e, profits).Tokens.Count(t => t.Type == "Wealth"), 1, "Profits gained a Wealth token");
        // Its Limited action puts it into play, independent of the Reaction.
        var o = Table(Phase.Action, 0); var hp = Add(o, Profits, 0, Zone.Hand);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Enabled(oe, 0, "pay-profits-enter", hp.Id).Command!);
        Eq(Now(oe, hp).Zone, Zone.Home, "Profits is in play");
    }

    private static void StealCandleInterrupt()
    {
        var w = Table(Phase.Battle, 0);
        var attacker = Add(w, "training-recon-attacker", 0, Zone.Battlefield, province: 0);
        var target = Add(w, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var fh = Add(w, "training-shugenja", 1, Zone.Battlefield, province: 0); var candle = Add(w, StealCandle, 1, Zone.Attached, province: 0, parent: fh.Id);
        var e = MatchEngine.FromTestState(w);
        Check(!CanUse(e, 1, "tf-steal-candle-interrupt", candle.Id), "not offered with no Attack pending");
        Accept(e, Offer(e, 0, "training-weapon-attack", attacker.Id, target.Id));
        Eq(Window(e), "attackReaction");
        var strength = e.State.PendingAttack!.Strength;
        Check(!CanUse(e, 0, "tf-steal-candle-interrupt", candle.Id), "not offered to the Attack's player (the Spell is the other player's)");
        Accept(e, Enabled(e, 1, "tf-steal-candle-interrupt", candle.Id).Command!);
        Eq(e.State.PendingAttack?.Strength ?? strength + 2, strength + 2, "+2 strength");
        Check(Now(e, candle).Bowed, "the Spell bowed as its cost");
        Outside(StealCandle, "tf-steal-candle-interrupt", (st, battle) =>
        {
            var h = Add(st, "training-shugenja", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(st, StealCandle, 0, Zone.Attached, province: battle ? 0 : null, parent: h.Id);
        });
        // Its Fire Battle attack (Ranged 0) is independent of the Reaction.
        var b = Table(Phase.Battle, 0);
        var bh = Add(b, "training-shugenja", 0, Zone.Battlefield, province: 0); var bc = Add(b, StealCandle, 0, Zone.Attached, province: 0, parent: bh.Id);
        var foe = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "tf-steal-candle-attack", bc.Id, foe.Id));
    }

    private static void StormlessFuryBow()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var holder = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        var fury = Add(s, StormlessFury, 0, Zone.Attached, province: 0, parent: holder.Id);
        var performer = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "stormless-fury-bow", fury.Id), "not offered before a Battle action resolved");
        Accept(e, Offer(e, 0, "training-weapon-attack", performer.Id, enemy.Id));
        for (var guard = 0; guard < 6 && Window(e) is "attackReaction" or "beforeActionReaction"; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(Window(e), "battleActionReaction");
        Check(!CanUse(e, 1, "stormless-fury-bow", fury.Id), "not offered to the other player");
        Accept(e, Enabled(e, 0, "stormless-fury-bow", fury.Id).Command!);
        Check(Now(e, performer).Bowed, "the Personality who performed the action was bowed");
        Outside(StormlessFury, "stormless-fury-bow", (st, battle) =>
        {
            var h = Add(st, "training-thunder-shugenja", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return Add(st, StormlessFury, 0, Zone.Attached, province: battle ? 0 : null, parent: h.Id);
        });
        // Its other Reaction (attach from the hand) is the same window: a copy in the hand attaches to a Thunder Shugenja.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var perf2 = Add(t, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0); Add(t, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var inHand = Add(t, StormlessFury, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        var en = t.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        Accept(te, Offer(te, 0, "training-weapon-attack", perf2.Id, en.Id));
        for (var guard = 0; guard < 6 && Window(te) is "attackReaction" or "beforeActionReaction"; guard++) Accept(te, new("declineReaction", te.State.PriorityPlayerId, te.Version));
        Check(CanUse(te, 0, "stormless-fury-attach", inHand.Id), "the hand copy's attach Reaction is offered in the same window");
    }

    private static void AttackReactorBoost()
    {
        foreach (var boost in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var attacker = Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0);
            var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var reactor = Add(s, "training-attack-reactor", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Check(!CanUse(e, 1, "training-attack-boost", reactor.Id), "not offered with no Attack pending");
            Accept(e, Offer(e, 1, "training-weapon-attack", attacker.Id, target.Id));
            Eq(Window(e), "attackReaction");
            var strength = e.State.PendingAttack!.Strength;
            Check(!CanUse(e, 0, "training-attack-boost", reactor.Id), "not offered to the other player");
            if (boost)
            {
                Accept(e, Enabled(e, 1, "training-attack-boost", reactor.Id).Command!);
                Eq(e.State.PendingAttack?.Strength, strength + 2, "+2 to the pending Attack");
            }
            else { Accept(e, Enabled(e, 1, "training-attack-negate", reactor.Id).Command!); Eq(e.State.PendingAttack?.Strength, 0, "the Attack is negated"); }
            for (var guard = 0; Window(e) == "attackReaction" && guard < 6; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(Now(e, target).Zone == Zone.Dead, boost, boost ? "boosted to 4 it destroys a Force 3 Personality" : "negated: the target survives");
        }
        Outside("training-attack-reactor", "training-attack-boost");
    }

    private static void KaiuVillageWeapon()
    {
        string regionId = "";
        var s = MatchEngine.CreateEconomyScenario().State;
        Gold(s, 0, 12); s.Players[0].Honor = 20;
        var region = Add(s, KaiuVillage, 0, Zone.Region, province: 3); s.Players[0].Provinces[3].RegionId = region.Id; regionId = region.Id;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-crane-samurai"; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, 5).First().SourceIds));
        Check(Window(e) is "entryReaction" or "proclaim", "the entry window is open");
        var force = e.CardForceForTest(cardId);
        Check(Options(e, 1, "kaiu-village-weapon", regionId).All(o => !o.Enabled), "not offered to the other player");
        var known = e.State.Cards.Keys.ToHashSet();
        var cmd = Enabled(e, 0, "kaiu-village-weapon", regionId).Command!;
        Eq(e.QuoteAbilityGold(cmd)!.Cost, 2, "pay 2 Gold");
        Accept(e, cmd with { SourceIds = e.QuoteAbilityGold(cmd)!.Payments.First().SourceIds });
        var made = e.State.Cards.Values.Single(c => !known.Contains(c.Id));
        Check(made.DefinitionId == KaiuWeapon && made.Zone == Zone.Attached && made.AttachedTo == cardId, "the Kaiu Weapon was created and attached to the Personality who entered");
        Eq(e.CardForceForTest(cardId), force + 1, "+1F");
        // A Region in play in an ordinary Action phase / Battle phase has no window to react in.
        Outside(KaiuVillage, "kaiu-village-weapon", (st, _) =>
        {
            var r = Add(st, KaiuVillage, 0, Zone.Region, province: 3); st.Players[0].Provinces[3].RegionId = r.Id; return r;
        });
    }

    private static void SpidersWebAttach()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        StrongholdOf(s, 0, SpidersWeb);
        Gold(s, 0, 8); s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-kensai"; s.Cards[cardId].FaceUp = true;
        var costly = Add(s, "training-costly-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var web = s.Players[0].StrongholdId;
        Check(!CanUse(e, 0, "spiders-web-attach-kensai", web), "not offered before a Personality enters play");
        Accept(e, new("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, 3).First().SourceIds));
        Check(!Options(e, 1, "spiders-web-attach-kensai", web).Any(o => o.Enabled), "not offered to the other player");
        var kensai = Options(e, 0, "spiders-web-attach-kensai", web).First(o => o.Enabled);
        Eq(kensai.TargetGold![costly.Id].Cost, 0, "3 less 4, to a minimum of 0");
        Accept(e, Offer(e, 0, "spiders-web-attach-kensai", web, costly.Id));
        Eq(Now(e, costly).AttachedTo, cardId, "the Weapon is attached to the entering Kensai");
        Check(!CanUse(e, 0, "spiders-web-attach-kensai", web), "once per turn");
        Outside("training-crane-samurai", "spiders-web-attach-kensai", (st, _) => StrongholdOf(st, 0, SpidersWeb));
    }

    private static void JakueiFavor()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1; StrongholdOf(s, 1, Carpenter);
        var jakuei = Add(s, Jakuei, 1, Zone.Battlefield, province: 0);
        var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 1, "jakuei-favor", jakuei.Id), "not offered before a duel resolved");
        Accept(e, new("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: jakuei.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new("acceptDuel", 0, e.Version));
        Accept(e, new("strike", 0, e.Version));
        Accept(e, new("duelistRefocus", 1, e.Version));
        Check(!CanUse(e, 0, "jakuei-favor", jakuei.Id), "not offered to the duel's other player");
        Accept(e, Enabled(e, 1, "jakuei-favor", jakuei.Id).Command!);
        Eq(e.State.FavorPlayerId, 1, "he took the Imperial Favor");
        Outside(Jakuei, "jakuei-favor");
        // His Political Battle bow is independent of the Reaction.
        var b = Table(Phase.Battle, 0); StrongholdOf(b, 0, Carpenter);
        var j2 = Add(b, Jakuei, 0, Zone.Battlefield, province: 0); var target = Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "jakuei-bow", j2.Id, target.Id));
        Check(Now(be, target).Bowed, "bowed");
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-reaction-4", [
        ("Isawa Sakonoko (targeted-by-action, Spell way): negates another Personality's destruction from a held action, offered to the targeted player only; not for an action on Sakonoko himself", SakonokoSpell),
        ("Perplexing Guests (targeted-by-action): discards the targeted Personality with his unit; its Battle action still creates a Guest", PerplexingGuests),
        ("Riku (targeted-by-action): an action that would target Tamahime targets him instead; his Iaijutsu Battle duel still works", RikuRedirect),
        ("Shosuro Aroru exp2 (targeted-by-action): redirects an action aimed at a Ninja to another card and may move after; his Battle attack still works", AroruRedirect),
        ("Social Grace (targeted-by-action): negates the dishonoring of a targeted Courtier; its Open action still gives a Force penalty", SocialGraceNegate),
        ("Taming the Beast (targeted-by-action): an Earth Shugenja's Spell destroys itself to negate a defender's destruction; its BattleOrOpen straighten still works in both phases", TamingBeastNegate),
        ("The Loyalty of the Scorpion (targeted-by-action, destroy half): destroys a performing Scorpion Personality to negate the target's destruction", LoyaltyDestroy),
        ("Unsafe Passage (targeted-by-action): negates a Commander's movement; its Battle grant of an additional action still works", UnsafePassage),
        ("Hundred-Fold Cut (targeted-by-action, bow way): bowing a Weapon negates a Kensai's destruction; its Battle draw still works", HundredFoldCutBow),
        ("Yasuki Ikke (targeted-by-action): pay 2 Gold to negate a Battle action's effects on him; his move-home Battle action still works", IkkeNegate),
        ("Akodo Kakihara (before-action-resolves): a Force bonus equal to his Personal Honor after an action targets him; not the Attack twin, not the acting player", KakiharaForce),
        ("Asako Kikugoro (after-action-resolved, Ring entry): look at the top two Dynasty cards after his Ring enters play; not before, not for the other player", KikugoroRing),
        ("Centering the Soul (after-action-resolved): a Void Shugenja's Spell destroys itself to straighten a Ring an action bowed; not without such a Ring", CenteringSoul),
        ("Crippled Sensei (after-action-resolved): creates Maimed Retainer after one of its player's Personalities is destroyed by an action", CrippledSenseiAction),
        ("Daigotsu Kanpeki (after-action-resolved): an additional Battle action after his own Strategy Battle action; not for another performer; his Battle destroy still works", KanpekiAdditional),
        ("Goju Kaxt (after-action-resolved): creates Kaxt at home after he is destroyed by an action; not while he is in play", KaxtCreates),
        ("Shosuro Rokuta (after-action-resolved, from the dead pile): creates his Apprentice and a player loses 3 Honor after another player's action destroyed him", RokutaCreates),
        ("Words Have Strength (after-action-resolved): a chosen player loses 3 Honor after another player's Battle action destroyed a Magistrate", WordsHaveStrength),
        ("Kyuden Otomo (after-action-resolved, trait): discard the Favor you took for a Favor token; not when another player took it; its Political Open still works", OtomoFavor),
        ("Meeting the Keepers (after-action-resolved): after an action from one of its player's Rings it finds an Event; its Open draw still works", KeepersSearch),
        ("The Empress' Address (after-battle-resolution): brings a dead Personality back after a battle destroyed a defender; only the Defender", EmpressAddress),
        ("Naka Mahatsu (before-action-resolves): negates an element action; not a plain one; at home not in a battle; his Limited search still works", MahatsuNegate),
        ("Interrogation (before-action-resolves): negates an action that bows the player's Holding; not an action on a Personality; its Open draw still works", InterrogationNegate),
        ("Forest Cover (after-engaging): Reconnaissance for the performing Scout in the Engage window only; a non-Scout cannot perform it", ForestCoverRecon),
        ("False Ring of the Void (after-engaging): bow it so a Personality will not move this turn, in the Engage window only and only from play", FalseRingOfVoidLock),
        ("Reconnaissance Scouts (after-engaging-at-own-battlefield): Reconnaissance and -3F to an enemy at the engaged battlefield only", ReconScouts),
        ("Nikutai (sibling-follower-bowed): straightens a Follower of its unit that just bowed even while bowed; its Battle attack still works", NikutaiStraighten),
        ("Easy Persuasion (p3-personality-entered-play): the entering Personality's controller loses Honor when he paid less than base cost; its Battle move still works", EasyPersuasionHonor),
        ("Profits (paying-gold-cost): produce 1 less Gold when paying for a card of base cost 1 or more; its Limited put-into-play still works", ProfitsReduce),
        ("Steal the Candle's Flame (before-attack-resolves): +2 strength to a pending Attack; its Fire Battle attack still works", StealCandleInterrupt),
        ("Stormless Fury (after-battle-action-resolves): bows the Personality who performed a Battle action; its attach twin is offered in the same window", StormlessFuryBow),
        ("Training attack reactor (before-attack-resolves): +2 to or negate a pending Attack for its controller only", AttackReactorBoost),
        ("Kaiu Village (own-personality-enters-from-province): pay 2 Gold for a Kaiu Weapon when the controller's Personality enters from a province", KaiuVillageWeapon),
        ("The Spider's Web (own-personality-enters-play): the discounted Weapon attach for an entering Kensai, once per turn", SpidersWebAttach),
        ("Kitsuki Jakuei (own-won-duel): take the Imperial Favor after winning a duel; his Political bow still works", JakueiFavor),
        ("Kitsu Tamasine (own-enters-play): offered to her controller only in the entering-play window, creates the Lion Ancestor; her Battle boost still works", TamasineCreate),
        ("Togashi Hiroto (own-enters-play): four Fire tokens in the entering-play window only; his Battle bow still works", HirotoTokens),
        ("Training skin donor / Tsudoken entry (own-enters-play): lose 3 Honor in the entering-play window only; his Open bow works for either player", SkinDonorHonorLoss),
        ("Asako Izuna (own-enters-play): dishonor and Kolat a legal enemy Personality in her entering-play window only; the three keyword choices share one use", IzunaKolat),
        ("The Broken Man (own-enters-play, Invest 5): two +1F tokens in the entering-play window only; his Earth Battle bow still works", BrokenManInvest),
        ("Yasuki Tsujiken (own-enters-play): marks a Holding in the entering-play window only; his Open straighten still works", TsujikenWatch),
        ("Celestial Sword of the Crab (own-enters-play): 2 Honor when equipped, in the entering-play window only; its straighten works in Battle and Action", CrabSwordHonor),
        ("Celestial Sword of the Phoenix (own-enters-play): 2 Honor when equipped, in the entering-play window only; its Battle/Open return still works", PhoenixSwordHonor),
    ]);
}
