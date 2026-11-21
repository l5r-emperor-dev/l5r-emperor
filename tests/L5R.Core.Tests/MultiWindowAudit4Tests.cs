using L5R.Core;
using static RegressionAssertions;

// Issue #830, slice 4: hand-built positions that satisfy each ability's OWN preconditions (Imperial Favor, cards in a discard pile, Rings, dishonorable targets, Kata,
// Terrain, tokens ...), which the generic MultiWindowAuditTests kitchen sink cannot. Each ability is probed in the four windows (battle as attacker, battle as defender,
// Action phase as the active player, Action phase as the other player): where its printed timing allows it, the engine must OFFER it and ACCEPT the offered command
// and the effect must match the card text; everywhere else it must be refused. The builder is called once per window with the table already in that phase.
internal static partial class MultiWindowAudit4Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase, int actor) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Options(e, player, ability, cardId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] sel) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, sel);
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai";
    private static readonly string[] Windows = ["battle-attacker", "battle-defender", "action-active", "action-other"];

    // What a builder returns: the source card, and the target / performer / selections the command names; Verify checks the effect after it ran.
    private sealed record Fx(CardInstance Source, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, Action<MatchEngine>? Verify = null);
    private sealed class WindowException(string message, Exception inner) : Exception(message, inner);
    private delegate Fx Builder(MatchState s, int me, bool battle);

    private static CardInstance Mk(MatchState s, string def, int owner, bool battle, bool bowed = false) =>
        Add(s, def, owner, battle ? Zone.Battlefield : Zone.Home, bowed, battle ? 0 : null);
    private static CardInstance Kw(CardInstance c, params string[] keywords) { foreach (var k in keywords) c.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return c; }
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Now(MatchEngine e, CardInstance c) => e.State.Cards[c.Id];

    private static GameCommand Offered(MatchEngine e, int me, string ability, Fx fx, bool provinceChoice = false)
    {
        var all = Options(e, me, ability, fx.Source.Id);
        var ok = all.Where(o => o.Enabled && o.Command is not null && (fx.Target is null || o.TargetIds is null || o.TargetIds.Contains(fx.Target))).ToArray();
        Check(ok.Length > 0, $"{ability} on {fx.Source.Id} (target {fx.Target}) was not offered enabled to player {me}: " + string.Join("; ", all.Select(a => $"{a.Label} [{a.Enabled}] {a.Reason} {(a.TargetIds is null ? "" : string.Join(",", a.TargetIds))}")));
        var pick = (fx.Performer is null ? null : ok.FirstOrDefault(o => o.Command!.PerformerId == fx.Performer)) ?? ok[0];
        return pick.Command! with { TargetId = fx.Target ?? pick.Command.TargetId, PerformerId = fx.Performer ?? pick.Command.PerformerId, Selections = fx.Sel ?? pick.Command.Selections, Province = provinceChoice ? pick.Provinces?.FirstOrDefault() : pick.Command.Province };
    }

    // Probes one ability in all four windows. `allowed` defaults to the ability's printed timing; the effect is verified in every window it runs in.
    private static void Probe(string ability, Builder build, string[]? allowed = null, string? timing = null, bool provinceChoice = false)
    {
        allowed ??= MultiWindowAuditTests.Expected(timing ?? AbilityCatalog.All[ability].Timing);
        foreach (var window in Windows)
        {
            var battle = window.StartsWith("battle");
            var me = window is "battle-attacker" or "action-other" ? 1 : 0;
            var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0);
            if (!battle) { s.ActivePlayerId = 0; }
            s.PriorityPlayerId = me;
            var fx = build(s, me, battle);
            var e = MatchEngine.FromTestState(s);
            try
            {
                if (allowed.Contains(window))
                {
                    Accept(e, Offered(e, me, ability, fx, provinceChoice));
                    fx.Verify?.Invoke(e);
                }
                else
                {
                    Check(!Options(e, me, ability, fx.Source.Id).Any(o => o.Enabled), $"{ability} must not be offered");
                    Reject(e, Use(e, me, ability, fx.Source.Id, fx.Target, fx.Performer, fx.Sel ?? []));
                }
            }
            catch (Exception ex) when (ex is not WindowException) { throw new WindowException($"[{window}] {ex.Message}", ex); }
        }
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit-4", [
        ("All Within the World (Battle/Open): straighten a Ring sharing an element, in both phases and both players' windows", AllWithinRing),
        ("All Within the World (Battle/Open): straighten the Shugenja himself, in both phases and both players' windows", AllWithinSelf),
        ("Blood of Shahai (Maho Battle/Open): -1C to a Personality below his printed Chi, in both phases and both players' windows", ShahaiChi),
        ("Chuda Hikano (Home Battle): bow to straighten your Oni, in a battle as attacker and defender, refused in the Action phase", HikanoStraighten),
        ("Chuda Hikano (Maho Limited): destroy an Ikoma Ayumu and himself, Action phase active player only", HikanoMaho),
        ("Doji Razan (Limited): destroy a Fudo card and himself, Action phase active player only", RazanDestroy),
        ("Green Lake Dojo (Open, own turn): bow, destroy and grant Cavalry on the owner's own turn only", GreenLakeOwnTurn),
        ("Green Lake Dojo (Open, other turn): bow and grant Cavalry on another player's turn only", GreenLakeOtherTurn),
        ("Insulting Gesture (Favor Political Battle): discard the Favor, dishonor, bow, send home and cost 2 Honor, as attacker and defender; refused in the Action phase", InsultingGestureShame),
        ("Yasuki Baiko (Favor Political Limited): discard the Favor to cost a dishonorable target's controller 1 Honor, Action phase active player only", BaikoShame),
        ("Kitsuki Yoyugi (Political Open): bow to lock a Unique non-Unicorn Personality's base abilities, either player's Action phase", YoyugiLock),
        ("Master of Beasts (Battle/Open): bow the Shugenja to straighten two Shadowlands Nonhumans, in both phases and both players' windows", MasterOfBeasts),
        ("Moto Ming-Gwok (Open): bow or straighten a Ring, either player's Action phase, refused in battle", MingGwokRing),
        ("P'an Ku's Madness (Open): discard the Celestial so a Personality with a Madness token cannot attack or defend, either player's Action phase", MadnessBar),
        ("Seeking the Question (Limited): from play, look at the top card of a deck, Action phase active player only", SeekingLook),
        ("Shinobi Vassal (Battle): Equip to a Ninja at the battlefield from the hand, as attacker and defender; refused in the Action phase", VassalEquip),
        ("Strength of the Divine (Battle, Force): +2F to an opposed Paragon and Melee Attack with his Force", DivineForce),
        ("Strength of the Divine (Battle, Personal Honor): +2PH to an opposed Paragon and Melee Attack with his Personal Honor", DivineHonor),
        ("Tetsuo (Battle): destroy an enemy Personality with fewer Weapons, as attacker and defender; refused in the Action phase", TetsuoWeaker),
        ("The Honesty of the Phoenix (Battle/Open): a Phoenix player names a card, in both phases and both players' windows", HonestyPhoenix),
        ("The Power of a Word (Battle): move one to three enemy dishonorable Personalities home, as attacker and defender", PowerWordMove),
        ("Theological Indecision (Limited): discard a Celestial, Action phase active player only", TheologicalDiscard),
        ("Find Your Center (Battle): from play, straighten a Ring, as attacker and defender; refused in the Action phase", FindCenterStraighten),
        ("Unchecked Fury (Battle): from play, Equip a Spell to an unbowed opposed Shugenja and take an additional action, as attacker and defender", UncheckedFuryEquip),
        ("Training move source: move your Personality from home to the current battlefield (Battle only)", TrainingMoveCurrent),
        ("Training move source: move an enemy Personality to another unresolved battlefield (Battle only)", TrainingMoveAnywhere),
        ("Training move source: move himself to an adjacent unresolved battlefield (Battle only)", TrainingMoveAdjacent),
        ("Training move source: move the performer and an enemy Personality to an adjacent battlefield (Battle only)", TrainingMovePair),
        ("Ring of Earth (Battle recruit): put into play from the hand after winning a battle with enemy units present, as the attacker or the defender; never in the Action phase", RingOfEarthRecruit),
        ("Rings of Air (Battle recruit): put into play on a granted battle, as attacker and defender; the Action-phase Favor grant waits for the next battle", RingOfAirRecruit),
    ]);

    // ---- All Within the World ------------------------------------------------------------------------------------------------------
    private static Fx AllWithinFx(MatchState s, int me, bool battle, bool ring)
    {
        var shugenja = Mk(s, "training-seeds5-fire-shugenja", me, battle, bowed: true);
        var spell = Add(s, "oracle-l5r-326", me, Zone.Attached, province: battle ? 0 : null, parent: shugenja.Id);
        var fire = Add(s, "training-ring-of-fire-stub", me, Zone.Home, bowed: true);
        return ring ? new(spell, fire.Id, shugenja.Id, Verify: e => { Check(!Now(e, fire).Bowed && Now(e, shugenja).Bowed, "the Ring straightened, the Shugenja did not"); })
                    : new(spell, null, shugenja.Id, Verify: e => { Check(!Now(e, shugenja).Bowed && Now(e, fire).Bowed, "the Shugenja straightened, the Ring did not"); });
    }
    private static void AllWithinRing() => Probe("promo-d-all-within-ring", (s, me, b) => AllWithinFx(s, me, b, true));
    private static void AllWithinSelf() => Probe("promo-d-all-within-self", (s, me, b) => AllWithinFx(s, me, b, false));

    // ---- Blood of Shahai -----------------------------------------------------------------------------------------------------------
    private static void ShahaiChi() => Probe("wl-shahai-chi", (s, me, b) =>
    {
        Mk(s, Crane, me, b);
        var damaged = Mk(s, Lion, 1 - me, b); damaged.Modifiers.Add(new("test-source", "test", "Chi", -1));
        var shahai = Add(s, "oracle-l5r-10602", me, Zone.Celestial);
        return new(shahai, damaged.Id, Verify: e => { Eq(e.PersonalityChiForTest(damaged.Id), 1, "Chi 3 -1 -1"); Eq(Now(e, shahai).Zone, Zone.Celestial, "the Celestial stays"); });
    });

    // ---- Chuda Hikano --------------------------------------------------------------------------------------------------------------
    private static void HikanoStraighten() => Probe("w735-hikano-straighten", (s, me, b) =>
    {
        var hikano = Add(s, "oracle-l5r-10665", me, Zone.Home);
        var oni = Kw(Mk(s, Crane, me, b, bowed: true), "Nonhuman", "Oni");
        Mk(s, Lion, 1 - me, b);
        return new(hikano, oni.Id, Verify: e => { Check(!Now(e, oni).Bowed && Now(e, hikano).Bowed, "the Oni straightened and Hikano bowed for the cost"); });
    });
    private static void HikanoMaho() => Probe("w735-hikano-maho", (s, me, b) =>
    {
        var hikano = Add(s, "oracle-l5r-10665", me, Zone.Home);
        var ayumu = Mk(s, "oracle-l5r-3595", 1 - me, b);
        return new(hikano, ayumu.Id, Verify: e => { Check(Now(e, ayumu).Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard && Now(e, hikano).Zone is Zone.Dead or Zone.DynastyDiscard or Zone.FateDiscard, "Ayumu and then Hikano were destroyed"); });
    });

    // ---- Doji Razan ----------------------------------------------------------------------------------------------------------------
    private static void RazanDestroy() => Probe("wl1-razan-destroy", (s, me, b) =>
    {
        var razan = Add(s, "oracle-l5r-10626", me, Zone.Home);
        var fudo = Mk(s, "training-fudo-personality", 1 - me, b);
        return new(razan, fudo.Id, Verify: e => { Check(Now(e, fudo).Zone != Zone.Home && Now(e, fudo).Zone != Zone.Battlefield && Now(e, razan).Zone != Zone.Home, "the Fudo card and Razan were destroyed"); });
    });

    // ---- Green Lake Dojo -----------------------------------------------------------------------------------------------------------
    private static Fx GreenLakeFx(MatchState s, int me, bool ownTurn)
    {
        var lake = Add(s, "oracle-l5r-10265", me, Zone.Home);
        var target = Add(s, Crane, me, Zone.Home);
        return new(lake, target.Id, Verify: e =>
        {
            Check(Now(e, target).GrantedKeywords.Any(k => k.Keyword == "Cavalry"), "he has Cavalry");
            Check(ownTurn ? Now(e, lake).Zone != Zone.Home : Now(e, lake).Zone == Zone.Home && Now(e, lake).Bowed, "destroyed on its owner's turn, only bowed otherwise");
        });
    }
    private static void GreenLakeOwnTurn() => Probe("wl-green-lake-dojo-cavalry-own-turn", (s, me, b) => GreenLakeFx(s, me, true), ["action-active"]);
    private static void GreenLakeOtherTurn() => Probe("wl-green-lake-dojo-cavalry", (s, me, b) => GreenLakeFx(s, me, false), ["action-other"]);

    // ---- Favor actions -------------------------------------------------------------------------------------------------------------
    private static void InsultingGestureShame() => Probe("insulting-gesture-shame", (s, me, b) =>
    {
        s.FavorPlayerId = me;
        Mk(s, Crane, me, b);
        var card = Add(s, "oracle-l5r-3779", me, Zone.Hand);
        var target = Mk(s, Lion, 1 - me, b);
        var honor = s.Players[1 - me].Honor;
        return new(card, target.Id, Verify: e =>
        {
            Check(e.State.FavorPlayerId is null, "the Favor was discarded");
            var c = Now(e, target);
            Check(c.Dishonorable && c.Bowed && c.Zone == Zone.Home, "dishonored, bowed and sent home");
            Eq(e.State.Players[1 - me].Honor, honor - 2, "his controller loses 2 Honor");
        });
    });

    private static void BaikoShame() => Probe("baiko-shame", (s, me, b) =>
    {
        s.FavorPlayerId = me;
        var baiko = Add(s, "oracle-l5r-10044", me, Zone.Home);
        var target = Mk(s, Lion, 1 - me, b); target.Dishonorable = true;
        var honor = s.Players[1 - me].Honor;
        return new(baiko, target.Id, Verify: e =>
        {
            Check(e.State.FavorPlayerId is null, "the Favor was discarded");
            Eq(e.State.Players[1 - me].Honor, honor - 1, "his controller loses 1 Honor");
        });
    });

    // ---- Personalities with Open actions -------------------------------------------------------------------------------------------
    private static void YoyugiLock() => Probe("yoyugi-lock", (s, me, b) =>
    {
        var yoyugi = Add(s, "oracle-l5r-9817", me, Zone.Home);
        var kudome = Mk(s, "oracle-l5r-9840", 1 - me, b); // Unique, Phoenix Clan
        return new(yoyugi, kudome.Id, Verify: e =>
        {
            Check(Now(e, yoyugi).Bowed, "a Unique non-Unicorn target costs the bow");
            Check(e.State.DisabledAbilities.Any(d => d.CardId == kudome.Id && d.UsageKey == "kudome-exp-duel"), "Kudome's base abilities are locked");
        });
    });

    private static void MingGwokRing() => Probe("wl-pc-mingwok-ring", (s, me, b) =>
    {
        var ming = Add(s, "oracle-l5r-10509", me, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 1 - me, Zone.Home);
        return new(ming, ring.Id, Verify: e =>
        {
            Eq(e.State.PendingChoice?.Type, "effectChoice", "the acting player chooses bow or straighten");
            var bow = e.QueryActions(me).Single(a => a.Type == "chooseEffect" && a.Enabled && a.Command!.Choice == "bow");
            Accept(e, bow.Command!);
            Check(Now(e, ring).Bowed, "the Ring is bowed");
        });
    });

    private static void MadnessBar() => Probe("w736-madness-bar", (s, me, b) =>
    {
        var celestial = Add(s, "oracle-l5r-10418", me, Zone.Celestial);
        var mad = Mk(s, Lion, 1 - me, b); mad.Tokens.Add(new CardToken("fx-madness", "Madness", "test"));
        var sane = Mk(s, Crane, 1 - me, b);
        return new(celestial, mad.Id, Verify: e =>
        {
            Eq(Now(e, celestial).Zone, Zone.DynastyDiscard, "the Celestial was discarded");
            Check(e.HasRestriction(Now(e, mad), RestrictionKind.NoAttack) && e.HasRestriction(Now(e, mad), RestrictionKind.NoDefend), "he cannot attack or defend");
            Check(!e.HasRestriction(Now(e, sane), RestrictionKind.NoAttack), "nobody else is restricted");
        });
    });

    // ---- Spells and Kata -----------------------------------------------------------------------------------------------------------
    // "Even if this card is not at the current battlefield": the Shugenja stays at home in every window; in a battle the targets are at the battlefield (a Battle action's targets must be).
    private static void MasterOfBeasts() => Probe("master-of-beasts-straighten", (s, me, b) =>
    {
        var shugenja = Add(s, "training-shugenja", me, Zone.Home);
        var spell = Add(s, "oracle-l5r-10128", me, Zone.Attached, parent: shugenja.Id);
        Mk(s, Lion, 1 - me, b);
        var oni1 = Mk(s, "training-seeds5-shadowlands-oni", me, b, bowed: true);
        var oni2 = Mk(s, "training-seeds5-shadowlands-oni", 1 - me, b, bowed: true);
        return new(spell, Sel: [Pick("targets", oni1.Id, oni2.Id)], Verify: e =>
        {
            Check(!Now(e, oni1).Bowed && !Now(e, oni2).Bowed && Now(e, shugenja).Bowed, "both straightened; the Shugenja bowed as the cost");
        });
    });

    private static void SeekingLook() => Probe("seeking-question-look", (s, me, b) =>
    {
        var seeking = Add(s, "oracle-l5r-6565", me, Zone.Home);
        return new(seeking, Verify: e => Check(e.State.PendingLook is not null, "the look begins"));
    });

    private static void FindCenterStraighten() => Probe("wlsac-find-center-straighten", (s, me, b) =>
    {
        var kata = Add(s, "oracle-l5r-10939", me, Zone.Home);
        var ring = Add(s, "training-ring-of-void", 1 - me, Zone.Home, bowed: true);
        Mk(s, Crane, me, b); Mk(s, Lion, 1 - me, b);
        return new(kata, ring.Id, Verify: e => { Check(!Now(e, ring).Bowed, "the Ring is straightened"); Eq(Now(e, kata).Zone, Zone.Home, "the Kata stays in play"); });
    });

    private static void UncheckedFuryEquip() => Probe("wlsac-unchecked-fury-equip", (s, me, b) =>
    {
        var kata = Add(s, "oracle-l5r-10962", me, Zone.Home);
        var shugenja = Mk(s, "training-shugenja", me, b); Mk(s, Lion, 1 - me, b);
        var spell = Add(s, "training-spell", me, Zone.Hand);
        return new(kata, Verify: e =>
        {
            Eq(Now(e, spell).Zone, Zone.Attached, "the Spell is attached"); Eq(Now(e, spell).AttachedTo, shugenja.Id, "to the Shugenja");
            Check(e.State.AdditionalActions.Any(g => g.PlayerId == me), "an additional action");
        });
    });

    // ---- Strategies and a Follower -------------------------------------------------------------------------------------------------
    private static void VassalEquip() => Probe("wl-vassal-equip", (s, me, b) =>
    {
        var ninja = Kw(Mk(s, Crane, me, b), "Ninja"); Mk(s, Lion, 1 - me, b);
        var vassal = Add(s, "oracle-l5r-10517", me, Zone.Hand);
        return new(vassal, Verify: e => Check(Now(e, vassal).Zone == Zone.Attached && Now(e, vassal).AttachedTo == ninja.Id, "attached to the Ninja"));
    });

    private static Fx DivineFx(MatchState s, int me, bool b, bool force)
    {
        var strat = Add(s, "oracle-l5r-10555", me, Zone.Hand);
        var paragon = Kw(Mk(s, Crane, me, b), "Paragon"); // Force 3, Personal Honor 2
        var enemy = Mk(s, "training-lion-courtier", 1 - me, b); // Force 2
        return new(strat, enemy.Id, paragon.Id, Verify: e =>
        {
            Eq(Now(e, enemy).Zone, Zone.Dead, "the Attack (strength 5 or 4) destroys a Force-2 target");
            Eq(Now(e, paragon).Modifiers.Where(m => m.Stat == (force ? "Force" : "PersonalHonor")).Sum(m => m.Amount), 2, "+2 to the chosen stat");
        });
    }
    private static void DivineForce() => Probe("strength-of-the-divine-force", (s, me, b) => DivineFx(s, me, b, true));
    private static void DivineHonor() => Probe("strength-of-the-divine-honor", (s, me, b) => DivineFx(s, me, b, false));

    private static void TetsuoWeaker() => Probe("tetsuo-destroy-weaker-weaponed", (s, me, b) =>
    {
        var tetsuo = Mk(s, "oracle-l5r-7943", me, b);
        Add(s, "training-weapon", me, Zone.Attached, province: b ? 0 : null, parent: tetsuo.Id);
        var victim = Mk(s, Lion, 1 - me, b);
        return new(tetsuo, victim.Id, Verify: e => Eq(Now(e, victim).Zone, Zone.Dead, "the Personality with fewer Weapons is destroyed"));
    });

    private static void HonestyPhoenix() => Probe("honesty-phoenix-name", (s, me, b) =>
    {
        s.Players[me].Clan = "Phoenix";
        var card = Add(s, "oracle-l5r-8137", me, Zone.Hand);
        var phoenix = Mk(s, "training-samurai", me, b); Mk(s, Lion, 1 - me, b);
        return new(card, Performer: phoenix.Id, Verify: e =>
        {
            Eq(e.State.PendingChoice?.Type, "effectChoice", "the card is a choice");
            Accept(e, e.QueryActions(me).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == "name:Veiled Menace").Command!);
            Eq(e.State.SincerityWatches.Single().Title, "Veiled Menace", "the title is recorded");
            Eq(Now(e, card).Zone, Zone.RemovedFromGame, "removed from the game");
        });
    });

    private static void PowerWordMove() => Probe("power-word-move", (s, me, b) =>
    {
        Mk(s, Crane, me, b);
        var word = Add(s, "oracle-l5r-8262", me, Zone.Hand);
        var a = Mk(s, Lion, 1 - me, b); a.Dishonorable = true;
        var d = Mk(s, "training-lion-veteran", 1 - me, b); d.Dishonorable = true;
        var honorable = Mk(s, Lion, 1 - me, b);
        return new(word, Sel: [Pick("targets", a.Id, d.Id)], Verify: e =>
        {
            Eq(Now(e, a).Zone, Zone.Home, "the first went home"); Eq(Now(e, d).Zone, Zone.Home, "and the second");
            Check(Now(e, honorable).Zone == (b ? Zone.Battlefield : Zone.Home), "an honorable one stays");
        });
    });

    private static void TheologicalDiscard() => Probe("theological-indecision-discard", (s, me, b) =>
    {
        var card = Add(s, "oracle-l5r-9937", me, Zone.Hand);
        var celestial = Add(s, "oracle-l5r-10602", 1 - me, Zone.Celestial);
        return new(card, celestial.Id, Verify: e => Eq(Now(e, celestial).Zone, Zone.DynastyDiscard, "the Celestial was discarded"));
    });

    // ---- Training move fixtures: a synthetic Personality with one Battle move per destination rule --------------------------------
    // Battle at province 1 with 0, 1 and 2 unresolved, so there are adjacent and other battlefields to move to.
    private static void MoveTable(MatchState s, bool b) { if (b) { s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 2]; } }
    private static CardInstance AtB(MatchState s, string def, int owner, bool b, bool bowed = false) => Add(s, def, owner, b ? Zone.Battlefield : Zone.Home, bowed, b ? 1 : null);

    private static void TrainingMoveCurrent() => Probe("training-move-current", (s, me, b) =>
    {
        MoveTable(s, b);
        var src = AtB(s, "training-move-source", me, b); AtB(s, Lion, 1 - me, b);
        var home = Add(s, Crane, me, Zone.Home);
        return new(src, home.Id, Verify: e => Check(Now(e, home).Zone == Zone.Battlefield && Now(e, home).ProvinceIndex == 1, "he is at the current battlefield"));
    });
    private static void TrainingMoveAnywhere() => Probe("training-move-anywhere", (s, me, b) =>
    {
        MoveTable(s, b);
        var src = AtB(s, "training-move-source", me, b); var foe = AtB(s, Lion, 1 - me, b);
        return new(src, foe.Id, Verify: e => Check(Now(e, foe).ProvinceIndex is 0 or 2 && Now(e, src).ProvinceIndex == 1, "only the target moved to another unresolved battlefield"));
    }, provinceChoice: true);
    private static void TrainingMoveAdjacent() => Probe("training-move-source-adjacent", (s, me, b) =>
    {
        MoveTable(s, b);
        var src = AtB(s, "training-move-source", me, b); var stay = AtB(s, Lion, 1 - me, b);
        return new(src, Verify: e => Check(Now(e, src).ProvinceIndex is 0 or 2 && Now(e, stay).ProvinceIndex == 1, "only the source moved"));
    }, provinceChoice: true);
    private static void TrainingMovePair() => Probe("training-move-performer-and-target", (s, me, b) =>
    {
        MoveTable(s, b);
        var src = AtB(s, "training-move-source", me, b); var perf = AtB(s, Crane, me, b); var foe = AtB(s, Lion, 1 - me, b);
        return new(src, foe.Id, perf.Id, Verify: e => Check(Now(e, perf).ProvinceIndex is 0 or 2 && Now(e, foe).ProvinceIndex == Now(e, perf).ProvinceIndex && Now(e, src).ProvinceIndex == 1, "the performer and the target moved together, the source stayed"));
    }, provinceChoice: true);

    // ---- Rings put into play by a recruit grant -------------------------------------------------------------------------------------
    private const string Air = "oracle-l5r-6333", Earth = "oracle-l5r-6334";
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);

    private static void RingOfEarthRecruit()
    {
        foreach (var winner in new[] { 1, 0 }) // 1 is the attacker of the table, 0 the defender
        {
            var s = Table(Phase.Battle, 1); s.BattleNumber = 1; s.PriorityPlayerId = 1;
            var ring = Add(s, Earth, winner, Zone.Hand);
            Add(s, "training-lion-veteran", winner, Zone.Battlefield, province: 0);
            Add(s, Crane, 1 - winner, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(!Options(e, winner, "ring-of-earth-recruit", ring.Id).Any(o => o.Enabled), "no grant before the battle is won");
            Accept(e, Pass(e)); Accept(e, Pass(e));
            Eq(e.State.PendingChoice?.Type, "battleRecruit", $"the win by player {winner} opens the after-battle window");
            Accept(e, Offered(e, winner, "ring-of-earth-recruit", new(ring)));
            Eq(Now(e, ring).Zone, Zone.Home, $"put into play from the hand by the {(winner == 1 ? "attacker" : "defender")}");
        }
        foreach (var me in new[] { 0, 1 })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = me;
            var ring = Add(s, Earth, me, Zone.Hand);
            Add(s, Crane, 0, Zone.Home); Add(s, Lion, 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Check(!Options(e, me, "ring-of-earth-recruit", ring.Id).Any(o => o.Enabled), "not offered in the Action phase");
            Reject(e, Use(e, me, "ring-of-earth-recruit", ring.Id));
        }
    }

    private static void RingOfAirRecruit()
    {
        foreach (var def in new[] { Air, "training-ring-of-air" })
            foreach (var me in new[] { 1, 0 }) // attacker, defender
            {
                var s = Table(Phase.Battle, 1); s.BattleNumber = 1; s.PriorityPlayerId = me;
                var ring = Add(s, def, me, Zone.Hand);
                Add(s, Crane, 0, Zone.Battlefield, province: 0); Add(s, Lion, 1, Zone.Battlefield, province: 0);
                var e = MatchEngine.FromTestState(s);
                Check(!Options(e, me, "ring-of-air-recruit", ring.Id).Any(o => o.Enabled), "not offered without the grant");
                s.AdditionalActions.Add(new AdditionalActionGrant(me, [ring.Id], Battle: 1));
                var g = MatchEngine.FromTestState(s);
                Accept(g, Offered(g, me, "ring-of-air-recruit", new(ring)));
                Eq(Now(g, ring).Zone, Zone.Home, "put into play from the hand");
            }
        // A third Spell or Kiho action of a new title in the Action phase grants it for the NEXT battle: not usable in the Action phase, usable once that battle begins.
        foreach (var me in new[] { 0, 1 })
        {
            var s = Table(Phase.Action, me); s.PriorityPlayerId = me; s.Players[me].SpellKihoActionsStamp = s.TurnNumber.ToString(); s.Players[me].SpellKihoActionTitles = ["Spell A", "Spell B"]; s.BattleNumber = 0;
            var ring = Add(s, "training-ring-of-air", me, Zone.Hand);
            var kiho = Add(s, "training-kiho-strategy", me, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, me, "training-plain-open", kiho.Id));
            Check(e.State.AdditionalActions.Any(g => g.ScopeCardIds?.Contains(ring.Id) == true), "the third Spell or Kiho action of a new title grants the recruit");
            Check(!Options(e, me, "ring-of-air-recruit", ring.Id).Any(o => o.Enabled), "not usable in the Action phase");
            Reject(e, Use(e, me, "ring-of-air-recruit", ring.Id));
            var later = e.State; later.Phase = Phase.Battle; later.BattleNumber = 1; later.CurrentBattleProvince = 0; later.UnresolvedBattlefields = [0]; later.PriorityPlayerId = me;
            var b = MatchEngine.FromTestState(later);
            Accept(b, Offered(b, me, "ring-of-air-recruit", new(ring)));
            Eq(Now(b, ring).Zone, Zone.Home, "usable once the next battle begins");
        }
    }
}
