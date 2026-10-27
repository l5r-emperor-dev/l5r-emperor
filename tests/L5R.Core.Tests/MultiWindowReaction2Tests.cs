using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #830 (multi-window audit, Reaction slice 2): every Reaction ability of a card with several action windows is opened through ordinary
// play (the same commands a player issues), must be on offer enabled to the right player and accepted with its printed effect, and must NOT be on
// offer outside its window (an ordinary Action / Battle phase with nothing pending) or to the other player. Where the card also carries a Battle /
// Open / Limited action, one quick check shows that action still works independently of the Reaction.
internal static partial class MultiWindowReaction2Tests
{
    public static void Run(TestRunner runner) => runner.Run("multi-window-reaction-2", [
        ("Asako Izuna: izuna-shadowlands is offered to her buyer in the entry window only, dishonors the legal target and gives Shadowlands", Izuna),
        ("Celestial Sword of the Lion: the +2 Honor entry Reaction opens on Equip for its controller only; the Battle create action is unaffected", SwordLion),
        ("Celestial Sword of the Unicorn: the +2 Honor entry Reaction opens on Equip for its controller only; the Battle bow action is unaffected", SwordUnicorn),
        ("Hida Yamadera: the refill Reaction is offered after entering from a province only and refills it face-up; her Battle bow is unaffected", Yamadera),
        ("Otomo Demiyah: demiyah-gain is offered in the entry window to her buyer only, for either player", Demiyah),
        ("Shosuro Ryoken: ryoken-dishonor is offered in the entry window to his buyer only and dishonors the target", Ryoken),
        ("Togashi Torazu: torazu-fire-tokens gives three +1F Fire tokens on entry only, and the Battle attack then works", Torazu),
        ("Training choose-a-player Personality: training-choose-player-gain is offered to the buyer in the entry window only", TrainingChoosePlayer),
        ("Words of Consecration: the honor trait is offered on attaching to an Air Shugenja only, to its controller only", Words),
        ("Armor of the Heavens: armor-heavens-gain opens when another player's action targets its wearer, for the wearer's controller only", Armor),
        ("Astonishing Accusation: accusation-negate opens when another player's Political action targets your Personality; the Limited half still works", Accusation),
        ("Fudo: promo-fudo-negate opens when an action targets him and he has a Charging token; his Limited and Battle actions are unaffected", Fudo),
        ("Inciting Mistrust: p5-inciting-mistrust-negate opens when a Ranged Attack targets your Ninja; the Battle discard action is unaffected", Mistrust),
        ("Isawa Norimichi: norimichi-negate opens when an action targets an Earth Shugenja at his location; his Battle action is unaffected", Norimichi),
        ("Know No Fear: know-no-fear-negate-fear opens when a Fear action targets your performing Samurai", KnowNoFear),
        ("The Power of a Word: power-word-reaction opens when a Battle action targets your Courtier; the Battle move is unaffected", PowerWord),
        ("Yasuki Tanimura: tanimura-political-pay opens when another player's Battle action targets him, for his controller only", Tanimura),
        ("Reverse Fate: w737-reverse-fate-minus opens for a Void Shugenja's Spell when an action discards a top card", ReverseFate),
        ("Near Miss: pay-near-miss-negate opens when another player's Battle action targets your attachment", NearMiss),
        ("Well Protected: pe-well-protected-bow opens when an Attack targets your armoured Personality", WellProtected),
        ("Kaiu Fumiko: fumiko-attack-boost opens when her controller's Ranged Attack is targeted, not for Melee or the enemy's Attack; her Battle bow is unaffected", Fumiko),
        ("Moshi Umiko: umiko-attack-reduce opens when a Ranged Attack is targeted, not for Melee", Umiko),
        ("Precision: precision-reduce opens in a Melee or Ranged Attack window from the hand; its Battle attack is unaffected", Precision),
        ("Unholy Strike: unholy-strike-boost opens in an Attack window from the hand; its Battle attack is unaffected", UnholyStrike),
        ("The Earth's Strength: earths-strength-negate-move opens when an action would move a defending Personality; its Battle straighten is unaffected", EarthsStrength),
        ("The Height of Courage: height-courage-negate opens when an action would move a Personality; its Battle boost is unaffected", HeightOfCourage),
        ("Closing the Gap: pe-closing-the-gap-home opens after an action that moved a Personality to the battlefield", ClosingTheGap),
        ("Daidoji Akeha: akeha-recon-draw opens after her player's Recon action from a Strategy resolves", Akeha),
        ("Lying in Ambush: pf-lying-in-ambush-battlefield opens after an action that created a Personality at the battlefield", LyingInAmbush),
        ("Shiba Tsukimi: tsukimi-home opens after another player's Battle action; her Battle attack is unaffected", Tsukimi),
        ("Shosuro Hawado: hawado-exp-move-home opens after a Battle action resolves with him at the battlefield", Hawado),
        ("Destructive Priorities: destructive-priorities-reaction opens before its controller gains Honor on his turn; the Kata can still be played", DestructivePriorities),
        ("Solid Defense: solid-defense-negate-favor opens before another player's action costs Honor; the Battle move is unaffected", SolidDefense),
        ("Kenro-ji-jin's Bounty: its Reaction is a Gold source only while paying for a Farm", Kenro),
        ("Yoritomo Sakuma: sakuma-produce-gold is a Gold source only while he is unbowed and its controller pays", Sakuma),
        ("Qalyar: qalyar-recon-plus opens when engaging at her battlefield", Qalyar),
        ("Stockpiled Resources: stockpiled-attach-commander opens after engaging for a Commander performer", Stockpiled),
        ("Temple Grounds: w738-temple-engage opens after engaging; its Battle half is unaffected", TempleGrounds),
        ("The Yari of the Shabura: pe-yari-bar-targeting opens after engaging for the wearer's controller; its Ranged attack is unaffected", Yari),
        ("Akodo Dairuko: dairuko-rally opens before a battle resolves at her battlefield; her Melee attack is unaffected", Dairuko),
        ("Entrenched Position: entrenched-negate opens when the Attacker announces early; its Battle bow is unaffected", Entrenched),
        ("Feign Death: feign-death-battle opens after a battle destroyed your Personality", FeignDeath),
        ("Kitsuki Horume: horume-honor opens after he wins a duel", Horume),
        ("Suitengu's Torch: torch-protect opens when you announce a Thunder Battle action; its Battle move is unaffected", Torch),
    ]);

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static string Def(string ability) => CardCatalog.Cards.Values.Where(c => !c.CreatedOnly && (c.Abilities ?? []).Contains(ability)).OrderBy(c => c.Id, StringComparer.Ordinal).First().Id;
    private static bool On(MatchEngine e, int player, string ability, string? card = null) => CanUse(e, player, ability, card);
    private static void Settle(MatchEngine e) { for (var guard = 0; guard < 16 && e.State.PendingChoice is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static CardInstance Mk(MatchState s, string def, int owner, Zone zone = Zone.Battlefield, int? province = 0, string? parent = null) => Add(s, def, owner, zone, province: zone is Zone.Battlefield or Zone.Attached ? province : null, parent: parent);
    private static void Kw(CardInstance c, params string[] keywords) { foreach (var k in keywords) c.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); }
    private static void Gold(MatchState s, int player, int n = 10) { for (var i = 0; i < n; i++) Add(s, "training-marketplace", player, Zone.Home); }

    // The card as its own controller has it in an ordinary position: a Personality at the battlefield / home, an attachment on a host, a Strategy in hand.
    private static CardInstance Place(MatchState s, string def, int owner = 0)
    {
        var battle = s.Phase == Phase.Battle; var zone = battle ? Zone.Battlefield : Zone.Home; int? at = battle ? 0 : null;
        switch (CardCatalog.Get(def).Type)
        {
            case CardType.Personality: return Add(s, def, owner, zone, province: at);
            case CardType.Item:
            case CardType.Follower:
            case CardType.Spell:
                var host = Add(s, "training-lion-samurai", owner, zone, province: at);
                return Add(s, def, owner, Zone.Attached, province: at, parent: host.Id);
            case CardType.Holding: return Add(s, def, owner, Zone.Home);
            case CardType.Stronghold: var sh = s.Cards[s.Players[owner].StrongholdId]; sh.DefinitionId = def; return sh;
            case CardType.Celestial: return Add(s, def, owner, Zone.Celestial);
            default: return Add(s, def, owner, Zone.Hand);
        }
    }

    // Nothing is pending: in an ordinary Action phase (either player active) and Battle phase (either player holding priority) neither player is offered the Reaction.
    private static void Quiet(string def, params string[] abilities) => QuietWith(def, null, abilities);
    private static void QuietWith(string def, Action<CardInstance>? prep, params string[] abilities)
    {
        foreach (var phase in new[] { Phase.Action, Phase.Battle })
            foreach (var actor in new[] { 0, 1 })
            {
                var s = Table(phase, actor);
                var card = Place(s, def); prep?.Invoke(card);
                Mk(s, "training-lion-samurai", 1); Mk(s, "training-crane-samurai", 0);
                var e = MatchEngine.FromTestState(s);
                Check(e.State.PendingChoice is null, "no window is open");
                foreach (var p in new[] { 0, 1 }) foreach (var a in abilities) Check(!On(e, p, a, card.Id), $"{a} is not on offer to player {p} in an ordinary {phase} phase (active {actor})");
            }
    }

    // The purchase of a Personality from province 0 (player 0), opening the entering-play window. `setup` adds the rest of the position before the buy.
    private static (MatchEngine E, string Id) Recruit(string def, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        Gold(s, 0); s.Players[0].Honor = 20;
        var id = s.Players[0].Provinces[0].CardId!; s.Cards[id].DefinitionId = def; s.Cards[id].FaceUp = true;
        setup?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: id, SourceIds: e.PaymentOptions(0, e.QuotePurchase(0, id).Cost).First().SourceIds));
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entering-play window is open");
        return (e, id);
    }

    // ---- own-enters-play --------------------------------------------------------------------------------------
    private static void Izuna()
    {
        CardInstance victim = null!, proud = null!;
        var (e, id) = Recruit(Def("izuna-shadowlands"), s =>
        {
            victim = Add(s, "training-lion-samurai", 1, Zone.Home); victim.Modifiers.Add(new("test-source", "test", "PersonalHonor", -CardCatalog.Get("training-lion-samurai").PersonalHonor));
            proud = Add(s, "training-lion-veteran", 1, Zone.Home); proud.Modifiers.Add(new("test-source", "test", "PersonalHonor", 10));
        });
        Check(!On(e, 1, "izuna-shadowlands", id), "not to the other player");
        var targets = Options(e, 0, "izuna-shadowlands", id).Single(o => o.Enabled).TargetIds!;
        Check(targets.Contains(victim.Id) && !targets.Contains(proud.Id), "only an enemy Personality with Personal Honor at most her Chi");
        Accept(e, Offer(e, 0, "izuna-shadowlands", id, victim.Id));
        Check(e.State.Cards[victim.Id].Dishonorable && e.HasKeyword(victim.Id, "Shadowlands"), "dishonored and Shadowlands");
        Check(!On(e, 0, "izuna-shadowlands", id), "the use is spent");
        Quiet(Def("izuna-shadowlands"), "izuna-shadowlands", "izuna-kolat", "izuna-ninja");
    }

    private static MatchEngine Equipped(string sword, string wielderDef, out CardInstance card, out CardInstance wielder, Phase phase = Phase.Action)
    {
        var s = Table(phase, 0); Gold(s, 0);
        wielder = Add(s, wielderDef, 0, Zone.Home); Kw(wielder, CardCatalog.Get(sword).AttachRequiresKeyword!);
        card = Add(s, sword, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("equip", 0, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(0, CardCatalog.Get(sword).GoldCost).First().SourceIds, TargetId: wielder.Id));
        return e;
    }

    private static void SwordEntry(string ability, string wielderDef, string battleAbility)
    {
        var def = Def(ability);
        var e = Equipped(def, wielderDef, out var sword, out _);
        Eq(e.State.Cards[sword.Id].Zone, Zone.Attached, "equipped");
        Check(!On(e, 1, ability, sword.Id), "not to the other player");
        var honor = Honor(e, 0); var theirs = Honor(e, 1);
        Accept(e, Enabled(e, 0, ability, sword.Id).Command!);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor"); Eq(Honor(e, 1), theirs, "only for its controller");
        Check(!On(e, 0, ability, sword.Id), "spent");
        Quiet(def, ability);
        // The sword's Battle action works on its own in a battle.
        var b = Table(Phase.Battle, 0);
        var wielder = Add(b, wielderDef, 0, Zone.Battlefield, province: 0);
        var attached = Add(b, def, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var enemy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Check(!On(be, 0, ability, attached.Id), "the Reaction is not a Battle-phase action");
        Accept(be, battleAbility.EndsWith("create") ? Use(be, 0, battleAbility, attached.Id) : Offer(be, 0, battleAbility, attached.Id, enemy.Id));
    }

    private static void SwordLion() => SwordEntry("celestial-sword-lion-honor", "training-lion-samurai", "celestial-sword-lion-create");
    private static void SwordUnicorn() => SwordEntry("celestial-sword-unicorn-honor", "training-battle-maiden", "celestial-sword-unicorn-bow");

    private static void Yamadera()
    {
        var def = Def("p5-yamadera-refill");
        var (e, id) = Recruit(def);
        var refill = e.State.Players[0].Provinces[0].CardId!;
        Check(refill != id && !e.State.Cards[refill].FaceUp, "the ordinary refill is face-down");
        Check(!On(e, 1, "p5-yamadera-refill", id), "not to the other player");
        Accept(e, Enabled(e, 0, "p5-yamadera-refill", id).Command!);
        Check(e.State.Cards[refill].FaceUp && e.State.Cards[refill].Zone == Zone.Province, "refilled face-up");
        Check(!On(e, 0, "p5-yamadera-refill", id), "spent");
        Quiet(def, "p5-yamadera-refill");
        var b = Table(Phase.Battle, 0);
        var y = Add(b, def, 0, Zone.Battlefield, province: 0);
        var weak = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "p5-yamadera-bow", y.Id, weak.Id));
        Check(be.State.Cards[weak.Id].Bowed, "her Battle bow still works");
    }

    private static void Demiyah()
    {
        var def = Def("demiyah-gain");
        var (e, id) = Recruit(def);
        Check(!On(e, 1, "demiyah-gain", id), "not to the other player");
        var h1 = Honor(e, 1); var h0 = Honor(e, 0);
        var pick = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "demiyah-gain" && a.Command.TargetPlayerId == 1);
        Accept(e, pick.Command!);
        Eq(Honor(e, 1), h1 + 1, "the chosen player gained 1"); Eq(Honor(e, 0), h0, "and nobody else");
        Check(!On(e, 0, "demiyah-gain", id) && !On(e, 0, "demiyah-lose", id), "the pair is one use");
        Quiet(def, "demiyah-gain", "demiyah-lose");
    }

    private static void Ryoken()
    {
        var def = Def("ryoken-dishonor");
        CardInstance victim = null!;
        var (e, id) = Recruit(def, s => victim = Add(s, "training-lion-samurai", 1, Zone.Home));
        Check(!On(e, 1, "ryoken-dishonor", id), "not to the other player");
        Accept(e, Offer(e, 0, "ryoken-dishonor", id, victim.Id));
        Check(e.State.Cards[victim.Id].Dishonorable, "the target was dishonored");
        Check(!On(e, 0, "ryoken-dishonor", id), "spent");
        Quiet(def, "ryoken-dishonor", "ryoken-honor-loss");
    }

    private static void Torazu()
    {
        var def = Def("torazu-fire-tokens");
        var (e, id) = Recruit(def);
        Check(!On(e, 1, "torazu-fire-tokens", id), "not to the other player");
        Accept(e, Use(e, 0, "torazu-fire-tokens", id));
        Eq(e.State.Cards[id].Tokens.Count, 3, "three tokens");
        Check(e.State.Cards[id].Tokens.All(t => t.Type == "Fire" && t.Stat == "Force" && t.Amount == 1), "+1F Fire tokens");
        Check(!On(e, 0, "torazu-fire-tokens", id), "spent");
        Quiet(def, "torazu-fire-tokens");
        var b = Table(Phase.Battle, 0);
        var t = Add(b, def, 0, Zone.Battlefield, province: 0);
        t.Tokens.Add(new CardToken("tk-1", "Fire", "test", "Force", 1));
        var foe = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "torazu-fire-attack", t.Id, foe.Id));
    }

    private static void TrainingChoosePlayer()
    {
        const string def = "training-choose-player-personality";
        var (e, id) = Recruit(def);
        Check(!On(e, 1, "training-choose-player-gain", id), "not to the other player");
        var mine = Honor(e, 0);
        var opt = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == "training-choose-player-gain" && a.Command.TargetPlayerId == 0);
        Accept(e, opt.Command!);
        Eq(Honor(e, 0), mine + 1, "the chosen player gained 1 Honor");
        Check(!On(e, 0, "training-choose-player-gain", id), "spent");
        Quiet(def, "training-choose-player-gain", "training-choose-player-lose");
    }

    private static void Words()
    {
        var def = Def("words-of-consecration-honor");
        MatchEngine Attach(string shugenja, out CardInstance spell)
        {
            var s = Table(Phase.Action, 0); Gold(s, 0);
            var host = Add(s, shugenja, 0, Zone.Home);
            spell = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new("equip", 0, e.Version, CardId: spell.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds, TargetId: host.Id));
            return e;
        }
        var e = Attach("training-seeds5-air-shugenja", out var sp);
        Check(!On(e, 1, "words-of-consecration-honor", sp.Id), "not to the other player");
        var h1 = Honor(e, 1);
        Accept(e, Enabled(e, 0, "words-of-consecration-honor", sp.Id).Command!);
        Accept(e, new("chooseEffect", 0, e.Version, Choice: "opponent-lose"));
        Eq(Honor(e, 1), h1 - 1, "the chosen player lost 1 Honor");
        var n = Attach("training-seeds5-earth-alchemist", out var sp2);
        var attempt = n.Execute(Use(n, 0, "words-of-consecration-honor", sp2.Id));
        Check(!attempt.Accepted && (attempt.Error ?? "").Contains("must be Air"), "a Shugenja who is not Air: the trait is refused (" + attempt.Error + ")");
        Quiet(def, "words-of-consecration-honor");
    }

    // ---- targeted-by-action ----------------------------------------------------------------------------------------
    private static string Window(MatchEngine e) => e.State.PendingChoice?.Type ?? "";
    private static GameCommand Cmd(MatchEngine e, int p, string ability, string card) => Enabled(e, p, ability, card).Command!;
    private static void ToPlayer(MatchEngine e, int player) { for (var g = 0; g < 8 && e.State.PendingChoice is not null && e.State.PriorityPlayerId != player; g++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); }

    // Player 1 (the Attacker, acting) bows `target` with a Battle bower at the battlefield: the before-action window opens for the target's controller.
    private static MatchEngine BowedBy(MatchState s, CardInstance target)
    {
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, target.Id));
        return e;
    }

    private static void Armor()
    {
        var def = Def("armor-heavens-gain");
        var s = Table(Phase.Battle, 1);
        var wearer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var armor = Add(s, def, 0, Zone.Attached, province: 0, parent: wearer.Id);
        var e = BowedBy(s, wearer);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "armor-heavens-gain", armor.Id), "not to the acting player");
        var h = Honor(e, 0);
        Accept(e, e.QueryActions(0).Single(o => o.Enabled && o.Command?.AbilityId == "armor-heavens-gain" && o.Command.TargetPlayerId == 0).Command!);
        Eq(Honor(e, 0), h + 2, "the chosen player gained 2 Honor");
        Check(!On(e, 0, "armor-heavens-lose", armor.Id), "gain or lose is one use");
        Settle(e);
        // Another Personality targeted: no window for the armor.
        var t = Table(Phase.Battle, 1);
        var w2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var armor2 = Add(t, def, 0, Zone.Attached, province: 0, parent: w2.Id);
        var other = Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var e2 = BowedBy(t, other);
        Check(!On(e2, 0, "armor-heavens-gain", armor2.Id), "an action on another Personality does not open the armor's Reaction");
        Quiet(def, "armor-heavens-gain", "armor-heavens-lose");
    }

    private static void Accusation()
    {
        var def = Def("accusation-negate");
        var s = Table(Phase.Battle, 1);
        var card = Add(s, def, 0, Zone.Hand);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var other = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var src = Add(s, "training-slots-source", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-slots-two-political", src.Id, selections: [Pick("first", own.Id), Pick("second", other.Id)]));
        Check(!On(e, 1, "accusation-negate", card.Id), "not to the acting player");
        Accept(e, Cmd(e, 0, "accusation-negate", card.Id));
        Settle(e);
        Check(!e.State.Cards[own.Id].Bowed, "the Political action's effect on my Personality was negated");
        // A non-Political action on my Personality: not offered.
        var t = Table(Phase.Battle, 1); var c2 = Add(t, def, 0, Zone.Hand); var p2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = BowedBy(t, p2);
        Check(!On(e2, 0, "accusation-negate", c2.Id), "only a Political action");
        // The Limited half is independent: a Kolat target in the Action phase.
        var a = Table(Phase.Action, 0); var c3 = Add(a, def, 0, Zone.Hand); var kolat = Add(a, "training-lion-samurai", 1, Zone.Home); Kw(kolat, "Kolat");
        var ea = MatchEngine.FromTestState(a); var before = Honor(ea, 1);
        Accept(ea, Offer(ea, 0, "accusation-honor-loss", c3.Id, kolat.Id));
        Eq(Honor(ea, 1), before - CardCatalog.Get("training-lion-samurai").PersonalHonor, "his controller lost Honor equal to his printed Personal Honor");
        Quiet(def, "accusation-negate");
    }

    private static void Fudo()
    {
        var def = Def("promo-fudo-negate");
        MatchEngine Start(int tokens, out CardInstance fudo)
        {
            var s = Table(Phase.Battle, 1);
            fudo = Add(s, def, 0, Zone.Battlefield, province: 0);
            for (var i = 0; i < tokens; i++) fudo.Tokens.Add(new CardToken($"tk-{i}", "Charging", "test-fixture"));
            return BowedBy(s, fudo);
        }
        var e = Start(3, out var f);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "promo-fudo-negate", f.Id), "not to the acting player");
        Accept(e, Cmd(e, 0, "promo-fudo-negate", f.Id));
        Eq(e.State.Cards[f.Id].Tokens.Count, 2, "one Charging token was the cost");
        Settle(e);
        Check(!e.State.Cards[f.Id].Bowed, "the action's effects on him were negated");
        var n = Start(0, out var f0);
        Check(!On(n, 0, "promo-fudo-negate", f0.Id), "no Charging token, no Reaction");
        QuietWith(def, c => { for (var i = 0; i < 3; i++) c.Tokens.Add(new CardToken($"q-{i}", "Charging", "test-fixture")); }, "promo-fudo-negate");
        // His Limited and Battle actions work independently.
        var a = Table(Phase.Action, 0); var fa = Add(a, def, 0, Zone.Home); var ea = MatchEngine.FromTestState(a);
        Accept(ea, Use(ea, 0, "promo-fudo-charge", fa.Id));
        Eq(ea.State.Cards[fa.Id].Tokens.Count, 2, "Limited: two Charging tokens");
        var b = Table(Phase.Battle, 0); var fb = Add(b, def, 0, Zone.Battlefield, province: 0); var foe = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "promo-fudo-destroy", fb.Id, foe.Id));
        Check(eb.State.Cards[foe.Id].Zone != Zone.Battlefield, "Battle: the enemy unit was destroyed");
    }

    private static void Mistrust()
    {
        var def = Def("p5-inciting-mistrust-negate");
        MatchEngine Start(bool personalityProvince, out CardInstance card, out CardInstance ninja)
        {
            var s = Table(Phase.Battle, 1);
            card = Add(s, def, 0, Zone.Hand);
            ninja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Kw(ninja, "Ninja");
            var shooter = Add(s, "training-dual-attacker", 1, Zone.Battlefield, province: 0);
            s.Cards[s.Players[0].Provinces[0].CardId!].DefinitionId = personalityProvince ? "training-crane-samurai" : "training-marketplace";
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-honor-cost-attack", shooter.Id, ninja.Id));
            return e;
        }
        var e = Start(true, out var m, out var n);
        Eq(Window(e), "attackReaction", "the Attack is held");
        Check(!On(e, 1, "p5-inciting-mistrust-negate", m.Id), "not to the attacking player");
        ToPlayer(e, 0);
        Accept(e, Cmd(e, 0, "p5-inciting-mistrust-negate", m.Id));
        Settle(e);
        Eq(e.State.Cards[n.Id].Zone, Zone.Battlefield, "the province card is a Personality: the Attack was negated");
        var h = Start(false, out var m2, out var n2); ToPlayer(h, 0);
        Accept(h, Cmd(h, 0, "p5-inciting-mistrust-negate", m2.Id)); Settle(h);
        Check(h.State.Cards[n2.Id].Zone != Zone.Battlefield, "a non-Personality province card: the Attack destroys the Ninja");
        // Not for a Personality who is not a Ninja.
        var s3 = Table(Phase.Battle, 1); var m3 = Add(s3, def, 0, Zone.Hand); var plain = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var sh3 = Add(s3, "training-dual-attacker", 1, Zone.Battlefield, province: 0); var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-honor-cost-attack", sh3.Id, plain.Id)); ToPlayer(e3, 0);
        Check(!On(e3, 0, "p5-inciting-mistrust-negate", m3.Id), "only for a Ninja");
        // The Battle discard action works independently.
        var b = Table(Phase.Battle, 0); var m4 = Add(b, def, 0, Zone.Hand); Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b); var province = b.Players[1].Provinces[0].CardId!;
        Accept(eb, Cmd(eb, 0, "p5-inciting-mistrust-discard", m4.Id));
        Eq(eb.State.Cards[province].Zone, Zone.DynastyDiscard, "the battlefield's province card was discarded");
        Quiet(def, "p5-inciting-mistrust-negate");
    }

    private static void Norimichi()
    {
        var def = Def("norimichi-negate");
        MatchEngine Start(string shugenjaDef, out CardInstance nori, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1);
            nori = Add(s, def, 0, Zone.Battlefield, province: 0);
            victim = shugenjaDef == "" ? nori : Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);
            var d = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-destroy-no-attachments", d.Id, victim.Id));
            return e;
        }
        var e = Start("", out var n, out _);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "norimichi-negate", n.Id), "not to the acting player");
        Accept(e, Cmd(e, 0, "norimichi-negate", n.Id)); Settle(e);
        Eq(e.State.Cards[n.Id].Zone, Zone.Battlefield, "his destruction was negated");
        var e2 = Start("training-seeds5-earth-alchemist", out var n2, out var kusugi);
        Accept(e2, Cmd(e2, 0, "norimichi-negate", n2.Id)); Settle(e2);
        Eq(e2.State.Cards[kusugi.Id].Zone, Zone.Battlefield, "an Earth Shugenja at his location survived");
        var e3 = Start("training-crane-samurai", out var n3, out var samurai);
        Check(!On(e3, 0, "norimichi-negate", n3.Id), "a Samurai is not an Earth Shugenja"); Settle(e3);
        Eq(e3.State.Cards[samurai.Id].Zone, Zone.Dead, "so he is destroyed");
        var b = Table(Phase.Battle, 0); var nb = Add(b, def, 0, Zone.Battlefield, province: 0); var eb = MatchEngine.FromTestState(b);
        Accept(eb, Use(eb, 0, "norimichi-army", nb.Id));
        Quiet(def, "norimichi-negate");
    }

    private static void KnowNoFear()
    {
        var def = Def("know-no-fear-negate-fear");
        var s = Table(Phase.Action, 1);
        var hotako = Add(s, Def("hotako-fear-lock"), 1, Zone.Home);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home); Kw(samurai, "Samurai");
        var know = Add(s, def, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "hotako-fear-lock", hotako.Id, samurai.Id));
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "know-no-fear-negate-fear", know.Id), "not to the acting player");
        Accept(e, Cmd(e, 0, "know-no-fear-negate-fear", know.Id)); Settle(e);
        Check(e.State.Cards[samurai.Id].ActionLocks.Count == 0, "the Fear action was negated: no lock");
        Eq(e.State.Cards[know.Id].Zone, Zone.FateDiscard, "spent");
        var t = Table(Phase.Action, 1); var h2 = Add(t, Def("hotako-fear-lock"), 1, Zone.Home); var plain = Add(t, "training-crane-samurai", 0, Zone.Home); var k2 = Add(t, def, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, "hotako-fear-lock", h2.Id, plain.Id));
        Check(!On(e2, 0, "know-no-fear-negate-fear", k2.Id), "a Personality who is not a Samurai is not protected");
        Quiet(def, "know-no-fear-negate-fear", "know-no-fear-negate-move");
    }

    private static void PowerWord()
    {
        var def = Def("power-word-reaction");
        MatchEngine Start(out CardInstance word, out CardInstance courtier)
        {
            var s = Table(Phase.Battle, 1);
            var law = Add(s, Def("laws-strength-move"), 1, Zone.Hand);
            var actor = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Kw(actor, "Courtier");
            courtier = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0); Kw(courtier, "Courtier");
            word = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "laws-strength-move", law.Id, courtier.Id, actor.Id));
            return e;
        }
        var e = Start(out var w, out var c);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "power-word-reaction", w.Id), "not to the acting player");
        var honor = Honor(e, 1);
        Accept(e, Cmd(e, 0, "power-word-reaction", w.Id));
        Eq(Window(e), "eitherOr", "the acting player is asked"); Eq(e.State.PendingChoice!.PlayerId, 1, "he decides");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "negate").Command!);
        Settle(e);
        Eq(Honor(e, 1), honor, "no Honor lost"); Eq(e.State.Cards[c.Id].Zone, Zone.Battlefield, "the action was negated: the Courtier stayed");
        var f = Start(out var w2, out var c2);
        Accept(f, Cmd(f, 0, "power-word-reaction", w2.Id));
        Accept(f, f.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "lose").Command!); Settle(f);
        Eq(Honor(f, 1), honor - 3, "he chose to lose 3 Honor"); Eq(f.State.Cards[c2.Id].Zone, Zone.Home, "and the action went ahead");
        // The Battle move works independently.
        var b = Table(Phase.Battle, 0); var wb = Add(b, def, 0, Zone.Hand); Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var dis = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); dis.Dishonorable = true;
        var eb = MatchEngine.FromTestState(b);
        Check(On(eb, 0, "power-word-move", wb.Id), "the Battle move is on offer");
        Quiet(def, "power-word-reaction");
    }

    private static void Tanimura()
    {
        var def = Def("tanimura-political-pay");
        var s = Table(Phase.Battle, 1);
        var tan = Add(s, def, 0, Zone.Battlefield, province: 0);
        var e = BowedBy(s, tan);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "tanimura-political-pay", tan.Id), "not to the acting player");
        Accept(e, Cmd(e, 0, "tanimura-political-pay", tan.Id));
        Eq(Window(e), "eitherOr", "the acting player chooses"); Eq(e.State.PendingChoice!.PlayerId, 1, "the player who took the action decides");
        var honor = Honor(e, 1);
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == "lose").Command!);
        Eq(Honor(e, 1), honor - 3, "he lost 3 Honor");
        Settle(e);
        Quiet(def, "tanimura-political-pay");
    }

    private static void ReverseFate()
    {
        var def = Def("w737-reverse-fate-minus");
        MatchEngine Start(string[] hostKeywords, out CardInstance spell)
        {
            var s = Table(Phase.Battle, 1);
            var fires = Add(s, Def("fires-of-turmoil"), 1, Zone.Hand);
            var victim = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Kw(host, hostKeywords);
            spell = Add(s, def, 0, Zone.Attached, province: 0, parent: host.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "fires-of-turmoil", fires.Id, victim.Id));
            return e;
        }
        var e = Start(["Shugenja", "Void"], out var sp);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!On(e, 1, "w737-reverse-fate-minus", sp.Id), "not to the acting player");
        var topId = e.State.Players[1].FateDeck[0]; var printed = CardCatalog.Get(e.State.Cards[topId].DefinitionId).Focus;
        Accept(e, Cmd(e, 0, "w737-reverse-fate-minus", sp.Id));
        Check(e.State.Cards[sp.Id].Bowed, "the Spell bowed as the cost");
        Check(!On(e, 0, "w737-reverse-fate-plus", sp.Id), "plus and minus are one use");
        Settle(e);
        Eq(e.State.Cards[topId].Zone, Zone.FateDiscard, "the top card was discarded");
        Eq(e.FocusValueForTest(topId), Math.Max(0, printed - 1), "its Focus Value is 1 lower");
        var n = Start(["Shugenja", "Water"], out var sp2);
        Check(!On(n, 0, "w737-reverse-fate-minus", sp2.Id), "not a Void Shugenja");
        Quiet(def, "w737-reverse-fate-minus", "w737-reverse-fate-plus");
    }

    private static void NearMiss()
    {
        var def = Def("pay-near-miss-negate");
        MatchEngine Start(out CardInstance follower, out CardInstance miss)
        {
            var s = Table(Phase.Battle, 1);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: victim.Id);
            var never = Add(s, Def("never-afraid"), 1, Zone.Hand); miss = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "never-afraid", never.Id, follower.Id));
            return e;
        }
        var e = Start(out var f, out var m);
        Check(!On(e, 1, "pay-near-miss-negate", m.Id), "not to the acting player");
        Accept(e, Cmd(e, 0, "pay-near-miss-negate", m.Id));
        Eq(e.State.Cards[m.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        Settle(e);
        Eq(e.State.Cards[f.Id].Zone, Zone.Attached, "the Follower's destruction was negated");
        var s2 = Table(Phase.Battle, 1); var v2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var m2 = Add(s2, def, 0, Zone.Hand);
        var e2 = BowedBy(s2, v2);
        Check(!On(e2, 0, "pay-near-miss-negate", m2.Id), "an action on a Personality is not an action on an attachment");
        Quiet(def, "pay-near-miss-negate");
    }

    private static void WellProtected()
    {
        var def = Def("pe-well-protected-bow");
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var weapon = Add(s, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: wielder.Id);
        var victim = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0); var armor = Add(s, "training-armor", 0, Zone.Attached, province: 0, parent: victim.Id);
        var card = Add(s, def, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-weapon-attack", weapon.Id, victim.Id));
        Eq(Window(e), "attackReaction", "the Attack is held");
        Check(!On(e, 1, "pe-well-protected-bow", card.Id), "not to the acting player");
        ToPlayer(e, 0);
        Accept(e, Cmd(e, 0, "pe-well-protected-bow", card.Id));
        Check(e.State.Cards[armor.Id].Bowed && e.State.Cards[armor.Id].Zone == Zone.Attached, "the Armor was bowed");
        Settle(e);
        Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "his destruction was negated");
        var n = Table(Phase.Battle, 1); var nw = Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var nweapon = Add(n, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: nw.Id);
        var bare = Add(n, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0); var nc = Add(n, def, 0, Zone.Hand);
        var ne = MatchEngine.FromTestState(n); Accept(ne, Offer(ne, 1, "training-weapon-attack", nweapon.Id, bare.Id)); ToPlayer(ne, 0);
        Check(!On(ne, 0, "pe-well-protected-bow", nc.Id), "no Armor: not offered");
        Quiet(def, "pe-well-protected-bow", "pe-well-protected-destroy");
    }

    // ---- shared battle helpers --------------------------------------------------------------------------------
    private static void Strong(MatchState s) { foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; }
    private static MatchState Sel() { var s = Table(Phase.BattleSelection, 0); Strong(s); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0]; return s; }
    private static void Engage(MatchEngine e) { Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0)); Eq(Window(e), "engageReaction", "the Engage window opens"); }
    private static void PassTo(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }.SelectMany(p => e.QueryActions(p)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({Window(e)})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the position was not reached");
    }
    private static GameCommand Raw(MatchEngine e, int player, string ability, string card, string? target = null) => new("useAbility", player, e.Version, CardId: card, AbilityId: ability, TargetId: target);

    // ---- before-attack-resolves ---------------------------------------------------------------------------------
    // `shooterOwner` takes a Ranged (or Melee) Attack with the dual attacker against a Personality of the other player; the Attack waits in its window.
    private static MatchEngine Shot(int shooterOwner, bool melee, Action<MatchState, CardInstance>? arrange, out CardInstance prey)
    {
        var s = Table(Phase.Battle, shooterOwner);
        var shooter = Add(s, "training-dual-attacker", shooterOwner, Zone.Battlefield, province: 0);
        prey = Add(s, "training-crane-samurai", 1 - shooterOwner, Zone.Battlefield, province: 0);
        arrange?.Invoke(s, prey);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, shooterOwner, melee ? "training-weapon-attack" : "training-honor-cost-attack", shooter.Id, prey.Id));
        Eq(Window(e), "attackReaction", "the Attack waits for reactions");
        return e;
    }

    private static void Fumiko()
    {
        var def = Def("fumiko-attack-boost");
        CardInstance fumiko = null!;
        var e = Shot(0, false, (s, _) => { fumiko = Add(s, def, 0, Zone.Battlefield, province: 0); Add(s, "training-attack-reactor", 0, Zone.Battlefield, province: 0); }, out _);
        ToPlayer(e, 0);
        Check(!On(e, 1, "fumiko-attack-boost", fumiko.Id), "not to the other player");
        var honor = Honor(e, 0); var strength = e.State.PendingAttack!.Strength;
        Accept(e, Cmd(e, 0, "fumiko-attack-boost", fumiko.Id));
        Eq(Honor(e, 0), honor + 1, "gained 1 Honor"); Eq(e.State.PendingAttack?.Strength, strength + 2, "+2 strength on the held Attack");
        Settle(e);
        var m = Shot(0, true, (s, _) => fumiko = Add(s, def, 0, Zone.Battlefield, province: 0), out _); ToPlayer(m, 0);
        Check(!On(m, 0, "fumiko-attack-boost", fumiko.Id), "not for a Melee Attack");
        var o = Shot(1, false, (s, _) => fumiko = Add(s, def, 0, Zone.Battlefield, province: 0), out _); ToPlayer(o, 0);
        Check(!On(o, 0, "fumiko-attack-boost", fumiko.Id), "not for the other player's Ranged Attack");
        Quiet(def, "fumiko-attack-boost");
        var b = Table(Phase.Battle, 0); var f = Add(b, def, 0, Zone.Battlefield, province: 0); var weak = Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Force(weak, -3);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "fumiko-bow-raise", f.Id, weak.Id));
        Check(eb.State.Cards[weak.Id].Bowed, "Battle: the weak enemy card was bowed");
    }

    private static void Umiko()
    {
        var def = Def("umiko-attack-reduce");
        CardInstance umiko = null!;
        var e = Shot(1, false, (s, _) => { umiko = Add(s, def, 0, Zone.Battlefield, province: 0); Add(s, "training-attack-reactor", 0, Zone.Battlefield, province: 0); }, out _);
        ToPlayer(e, 0);
        Check(!On(e, 1, "umiko-attack-reduce", umiko.Id), "not to the other player");
        var strength = e.State.PendingAttack!.Strength;
        Accept(e, Cmd(e, 0, "umiko-attack-reduce", umiko.Id));
        Eq(e.State.PendingAttack?.Strength, strength - 2, "-2 strength");
        Check(!On(e, 0, "umiko-attack-boost", umiko.Id) && !On(e, 0, "umiko-attack-reduce", umiko.Id), "one use covers both");
        Settle(e);
        var m = Shot(1, true, (s, _) => umiko = Add(s, def, 0, Zone.Battlefield, province: 0), out _); ToPlayer(m, 0);
        Check(!On(m, 0, "umiko-attack-reduce", umiko.Id), "not for a Melee Attack");
        Quiet(def, "umiko-attack-reduce", "umiko-attack-boost");
    }

    private static void Precision()
    {
        var def = Def("precision-reduce");
        foreach (var melee in new[] { false, true })
        {
            CardInstance card = null!;
            var e = Shot(1, melee, (s, _) => { card = Add(s, def, 0, Zone.Hand); Add(s, "training-attack-reactor", 0, Zone.Home); }, out _);
            ToPlayer(e, 0);
            Check(!On(e, 1, "precision-reduce", card.Id), "not to the other player");
            Check(On(e, 0, "precision-reduce", card.Id), $"offered from the hand in a {(melee ? "Melee" : "Ranged")} Attack window");
            var strength = e.State.PendingAttack!.Strength;
            Accept(e, Raw(e, 0, "precision-reduce", card.Id));
            Eq(e.State.PendingAttack!.Strength, strength - 2, "-2 strength");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        }
        Quiet(def, "precision-reduce", "precision-boost");
        var s2 = Table(Phase.Battle, 0); var atk = Add(s2, def, 0, Zone.Hand); Add(s2, def, 0, Zone.Hand); /* issue #917: a second copy -- the card being played is in the resolution area and cannot react to its own Attack */ Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Raw(e2, 0, "precision-attack", atk.Id, enemy.Id));
        Eq(Window(e2), "attackReaction", "the Battle half still opens its own Attack");
    }

    private static void UnholyStrike()
    {
        var def = Def("unholy-strike-boost");
        var s = Table(Phase.Battle, 0);
        var attack = Add(s, def, 0, Zone.Hand); var interrupt = Add(s, def, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-attack-reactor", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!On(e, 0, "unholy-strike-boost", interrupt.Id), "no Attack pending: not offered");
        Accept(e, Raw(e, 0, "unholy-strike-attack", attack.Id, enemy.Id));
        Eq(Window(e), "attackReaction", "the Attack waits");
        Check(!On(e, 1, "unholy-strike-boost", interrupt.Id), "not to the other player");
        Check(On(e, 0, "unholy-strike-boost", interrupt.Id), "offered from the hand");
        var strength = e.State.PendingAttack!.Strength;
        Accept(e, Raw(e, 0, "unholy-strike-boost", interrupt.Id));
        Eq(e.State.PendingAttack!.Strength, strength + 2, "+2 strength");
        // The opponent's Attack, answered by my Interrupt.
        var t = Table(Phase.Battle, 1); var mine = Add(t, def, 0, Zone.Hand); Add(t, "training-attack-reactor", 0, Zone.Home);
        var shooter = Add(t, "training-dual-attacker", 1, Zone.Battlefield, province: 0); var prey = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var et = MatchEngine.FromTestState(t);
        Accept(et, Offer(et, 1, "training-weapon-attack", shooter.Id, prey.Id)); ToPlayer(et, 0);
        var before = et.State.PendingAttack!.Strength;
        Accept(et, Raw(et, 0, "unholy-strike-boost", mine.Id));
        Eq(et.State.PendingAttack!.Strength, before + 2, "an enemy Attack may be boosted too");
        Quiet(def, "unholy-strike-boost");
    }

    // ---- before-action-resolves ---------------------------------------------------------------------------------
    private static void EarthsStrength()
    {
        var def = Def("earths-strength-negate-move");
        MatchEngine Start(bool attackerMovesOwn, out CardInstance strength, out CardInstance defender)
        {
            var t = Table(Phase.Battle, 1);
            defender = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var shugenja = Add(t, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
            strength = Add(t, def, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            var attacker = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var moksha = Add(t, Def("moksha-patam-move"), 1, Zone.Attached, province: 0, parent: attacker.Id);
            var ally = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(t);
            Accept(e, Offer(e, 1, "moksha-patam-move", moksha.Id, attackerMovesOwn ? ally.Id : defender.Id));
            return e;
        }
        var e = Start(false, out var sp, out var d);
        Eq(Window(e), "beforeActionReaction", "the window opens");
        Check(!e.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "earths-strength-negate-move"), "not to the acting player");
        var reaction = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "earths-strength-negate-move" && a.Command.TargetId == d.Id);
        Check(reaction is not null, "the defending Personality the action would move is offered");
        Accept(e, reaction!.Command!); Settle(e);
        Eq(e.State.Cards[d.Id].Zone, Zone.Battlefield, "his movement was negated"); Check(e.State.Cards[sp.Id].Bowed, "the Spell bowed as the cost");
        var o = Start(true, out var sp2, out _);
        Check(!o.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "earths-strength-negate-move"), "an attacking Personality is not a defending one"); Settle(o);
        var b = Table(Phase.Battle, 0); var alch = Add(b, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0, bowed: true); var spell = Add(b, def, 0, Zone.Attached, province: 0, parent: alch.Id);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "earths-strength-straighten", spell.Id, alch.Id));
        Check(!eb.State.Cards[alch.Id].Bowed && eb.State.Cards[spell.Id].Bowed, "Battle: straightened; the Spell bowed as its cost");
        Quiet(def, "earths-strength-negate-move");
    }

    private static void HeightOfCourage()
    {
        var def = Def("height-courage-negate");
        MatchEngine Start(out CardInstance card, out CardInstance defender)
        {
            var s = Table(Phase.Battle, 1);
            card = Add(s, def, 0, Zone.Hand); defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var inc = Add(s, Def("incapacitated"), 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "incapacitated", inc.Id, defender.Id));
            return e;
        }
        var e = Start(out var c, out var d);
        Eq(Window(e), "beforeActionReaction", "the movement is offered for negation");
        Check(!On(e, 1, "height-courage-negate", c.Id), "not to the acting player");
        var offers = Options(e, 0, "height-courage-negate", c.Id).Where(a => a.Enabled).ToArray();
        Check(offers.Length == 1 && offers[0].Command!.PerformerId == d.Id, "one option: the Personality the action would move");
        Accept(e, offers[0].Command!); Settle(e);
        Eq(e.State.Cards[d.Id].Zone, Zone.Battlefield, "his movement was negated"); Eq(e.State.Cards[c.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var f = Start(out _, out var moved); Settle(f);
        Eq(f.State.Cards[moved.Id].Zone, Zone.Home, "declined: he moved home");
        var b = Table(Phase.Battle, 0); var card2 = Add(b, def, 0, Zone.Hand); var lead = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Use(eb, 0, "height-courage-boost", card2.Id, null, lead.Id));
        Eq(eb.State.Cards[card2.Id].Zone, Zone.FateDiscard, "the Battle boost still works");
        Quiet(def, "height-courage-negate");
    }

    // ---- after-action-resolved ----------------------------------------------------------------------------------
    private static void ClosingTheGap()
    {
        var def = Def("pe-closing-the-gap-home");
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, "training-seeds5-air-shugenja", 1, Zone.Battlefield, province: 0); var spell = Add(s, Def("tf-typhoon-surge-move"), 1, Zone.Attached, province: 0, parent: attacker.Id);
        var atBattle = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); var moved = Add(s, "training-crane-veteran", 0, Zone.Home);
        var card = Add(s, def, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!On(e, 0, "pe-closing-the-gap-home", card.Id), "nothing resolved yet");
        Accept(e, Offer(e, 1, "tf-typhoon-surge-move", spell.Id, moved.Id));
        Eq(Window(e), "afterActionResolvedReaction", "the action moved a Personality to the battlefield");
        Check(!On(e, 1, "pe-closing-the-gap-home", card.Id), "not to the other player");
        Accept(e, Use(e, 0, "pe-closing-the-gap-home", card.Id, null, atBattle.Id));
        Eq(e.State.Cards[atBattle.Id].Zone, Zone.Home, "he went home"); Check(!e.State.Cards[atBattle.Id].Bowed, "and his unit was straightened");
        Check(!On(e, 0, "pe-closing-the-gap-to-battle", card.Id), "one use");
        Quiet(def, "pe-closing-the-gap-home", "pe-closing-the-gap-to-battle");
    }

    private static void Akeha()
    {
        var def = Def("akeha-recon-draw");
        MatchEngine Start(int burnOwner, out CardInstance akeha, out CardInstance burn)
        {
            var s = Sel();
            akeha = Add(s, def, 0, Zone.Battlefield, province: 0);
            var scout = burnOwner == 0 ? akeha : Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (burnOwner == 1) Kw(scout, "Scout"); else Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            burn = Add(s, Def("burn-towers-recon"), burnOwner, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Start(0, out var a, out var burn);
        Engage(e);
        Check(!On(e, 0, "akeha-recon-draw", a.Id), "nothing resolved yet");
        Accept(e, Enabled(e, 0, "burn-towers-recon", burn.Id, a.Id).Command!);
        Eq(Window(e), "afterActionResolvedReaction", "the Recon Strategy's resolution opens the window");
        Check(!On(e, 1, "akeha-recon-draw", a.Id), "not to the other player");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Cmd(e, 0, "akeha-recon-draw", a.Id));
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Akeha drew a card");
        Quiet(def, "akeha-recon-draw");
    }

    private static void LyingInAmbush()
    {
        var def = Def("pf-lying-in-ambush-battlefield");
        var s = Table(Phase.Battle, 0);
        var dak = Add(s, Def("dak-ho-create"), 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var card = Add(s, def, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!On(e, 0, "pf-lying-in-ambush-battlefield", card.Id), "nothing was created yet");
        Accept(e, Enabled(e, 0, "dak-ho-create", dak.Id).Command!);
        Check(On(e, 0, "pf-lying-in-ambush-battlefield", card.Id) && !On(e, 0, "pf-lying-in-ambush-home", card.Id), "created at the battlefield: only the battlefield half");
        Check(!On(e, 1, "pf-lying-in-ambush-battlefield", card.Id), "not to the other player");
        var before = e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Accept(e, Cmd(e, 0, "pf-lying-in-ambush-battlefield", card.Id));
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield), before + 1, "an Ambusher joined my army at the battlefield");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        Quiet(def, "pf-lying-in-ambush-battlefield", "pf-lying-in-ambush-home");
    }

    private static void Tsukimi()
    {
        var def = Def("tsukimi-home");
        var s = Table(Phase.Battle, 1);
        var tsu = Add(s, def, 0, Zone.Battlefield, province: 0); var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = BowedBy(s, victim);
        Eq(Window(e), "afterActionResolvedReaction", "the window opens after another player's Battle action");
        Check(!On(e, 1, "tsukimi-home", tsu.Id), "not to the other player");
        Accept(e, Cmd(e, 0, "tsukimi-home", tsu.Id)); Settle(e);
        Eq(e.State.Cards[tsu.Id].Zone, Zone.Home, "moved home");
        var own = Table(Phase.Battle, 0); var t2 = Add(own, def, 0, Zone.Battlefield, province: 0); var bower = Add(own, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var foe = Add(own, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var eo = MatchEngine.FromTestState(own);
        Accept(eo, Offer(eo, 0, "training-bow-at-battle", bower.Id, foe.Id));
        Check(!On(eo, 0, "tsukimi-home", t2.Id), "only another player's Battle action");
        Settle(eo);
        var a = Table(Phase.Battle, 0); var t3 = Add(a, def, 0, Zone.Battlefield, province: 0); var enemy = Add(a, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var ea = MatchEngine.FromTestState(a);
        Accept(ea, Offer(ea, 0, "tsukimi-attack", t3.Id, enemy.Id)); Settle(ea);
        Check(ea.State.Cards[enemy.Id].Zone != Zone.Battlefield, "her Melee attack is unaffected: it destroyed the enemy");
        Quiet(def, "tsukimi-home", "tsukimi-battlefield");
    }

    private static void Hawado()
    {
        var def = Def("hawado-exp-move-home");
        MatchEngine Start(Zone zone, out CardInstance hawado, out CardInstance performer)
        {
            var s = Table(Phase.Battle, 1); s.UnresolvedBattlefields = [0, 1];
            hawado = Add(s, def, 0, zone, province: zone == Zone.Battlefield ? 0 : null);
            var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            performer = Add(s, "training-force-seven-bower", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-bow-at-battle", performer.Id, victim.Id));
            return e;
        }
        var e = Start(Zone.Battlefield, out var h, out var p);
        Eq(Window(e), "battleActionReaction", "the after-Battle-action window opens");
        Check(!On(e, 1, "hawado-exp-move-home", h.Id), "not to the other player");
        Accept(e, Cmd(e, 0, "hawado-exp-move-home", h.Id));
        Eq(e.State.Cards[p.Id].Zone, Zone.Home, "the performer moved home");
        var away = Start(Zone.Home, out var h2, out _);
        Check(!On(away, 0, "hawado-exp-move-home", h2.Id), "Hawado at home is not at the current battlefield: listed disabled, as the engine would refuse it");
        var refused = away.Execute(Use(away, 0, "hawado-exp-move-home", h2.Id));
        Check(!refused.Accepted, "and the engine does refuse it");
        Quiet(def, "hawado-exp-move-home", "hawado-exp-move-battlefield");
    }

    // ---- before-honor-change ------------------------------------------------------------------------------------
    private static void DestructivePriorities()
    {
        var def = Def("destructive-priorities-reaction");
        MatchEngine Start(out CardInstance kata, out CardInstance courtier)
        {
            var s = Table(Phase.Action, 0);
            kata = Add(s, def, 0, Zone.Home);
            courtier = Add(s, "training-crane-courtier", 0, Zone.Home); Kw(courtier, "Courtier");
            var ancestor = Add(s, "training-crane-samurai", 0, Zone.Home); Kw(ancestor, "Ancestor");
            var forefather = Add(s, Def("forefather-honor"), 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, "forefather-honor", forefather.Id).Command!);
            return e;
        }
        var e = Start(out var k, out var c);
        Eq(Window(e), "honorChangeReaction", "before he gains Honor: the window");
        Check(!On(e, 1, "destructive-priorities-reaction", k.Id), "not to the other player");
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Options(e, 0, "destructive-priorities-reaction", k.Id).First(o => o.Enabled && o.Command!.TargetPlayerId == 1).Command!);
        PassTo(e, () => e.State.PendingChoice is null);
        Eq(Honor(e, 0), h0, "the gain was negated"); Eq(Honor(e, 1), h1 - 2, "the chosen player lost 2 Honor"); Check(e.State.Cards[c.Id].Bowed, "the Courtier bowed as the cost");
        // No gain pending: not offered (the Kata sits in play).
        var q = Table(Phase.Action, 0); var kq = Add(q, def, 0, Zone.Home); var cq = Add(q, "training-crane-courtier", 0, Zone.Home); Kw(cq, "Courtier");
        var eq = MatchEngine.FromTestState(q);
        Check(!On(eq, 0, "destructive-priorities-reaction", kq.Id), "nothing pending");
        // The Kata itself is still played from the hand.
        var h = Table(Phase.Action, 0); var inHand = Add(h, def, 0, Zone.Hand); var eh = MatchEngine.FromTestState(h);
        Accept(eh, Enabled(eh, 0, "destructive-priorities-kata", inHand.Id).Command!);
        Eq(eh.State.Cards[inHand.Id].Zone, Zone.Home, "the Kata is in play");
        Quiet(def, "destructive-priorities-reaction");
    }

    private static void SolidDefense()
    {
        var def = Def("solid-defense-negate-favor");
        MatchEngine Start(int force, bool favor, out CardInstance card)
        {
            var s = Table(Phase.Battle, 1);
            card = Add(s, def, 0, Zone.Hand);
            var lead = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(lead, force - 3);
            var adversity = Add(s, Def("overcome-adversity"), 1, Zone.Hand);
            var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Kw(lion, "Paragon");
            if (favor) s.FavorPlayerId = 0;
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "overcome-adversity", adversity.Id, lead.Id, lion.Id));
            return e;
        }
        var f = Start(4, true, out var card);
        Eq(Window(f), "honorChangeReaction", "the loss is offered for negation");
        Check(!On(f, 1, "solid-defense-negate-favor", card.Id), "not to the acting player");
        var honor = Honor(f, 1);
        Accept(f, Cmd(f, 0, "solid-defense-negate-favor", card.Id));
        PassTo(f, () => f.State.PendingChoice is null);
        Eq(Honor(f, 1), honor, "the Honor loss was negated"); Eq(f.State.FavorPlayerId, (int?)null, "the Imperial Favor was discarded");
        var g = Start(4, false, out var card2);
        Check(!On(g, 0, "solid-defense-negate-favor", card2.Id), "no Imperial Favor to discard: not offered");
        var b = Table(Phase.Battle, 0); var half = Add(b, def, 0, Zone.Hand); Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var bare = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "solid-defense-move", half.Id, bare.Id));
        Eq(eb.State.Cards[bare.Id].Zone, Zone.Home, "the Battle move still works");
        Quiet(def, "solid-defense-negate-favor", "solid-defense-negate");
    }

    // ---- paying-gold-cost ---------------------------------------------------------------------------------------
    private static MatchState FarmBuy(string provinceCard, Action<MatchState>? edit = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var c = s.Cards[s.Players[0].Provinces[0].CardId!]; c.DefinitionId = provinceCard; c.FaceUp = true;
        edit?.Invoke(s);
        return s;
    }

    private static void Kenro()
    {
        var def = Def("pe-kenro-ji-jins-bounty-gold");
        var s = FarmBuy("training-farm"); var kenro = Add(s, def, 0, Zone.Celestial);
        var farmId = s.Players[0].Provinces[0].CardId!;
        var e = MatchEngine.FromTestState(s);
        var quote = e.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == farmId).Gold!;
        Check(quote.Sources!.Any(m => m.Id == kenro.Id + "#reaction" && m.Produced == 1 && m.Kind == "reaction"), "while paying for a Farm it is a Gold source of 1");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: farmId, SourceIds: [e.State.Players[0].StrongholdId, kenro.Id + "#reaction"]));
        Eq(e.State.Cards[farmId].Zone, Zone.Home, "the Farm entered play");
        Check(!e.State.Cards[kenro.Id].Bowed && e.State.Cards[kenro.Id].AbilityUses.Count > 0, "not bowed, but its Reaction is spent for the turn");
        var t = FarmBuy("training-marketplace"); var k2 = Add(t, def, 0, Zone.Celestial); var te = MatchEngine.FromTestState(t);
        Check(!te.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == t.Players[0].Provinces[0].CardId).Gold!.Sources!.Any(m => m.Id == k2.Id + "#reaction"), "not while paying for a Holding that is not a Farm");
        var u = FarmBuy("training-farm"); var k3 = Add(u, def, 1, Zone.Celestial); var ue = MatchEngine.FromTestState(u);
        Check(!ue.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == u.Players[0].Provinces[0].CardId).Gold!.Sources!.Any(m => m.Id == k3.Id + "#reaction"), "another player's Celestial is not my source");
        var a = Table(Phase.Action, 0); var cel = Add(a, def, 0, Zone.Celestial); var ea = MatchEngine.FromTestState(a);
        Check(!ea.QueryActions(0).Any(o => o.Type == "useAbility" && o.Command?.AbilityId == "pe-kenro-ji-jins-bounty-gold"), "never a window action in an ordinary Action phase");
        Accept(ea, Enabled(ea, 0, "pe-kenro-ji-jins-bounty-farms", cel.Id).Command!);
        Eq(ea.State.Cards[cel.Id].Zone, Zone.DynastyDiscard, "the Limited half still works: discarded as its cost");
    }

    private static void Sakuma()
    {
        var def = Def("sakuma-produce-gold");
        MatchState Poor(int sakumaOwner, bool bowed)
        {
            var s = FarmBuy("training-farm");
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
            var sak = Add(s, def, sakumaOwner, Zone.Home); sak.Bowed = bowed;
            return s;
        }
        var s = Poor(0, false); var sakuma = s.Cards.Values.Single(c => c.DefinitionId == def);
        var farmId = s.Players[0].Provinces[0].CardId!;
        var e = MatchEngine.FromTestState(s);
        var source = e.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == farmId).Gold!.Sources!.SingleOrDefault(m => m.Id.StartsWith(sakuma.Id));
        Check(source is not null && source.Produced == 2, "while paying, Sakuma is a 2 Gold source");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: farmId, SourceIds: [source!.Id]));
        Eq(e.State.Cards[farmId].Zone, Zone.Home, "the Farm entered play"); Check(e.State.Cards[sakuma.Id].Bowed, "Sakuma bowed to produce");
        foreach (var (owner, bowed, why) in new[] { (0, true, "a bowed Sakuma"), (1, false, "the other player's Sakuma") })
        {
            var p = Poor(owner, bowed); var sk = p.Cards.Values.Single(c => c.DefinitionId == def); var pe = MatchEngine.FromTestState(p);
            Check(!pe.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == p.Players[0].Provinces[0].CardId).Gold!.Sources!.Any(m => m.Id.StartsWith(sk.Id) && m.Produced > 0), $"{why} is no source");
        }
        Quiet(def, "sakuma-produce-gold");
    }

    // ---- after-engaging -----------------------------------------------------------------------------------------
    private static void Qalyar()
    {
        var def = Def("qalyar-recon-plus");
        MatchEngine Start(Zone zone, out CardInstance q)
        {
            var s = Sel();
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            q = Add(s, def, 0, zone, province: zone == Zone.Battlefield ? 0 : null);
            var e = MatchEngine.FromTestState(s); if (zone == Zone.Battlefield) Engage(e); else Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0)); return e;
        }
        var e = Start(Zone.Battlefield, out var qal);
        Check(!On(e, 1, "qalyar-recon-plus", qal.Id), "not to the other player");
        var before = e.GetView(0).Players[1].Provinces[0].Strength;
        Accept(e, Use(e, 0, "qalyar-recon-plus", qal.Id));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance at the battlefield");
        Eq(e.GetView(0).Players[1].Provinces[0].Strength, before + 2, "+2 strength");
        Check(!On(e, 0, "qalyar-recon-plus", qal.Id) && !On(e, 0, "qalyar-recon-minus", qal.Id), "one use");
        var away = Start(Zone.Home, out var q2);
        Check(Window(away) != "engageReaction" && !On(away, 0, "qalyar-recon-plus", q2.Id), "a Qalyar at home cannot react: no Engage window");
        Quiet(def, "qalyar-recon-plus", "qalyar-recon-minus");
    }

    private static void Stockpiled()
    {
        var def = Def("stockpiled-attach-commander");
        MatchEngine Start(bool commander, out CardInstance card, out CardInstance item, out CardInstance actor)
        {
            var t = Sel(); for (var i = 0; i < 4; i++) Add(t, "training-marketplace", 0, Zone.Home);
            actor = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); if (commander) Kw(actor, "Commander");
            Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            item = Add(t, "training-costly-weapon", 0, Zone.Hand); card = Add(t, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(t); Engage(e); return e;
        }
        var e = Start(true, out var c, out var item, out var actor);
        Check(!On(e, 1, "stockpiled-attach-commander", c.Id), "not to the other player");
        var offer = e.QueryActions(e.State.PriorityPlayerId).Single(x => x.Enabled && x.Command?.AbilityId == "stockpiled-attach-commander" && x.CardId == c.Id);
        var cost = CardCatalog.Get("training-costly-weapon").GoldCost;
        Eq(offer.TargetGold![item.Id].Cost, Math.Max(0, cost - 4), "4 less Gold for a Commander");
        Accept(e, offer.Command! with { TargetId = item.Id, PerformerId = actor.Id });
        Eq(e.State.Cards[item.Id].AttachedTo, actor.Id, "attached to the performing Personality");
        var n = Start(false, out var c2, out _, out _);
        Check(!n.QueryActions(n.State.PriorityPlayerId).Any(x => x.Enabled && x.Command?.AbilityId == "stockpiled-attach-commander" && x.CardId == c2.Id), "no Commander: the discounted attach is not offered");
        Quiet(def, "stockpiled-attach-commander", "stockpiled-attach");
    }

    private static void TempleGrounds()
    {
        var def = Def("w738-temple-engage");
        var t = Sel(); Add(t, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var temple = Add(t, def, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(t);
        Engage(e);
        Check(!On(e, 0, "w738-temple-engage", temple.Id), "not to the other player");
        Accept(e, new GameCommand("declineReaction", 0, e.Version));
        Accept(e, Cmd(e, 1, "w738-temple-engage", temple.Id));
        Eq(e.State.Cards[temple.Id].Zone, Zone.Battlefield, "Engage: it is in play");
        var b = Table(Phase.Battle, 0); var tb = Add(b, def, 0, Zone.Hand); Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Check(!On(eb, 0, "w738-temple-engage", tb.Id), "the Engage half is not a Battle action");
        Accept(eb, Cmd(eb, 0, "w738-temple-battle", tb.Id));
        Eq(eb.State.Cards[tb.Id].Zone, Zone.Battlefield, "Battle: it is in play");
        Quiet(def, "w738-temple-engage");
    }

    private static void Yari()
    {
        var def = Def("pe-yari-bar-targeting");
        var s = Sel();
        var wearer = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Kw(wearer, "Crane Clan");
        var yari = Add(s, def, 1, Zone.Attached, province: 0, parent: wearer.Id);
        var archer = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Engage(e);
        Check(!On(e, 0, "pe-yari-bar-targeting", yari.Id), "not to the other player");
        Accept(e, new GameCommand("declineReaction", 0, e.Version));
        var option = e.QueryActions(1).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == "pe-yari-bar-targeting" && a.Enabled && a.Command.TargetId == archer.Id);
        Check(option is not null, "the Yari may name the opposing Personality");
        Accept(e, option!.Command!);
        Eq(e.State.PromoItemsYariBars.Count, 1, "the restriction is recorded");
        var b = Table(Phase.Battle, 0); var holder = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var spear = Add(b, def, 0, Zone.Attached, province: 0, parent: holder.Id);
        var sturdy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Check(!On(eb, 0, "pe-yari-bar-targeting", spear.Id), "not outside the Engage window");
        Accept(eb, Offer(eb, 0, "pe-yari-ranged", spear.Id, sturdy.Id));
        Eq(eb.State.Cards[spear.Id].Zone, Zone.FateDiscard, "the Ranged attack still works: the Yari was destroyed as the cost");
        Quiet(def, "pe-yari-bar-targeting");
    }

    // ---- misc windows -------------------------------------------------------------------------------------------
    private static MatchState Combat() { var s = Table(Phase.Battle, 0); Strong(s); s.PriorityPlayerId = 1; s.UnresolvedBattlefields = [0, 1]; return s; }

    private static void Dairuko()
    {
        var def = Def("dairuko-rally");
        MatchEngine Start(out CardInstance dairuko, out CardInstance ally)
        {
            var s = Combat();
            dairuko = Add(s, def, 0, Zone.Battlefield, province: 0); ally = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Start(out var d, out var ally);
        Check(!On(e, 0, "dairuko-rally", d.Id), "nothing before the battle resolves");
        Accept(e, new GameCommand("pass", 1, e.Version)); Accept(e, new GameCommand("pass", 0, e.Version));
        Eq(Window(e), "beforeBattleReaction", "the window opens before the resolution"); Eq(e.State.PendingChoice?.CardId, d.Id);
        Check(!On(e, 1, "dairuko-rally", d.Id), "not to the other player");
        Accept(e, Cmd(e, 0, "dairuko-rally", d.Id));
        Check(e.State.Cards[ally.Id].Zone == Zone.Home && !e.State.Cards[ally.Id].Bowed && !e.State.Cards[d.Id].Bowed, "the resolution did not bow the army");
        Quiet(def, "dairuko-rally");
        var b = Table(Phase.Battle, 0); var db = Add(b, def, 0, Zone.Battlefield, province: 0); var foe = Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "dairuko-melee", db.Id, foe.Id)); Settle(eb);
        Check(eb.State.Cards[foe.Id].Zone != Zone.Battlefield, "her Melee attack is unaffected: it destroyed the enemy");
    }

    private static void Entrenched()
    {
        var def = Def("entrenched-negate");
        MatchEngine Start(bool lead, out CardInstance attackerCard, out CardInstance defenderCard, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1); s.BattleNumber = 1; s.FirstActionPlayerId = 1; if (lead) s.AttackerLeadBattle = 1;
            attackerCard = Add(s, def, 1, Zone.Hand); defenderCard = Add(s, def, 0, Zone.Hand);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "entrenched-bow", attackerCard.Id, victim.Id));
            return e;
        }
        var e = Start(true, out var ac, out var dc, out var v);
        Check(e.State.PendingChoice is { Type: "announceReaction", PlayerId: 0 }, "the Defender is offered the Reaction");
        Check(!On(e, 1, "entrenched-negate", ac.Id), "not to the Attacker");
        Accept(e, Cmd(e, 0, "entrenched-negate", dc.Id)); Settle(e);
        Check(!e.State.Cards[v.Id].Bowed, "the Attacker's Battle action was negated"); Eq(e.State.Cards[dc.Id].Zone, Zone.FateDiscard, "Entrenched Position was played");
        var n = Start(false, out _, out var dc2, out var v2);
        Check(n.State.PendingChoice is null && n.State.Cards[v2.Id].Bowed && !On(n, 0, "entrenched-negate", dc2.Id), "after the Defender's first opportunity: no window");
        var b = Table(Phase.Battle, 0); var bow = Add(b, def, 0, Zone.Hand); Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var enemy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, "entrenched-bow", bow.Id, enemy.Id));
        Check(eb.State.Cards[enemy.Id].Bowed, "the Battle bow still works");
        Quiet(def, "entrenched-negate");
    }

    private static void FeignDeath()
    {
        var def = Def("feign-death-battle");
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var feign = Add(s, def, 0, Zone.Hand); var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); var earlier = Add(s, "training-crane-veteran", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Check(!On(e, 0, "feign-death-battle", feign.Id), "nothing destroyed yet");
        PassTo(e, () => e.QueryActions(0).Any(o => o.Command?.AbilityId == "feign-death-battle" && o.Enabled));
        Check(e.State.Cards[defender.Id].Zone == Zone.Dead, "the defender died in the resolution");
        Check(!On(e, 1, "feign-death-battle", feign.Id), "not to the other player");
        var offers = e.QueryActions(0).Where(o => o.Command?.AbilityId == "feign-death-battle" && o.Enabled).ToArray();
        var targets = offers.SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(defender.Id) && !targets.Contains(earlier.Id), "only the Personality that battle destroyed");
        var honor = Honor(e, 0);
        Accept(e, offers[0].Command! with { TargetId = defender.Id });
        Check(e.State.Cards[defender.Id].Zone == Zone.Home && e.State.Cards[defender.Id].Bowed && e.State.Cards[defender.Id].Dishonorable, "at home, bowed and dishonored");
        Eq(Honor(e, 0), honor - 2, "lose 2 Honor");
        Quiet(def, "feign-death-battle", "feign-death-action");
    }

    private static void Horume()
    {
        var def = Def("horume-honor");
        var s = Table(Phase.Battle, 1); Strong(s);
        var horume = Add(s, def, 1, Zone.Battlefield, province: 0); var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!On(e, 1, "horume-honor", horume.Id), "no duel yet");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: horume.Id, TargetId: enemy.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", 0, e.Version));
        Accept(e, new GameCommand("strike", 0, e.Version));
        Accept(e, new GameCommand("duelistRefocus", 1, e.Version));
        Eq(Window(e), "duelWonReaction", "the duel is over: the winner's window");
        Check(!On(e, 0, "horume-honor", horume.Id), "not to the loser");
        var honor = Honor(e, 1);
        Accept(e, Cmd(e, 1, "horume-honor", horume.Id));
        Eq(Honor(e, 1), honor + 1, "gained 1 Honor");
        Quiet(def, "horume-honor");
    }

    private static void Torch()
    {
        var def = Def("torch-protect");
        MatchEngine Start(bool thunder, out CardInstance torch, out CardInstance performer, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0);
            torch = s.Cards[s.Players[0].StrongholdId]; torch.DefinitionId = def;
            performer = Add(s, thunder ? "training-thunder-shugenja" : "training-battle-bower", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy, -3);
            return MatchEngine.FromTestState(s);
        }
        var e = Start(true, out var torch, out var thunder, out var enemy);
        Check(!On(e, 0, "torch-protect", torch.Id), "nothing announced yet");
        Accept(e, Offer(e, 0, "training-weapon-attack", thunder.Id, enemy.Id));
        Eq(Window(e), "announceReaction", "the announce window opens");
        Check(!On(e, 1, "torch-protect", torch.Id), "not to the other player");
        Accept(e, Cmd(e, 0, "torch-protect", torch.Id));
        Check(e.State.UnnegatableActionKey is not null, "the announced action is marked");
        Check(!On(e, 0, "torch-protect", torch.Id), "once each turn");
        var n = Start(false, out var torch2, out var bower, out var enemy2);
        Accept(n, Offer(n, 0, "training-bow-at-battle", bower.Id, enemy2.Id));
        Check(!On(n, 0, "torch-protect", torch2.Id), "not for a non-Thunder action");
        var m = Start(true, out var torch3, out var perf3, out var enemy3);
        Accept(m, Offer(m, 0, "torch-move", torch3.Id, enemy3.Id, perf3.Id)); Settle(m);
        Check(m.State.Cards[enemy3.Id].Zone == Zone.Home, "the Battle move still works: " + Window(m) + " " + m.State.Cards[enemy3.Id].Zone);
        Quiet(def, "torch-protect");
    }
}
