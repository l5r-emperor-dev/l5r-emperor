using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #830, reaction slice 1: every multi-window card whose Reaction needs a trigger window. Each test opens the real window through ordinary engine
// play (a purchase, an equip, a Strategy's Attack, a Battle action, a battle's resolution ...), proves the Reaction is offered enabled to its own
// player only, accepted, with the printed effect; then proves the identical command is refused (and not offered) once the window is closed or never
// opened, and that the card's other Battle/Open/Limited actions work independently of it.
internal static partial class MultiWindowReaction1Tests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }

    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner, listed for nobody else, refused if the other player submits it; on a copy where the
    // window is declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`).
    private static GameCommand InWindow(MatchEngine e, string windowType, int owner, string ability, string cardId, string? performerId = null, string? targetId = null)
    {
        Check(e.State.PendingChoice?.Type == windowType || windowType == "entryReaction" && e.State.PendingChoice?.Type == "proclaim", ability + ": the window is " + e.State.PendingChoice?.Type); // a Proclaim-eligible recruit shares its entry window with the Proclaim
        if (e.State.PriorityPlayerId != owner && e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the other player holds the first opportunity and has nothing to react with
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner}" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        var command = targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
        Check(!CanUse(e, 1 - owner, ability, cardId), ability + " is listed enabled for the other player");
        Reject(e, command with { PlayerId = 1 - owner });
        var closed = e.Clone();
        Settle(closed);
        Check(closed.State.PendingChoice is null, ability + ": the window did not close");
        Check(!CanUse(closed, owner, ability, cardId, performerId), ability + " is still offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        return command;
    }

    // A fresh position in which the card sits in play (or in hand) and no window was ever opened: not offered, and its command is refused.
    private static void NeverOpened(MatchEngine e, int owner, string ability, string cardId, GameCommand command)
    {
        Check(e.State.PendingChoice is null, "a window is open");
        Check(!CanUse(e, owner, ability, cardId) && !CanUse(e, 1 - owner, ability, cardId), ability + " is offered with no window open");
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = owner });
    }

    // ---- entering-play windows ----------------------------------------------------------------------------------------------------------------------------------------------
    // A Personality bought from player 0's province 0 in the Dynasty Phase, leaving its entering-play window open.
    private static (MatchEngine E, string Id) Buy(string def, Action<MatchState>? tweak = null, int gold = 14)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = "Dragon"; s.Players[0].Honor = 30;
        Gold(s, 0, gold);
        var entering = s.Cards[s.Players[0].Provinces[0].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false });
        Accept(e, option.Command!);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"{def} bought, but the window is {e.State.PendingChoice?.Type}");
        return (e, entering.Id);
    }

    // The same Personality standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, out CardInstance card, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        card = Add(s, def, 0, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    private static ActionOption[] Listed(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();

    private static void TsudokenEntry()
    {
        const string ability = "tsudoken-exp2-entry";
        var def = Def(ability, "Akodo Tsudoken");
        var (e, id) = Buy(def);
        // "After Tsudoken enters play, lose 3 Honor" is a trait the engine takes in the entry window (like Ayumi's), as his controller's command.
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var honor = Honor(e, 0);
        Accept(e, command);
        Eq(Honor(e, 0), honor - 3, "the 3 Honor was lost");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        // At home with no window: not offered, refused; his Open straighten is independent.
        var h = AtHome(def, out var t, (s, c) => Add(s, "training-lion-samurai", 0, Zone.Home, bowed: true));
        NeverOpened(h, 0, ability, t.Id, command with { CardId = t.Id });
        var bowed = h.State.Cards.Values.First(c => c.OwnerId == 0 && c.Bowed);
        Accept(h, Offer(h, 0, "tsudoken-exp2-straighten", t.Id, bowed.Id));
        Check(!h.State.Cards[bowed.Id].Bowed, "the Open straighten works");
    }

    private static void IzunaEntry()
    {
        var def = Def("izuna-ninja");
        foreach (var (ability, keyword) in new[] { ("izuna-kolat", "Kolat"), ("izuna-ninja", "Ninja"), ("izuna-shadowlands", "Shadowlands") })
        {
            var (e, id) = Buy(def, s => Add(s, "training-lion-samurai", 1, Zone.Home));
            var victim = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
            var offer = Offer(e, 0, ability, id, victim.Id);
            Check(!CanUse(e, 1, ability, id), "the other player is offered Izuna's Reaction");
            Reject(e, offer with { PlayerId = 1 });
            var closed = e.Clone(); Settle(closed);
            Check(!CanUse(closed, 0, ability, id), ability + " is offered after the window closed");
            Reject(closed, offer with { ExpectedVersion = closed.Version });
            Accept(e, offer);
            Check(e.State.Cards[victim.Id].Dishonorable, ability + ": the target was dishonored");
            Check(e.State.Cards[victim.Id].GrantedKeywords.Any(k => k.Keyword == keyword), ability + ": permanently gave " + keyword);
            // The three halves share one use.
            Check(!CanUse(e, 0, "izuna-ninja", id) && !CanUse(e, 0, "izuna-kolat", id), "the three share one use");
        }
        // Out of any window: standing at home in the Action phase, none of the three is offered.
        var h = AtHome(def, out var izuna, (s, c) => Add(s, "training-lion-samurai", 1, Zone.Home));
        foreach (var ability in new[] { "izuna-kolat", "izuna-ninja", "izuna-shadowlands" })
        {
            Check(!CanUse(h, 0, ability, izuna.Id), ability + " offered with no entering-play window");
            Reject(h, Use(h, 0, ability, izuna.Id, h.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Id));
        }
    }

    private static void KikugoroEntry()
    {
        const string ability = "kikugoro-self-entry";
        var def = Def(ability);
        var s = MatchEngine.CreateEconomyScenario().State;
        var (e, id) = Buy(def, null, 8);
        var top = e.State.Players[0].DynastyDeck.Take(2).ToArray();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Eq(e.State.PendingChoice?.Type, "lookArrange", "the look at the top two Dynasty cards");
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["1:top", "0:bottom"]));
        Eq(e.State.Players[0].DynastyDeck[0], top[1], "the one kept on top");
        Eq(e.State.Players[0].DynastyDeck[^1], top[0], "the other went to the bottom");
        Check(s.Cards.Count > 0);
        // Standing in play with no entry: not offered (neither half), refused.
        var h = AtHome(def, out var k);
        NeverOpened(h, 0, ability, k.Id, command);
        Check(!CanUse(h, 0, "kikugoro-henshin-entry", k.Id), "the Henshin half is not offered with no Henshin entering");
        // The same card's Battle-free Open/Limited actions: Kikugoro prints only the three entry/ring triggers, so there is nothing else to run.
        Check((CardCatalog.Get(def).Abilities ?? []).All(a => AbilityCatalog.All[a].Timing == "Reaction"), "Kikugoro has only Reactions");
    }

    private static void HorumeEntry()
    {
        var def = Def("horume-gain");
        foreach (var (ability, delta) in new[] { ("horume-gain", 1), ("horume-lose", -1) })
            foreach (var chosen in new[] { 0, 1 })
            {
                var (e, id) = Buy(def, null, 16);
                var option = Options(e, 0, ability, id).FirstOrDefault(o => o.Enabled && o.Command!.TargetPlayerId == chosen) ?? throw new Exception($"{ability} for player {chosen} not offered in {e.State.PendingChoice?.Type}: " + string.Join("; ", e.QueryActions(0).Select(a => a.Type + ":" + a.Label + (a.Enabled ? "" : " off " + a.Reason))));
                Check(!CanUse(e, 1, ability, id), "the other player is offered Horume's Reaction");
                Reject(e, option.Command! with { PlayerId = 1 });
                var closed = e.Clone(); Settle(closed);
                Check(!CanUse(closed, 0, ability, id), ability + " offered after the window closed");
                Reject(closed, option.Command! with { ExpectedVersion = closed.Version });
                var before = Honor(e, chosen);
                Accept(e, option.Command!);
                Eq(Honor(e, chosen), before + delta, $"{ability} on player {chosen}");
            }
        var h = AtHome(def, out var horume);
        foreach (var ability in new[] { "horume-gain", "horume-lose" })
        {
            Check(!CanUse(h, 0, ability, horume.Id), ability + " offered with no entering-play window");
            Reject(h, new GameCommand("useAbility", 0, h.Version, CardId: horume.Id, AbilityId: ability, TargetPlayerId: 0));
        }
        // His duel Reaction ("horume-honor") is not an entry window action either.
        Check(!CanUse(h, 0, "horume-honor", horume.Id), "the duel Reaction is not offered outside a duel");
    }

    private static void YukoEntry()
    {
        const string ability = "wl1-yuko-invest";
        var def = Def(ability);
        var (e, id) = Buy(def, null, 12);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var honor = Honor(e, 0);
        Accept(e, command);
        Eq(Honor(e, 0), honor + 1, "Invest 2 Gold: gained 1 Honor");
        Check(command.SourceIds!.Length > 0 && command.SourceIds.All(g => e.State.Cards[g].Bowed), "the 2 Gold were paid");
        // Declining costs nothing.
        var (f, id2) = Buy(def, null, 12);
        var h0 = Honor(f, 0);
        Accept(f, new GameCommand("declineReaction", 0, f.Version));
        Eq(Honor(f, 0), h0, "declined: nothing gained");
        // At home with no window: refused; her Limited bow-for-Honor still works.
        var h = AtHome(def, out var yuko, (s, c) => Gold(s, 0, 4));
        NeverOpened(h, 0, ability, yuko.Id, command with { CardId = yuko.Id });
        var before = Honor(h, 0);
        Accept(h, Enabled(h, 0, "wl1-yuko-honor", yuko.Id).Command!);
        Eq(Honor(h, 0), before + 1, "the Limited action gains 1 Honor");
        Check(h.State.Cards[yuko.Id].Bowed && id2.Length > 0, "and bows her");
    }

    private static void SeikenEntry()
    {
        const string ability = "w739-seiken-recruit";
        var def = Def(ability);
        var (e, id) = Buy(def, s =>
        {
            var cheap = s.Cards[s.Players[0].Provinces[1].CardId!]; cheap.DefinitionId = "training-crane-samurai"; cheap.FaceUp = true; Kw(cheap, "Samurai");
        }, 20);
        var cheap = e.State.Cards[e.State.Players[0].Provinces[1].CardId!];
        var option = Options(e, 0, ability, id).First(o => o.Enabled && (o.TargetIds?.Contains(cheap.Id) ?? false));
        var command = option.Command! with { TargetId = cheap.Id };
        Check(!CanUse(e, 1, ability, id), "the other player is offered Seiken's recruit");
        Reject(e, command with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, id), "offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        var honor = Honor(e, 0);
        Accept(e, command);
        Check(e.State.Cards[cheap.Id].Zone is Zone.Home or Zone.Battlefield, "the Samurai was recruited into play");
        Eq(Honor(e, 0), honor, "ignoring costs");
        // His Open action (take the Imperial Favor) still works at home in the Action phase; the recruit is not offered there.
        var h = AtHome(def, out var seiken);
        Check(!CanUse(h, 0, ability, seiken.Id), "the recruit is not offered with no entering-play window");
        Accept(h, Enabled(h, 0, "w739-seiken-favor", seiken.Id).Command!);
        Eq(h.State.FavorPlayerId, 0, "he took the Imperial Favor");
    }

    // An Item equipped in the Action phase from hand, leaving its entering-play window open.
    private static (MatchEngine E, CardInstance Sword, CardInstance Host) Equip(string def, string clanKeyword)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var host = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), clanKeyword);
        var sword = Add(s, def, 0, Zone.Hand);
        Gold(s, 0, 6);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == sword.Id);
        Check(equip.Command is not null, "equip offered: " + equip.Reason);
        Accept(e, equip.Command! with { TargetId = host.Id });
        return (e, sword, host);
    }

    private static void CraneSwordEntry()
    {
        const string ability = "celestial-sword-crane-honor";
        var def = Def(ability);
        var (e, sword, host) = Equip(def, "Crane Clan");
        var command = InWindow(e, "entryReaction", 0, ability, sword.Id);
        var honor = Honor(e, 0);
        Accept(e, command);
        Eq(Honor(e, 0), honor + 2, "gained 2 Honor as it entered play");
        // Priority passed on after the window; with the sword simply standing in play its Open action is independent of the Reaction.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var wielder = Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Crane Clan");
        var inPlay = Add(t, def, 0, Zone.Attached, province: null, parent: wielder.Id);
        var enemy = Add(t, "training-lion-samurai", 1, Zone.Home);
        var o = MatchEngine.FromTestState(t);
        var force = o.CardForceForTest(enemy.Id);
        Accept(o, Offer(o, 0, "celestial-sword-crane-weaken", inPlay.Id, enemy.Id));
        Check(o.CardForceForTest(enemy.Id) < force, "its Open action gave -4F");
        NeverOpened(o, 0, ability, inPlay.Id, command with { CardId = inPlay.Id });
        Check(sword.Id.Length > 0 && host.Id.Length > 0);
    }

    private static void ScorpionSwordEntry()
    {
        const string ability = "celestial-sword-scorpion-lose";
        var def = Def(ability);
        var (e, sword, host) = Equip(def, "Scorpion Clan");
        Eq(e.State.PendingChoice?.Type, "entryReaction");
        var options = Options(e, 0, ability, sword.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.Command!.TargetPlayerId == 0) && options.Any(o => o.Command!.TargetPlayerId == 1), "either player may be chosen");
        var lose = options.First(o => o.Command!.TargetPlayerId == 1).Command!;
        Check(!CanUse(e, 1, ability, sword.Id), "the other player is offered the Reaction");
        Reject(e, lose with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, sword.Id), "offered after the window closed");
        Reject(closed, lose with { ExpectedVersion = closed.Version });
        var theirs = Honor(e, 1);
        Accept(e, lose);
        Eq(Honor(e, 1), theirs - 2, "the chosen player lost 2 Honor");
        NeverOpened(e, 0, ability, sword.Id, lose);
        // Its Battle action (bow a bare enemy card) works on its own in a battle.
        var t = Table(Phase.Battle, 1);
        var wielder = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(t, def, 1, Zone.Attached, province: 0, parent: wielder.Id);
        var target = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(t);
        Accept(b, Offer(b, 1, "celestial-sword-scorpion-bow", b.State.Cards.Values.Single(c => c.DefinitionId == def).Id, target.Id));
        Check(b.State.Cards[target.Id].Bowed, "the Battle action bowed the bare card");
    }

    // ---- Ranged / Melee Attack windows (before-attack-resolves) -------------------------------------------------------------------------------------------------------------
    // Player 1 (the Attacker) Attacks player 0's Personality at province 0 with a Ranged 3 (a Strategy in hand costing 2 Honor) or Melee 2 (a Weapon on his Lion) Attack; `reactor`
    // is a Strategy in `owner`'s hand or a Personality in his home. The target's Force is set so the Attack's strength decides whether it dies.
    private sealed record Rig(MatchEngine E, CardInstance Reactor, CardInstance Target, string AttackCard, string AttackAbility);

    private static Rig AttackRig(string reactorDef, int owner, string kind, bool armor, int targetForce, bool attack = true)
    {
        var s = Table(Phase.Battle, 1);
        var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var source = kind == "Ranged" ? Add(s, "training-honor-cost-strategy", 1, Zone.Hand) : Add(s, "training-weapon-with-attack", 1, Zone.Attached, province: 0, parent: lion.Id);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        if (armor) Kw(Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: target.Id), "Armor");
        var type = CardCatalog.Get(reactorDef).Type;
        // Issue #1131: a Personality's Reaction to an Attack needs presence at the battlefield (the Training fixture has no location rule and stays home).
        var reactor = type == CardType.Personality ? reactorDef == "training-attack-reactor" ? Add(s, reactorDef, owner, Zone.Home) : Add(s, reactorDef, owner, Zone.Battlefield, province: 0) : Add(s, reactorDef, owner, Zone.Hand);
        var probe = MatchEngine.FromTestState(s);
        Force(target, targetForce - probe.CardForceForTest(target.Id));
        var e = MatchEngine.FromTestState(s);
        var abilityId = kind == "Ranged" ? "training-honor-cost-attack" : "training-weapon-attack";
        if (attack) Accept(e, Offer(e, 1, abilityId, source.Id, target.Id));
        return new Rig(e, reactor, target, source.Id, abilityId);
    }

    private static bool Dead(MatchEngine e, CardInstance target) => e.State.Cards[target.Id].Zone == Zone.Dead;

    // One Reaction of the attack window: offered to `owner` (and not the other player) while the Attack is pending, accepted with the effect on the Attack's outcome (`killsWithReaction` says
    // whether the Attack then kills the target), refused once the window closed or before any Attack, and -- when `meleeToo` is false -- not offered against a Melee Attack.
    private static void AttackReaction(string ability, string reactorDef, int owner, bool armor, int targetForce, bool killsWithReaction, bool meleeToo, string[] kinds)
    {
        foreach (var kind in kinds)
        {
            var rig = AttackRig(reactorDef, owner, kind, armor, targetForce);
            var control = AttackRig(reactorDef, owner, kind, armor, targetForce);
            Settle(control.E);
            Eq(Dead(control.E, control.Target), !killsWithReaction, $"{ability}/{kind}: without the reaction the Attack {(killsWithReaction ? "fails" : "hits")}");
            var command = InWindow(rig.E, "attackReaction", owner, ability, rig.Reactor.Id);
            Accept(rig.E, command);
            Settle(rig.E);
            Eq(Dead(rig.E, rig.Target), killsWithReaction, $"{ability}/{kind}: the reaction's effect on the Attack");
            // No Attack yet: not offered, refused.
            var quiet = AttackRig(reactorDef, owner, kind, armor, targetForce, attack: false);
            NeverOpened(quiet.E, owner, ability, quiet.Reactor.Id, command with { CardId = quiet.Reactor.Id });
        }
        if (!meleeToo)
        {
            var melee = AttackRig(reactorDef, owner, "Melee", armor, targetForce);
            Eq(melee.E.State.PendingAttack?.Kind, "Melee");
            Check(!CanUse(melee.E, owner, ability, melee.Reactor.Id), ability + " is offered against a Melee Attack");
            Reject(melee.E, Use(melee.E, owner, ability, melee.Reactor.Id));
        }
    }

    // A fresh battle for the quick check that a card's other Battle action still works on its own.
    private static MatchEngine Battle(Action<MatchState> arrange, int actor = 1, int? priority = null)
    {
        var s = Table(Phase.Battle, actor); s.PriorityPlayerId = priority ?? actor; arrange(s);
        return MatchEngine.FromTestState(s);
    }

    private static void ACloseCall()
    {
        const string ability = "a-close-call-negate";
        var def = Def(ability);
        AttackReaction(ability, def, 0, true, 2, false, true, ["Ranged", "Melee"]);
        // No Armor attached to the target: not offered.
        var bare = AttackRig(def, 0, "Ranged", false, 2);
        Check(!CanUse(bare.E, 0, ability, bare.Reactor.Id), "offered against a Personality with no Armor");
        // Its Battle action: +2F to a Personality with an attachment.
        var e = Battle(s => { var p = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: p.Id); Add(s, def, 0, Zone.Hand); }, 0);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        var host = e.State.Cards.Values.First(c => c.Zone == Zone.Attached && c.OwnerId == 0 && c.DefinitionId == "training-weapon");
        host = e.State.Cards[host.AttachedTo!];
        var force = e.CardForceForTest(host.Id);
        Accept(e, Offer(e, 0, "a-close-call-boost", card.Id, host.Id));
        Eq(e.CardForceForTest(host.Id), force + 2, "its Battle action gave +2F");
    }

    private static void KanameAttackBoost()
    {
        const string ability = "kaname-attack-boost";
        var def = Def(ability);
        AttackReaction(ability, def, 1, false, 4, true, false, ["Ranged"]);
        // His Ranged 1 Attack action is a Battle action of its own.
        var e = Battle(s => { Add(s, def, 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); }, 0);
        var kaname = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        Accept(e, Offer(e, 0, "kaname-ranged-attack", kaname.Id, e.State.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id));
    }

    private static void UmikoAttackBoost()
    {
        const string ability = "umiko-attack-boost";
        var def = Def(ability);
        AttackReaction(ability, def, 1, false, 4, true, false, ["Ranged"]);
        // Her other Reaction (-2 to a Ranged Attack) is a window action of its own.
        var rig = AttackRig(def, 0, "Ranged", false, 2);
        Accept(rig.E, Enabled(rig.E, 0, "umiko-attack-reduce", rig.Reactor.Id).Command!);
        Settle(rig.E);
        Check(!Dead(rig.E, rig.Target), "-2 strength saved the Force 2 target from the Ranged 3");
    }

    private static void PrecisionBoost()
    {
        const string ability = "precision-boost";
        var def = Def(ability);
        AttackReaction(ability, def, 1, false, 4, true, true, ["Ranged", "Melee"]);
        var e = Battle(s => { Add(s, def, 1, Zone.Hand); Add(s, def, 1, Zone.Hand); /* issue #917: a second copy -- the card being played is in the resolution area and cannot react to its own Attack */ Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); });
        var card = e.State.Cards.Values.First(c => c.DefinitionId == def);
        Accept(e, Offer(e, 1, "precision-attack", card.Id, e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "its own Battle Attack opens the window");
    }

    private static void StrongDefenses()
    {
        const string ability = "strong-defenses-reaction";
        var def = Def(ability);
        AttackReaction(ability, def, 0, false, 2, false, true, ["Ranged", "Melee"]);
        var e = Battle(s => { Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, def, 0, Zone.Hand); }, 1, 0);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        var defender = e.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var force = e.CardForceForTest(defender.Id);
        var boost = e.QueryActions(0).FirstOrDefault(a => a.CardId == card.Id && a.Command?.AbilityId == "strong-defenses-boost" && a.Command.PerformerId == defender.Id && a.Enabled);
        Check(boost is not null, "strong-defenses-boost offered: " + string.Join("; ", e.QueryActions(0).Where(a => a.CardId == card.Id).Select(a => a.Label + " " + a.Enabled + " " + a.Reason + " " + a.Command?.PerformerId)));
        Accept(e, boost!.Command!);
        Eq(e.CardForceForTest(defender.Id), force + 5, "its Battle action gave +5F");
    }

    private static void TrustingInstinct()
    {
        const string ability = "trusting-instinct-reaction";
        var def = Def(ability);
        AttackReaction(ability, def, 0, false, 2, false, true, ["Ranged", "Melee"]);
        var e = Battle(s => { Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, def, 0, Zone.Hand); }, 0);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        var mover = e.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        Accept(e, Enabled(e, 0, "trusting-instinct-move", card.Id, mover.Id).Command!);
        Eq(e.State.Cards[mover.Id].Zone, Zone.Battlefield, "its Battle action moved the Personality to the battlefield");
    }

    private static void TrainingAttackNegate()
    {
        const string ability = "training-attack-negate";
        AttackReaction(ability, "training-attack-reactor", 0, false, 2, false, true, ["Ranged", "Melee"]);
        // Its other Reaction, the boost, is a separate window action.
        var rig = AttackRig("training-attack-reactor", 1, "Ranged", false, 4);
        Accept(rig.E, Enabled(rig.E, 1, "training-attack-boost", rig.Reactor.Id).Command!);
        Settle(rig.E);
        Check(Dead(rig.E, rig.Target), "+2 strength killed the Force 4 target");
    }

    // ---- before-action windows: another player's action targets one of this player's cards --------------------------------------------------------------------------------------
    // Player 1 (the Attacker) acts, player 0's cards react. `Bow` is the plain Battle action (bow a target enemy Personality at the battlefield), `Destroy` the plain destroy.
    private const string Bow = "training-bow-at-battle", Destroy = "training-destroy-no-attachments";

    private static MatchEngine Open(MatchState s, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, player, ability, cardId, targetId, performerId));
        return e;
    }

    private static void ChampionOfThunder()
    {
        const string ability = "champion-of-thunder-negate";
        foreach (var name in new[] { "Champion of Thunder", "O-Yoroi Troops" })
        {
            var def = Def(ability, name);
            MatchEngine Rig(bool act, bool targetHost, out CardInstance champion, out CardInstance host)
            {
                var s = Table(Phase.Battle, 1);
                host = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
                champion = Add(s, def, 0, Zone.Attached, province: 0, parent: host.Id);
                var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
                if (!act) return MatchEngine.FromTestState(s);
                return Open(s, 1, Bow, bower.Id, targetHost ? host.Id : other.Id);
            }
            var e = Rig(true, true, out var champ, out var hostCard);
            var command = InWindow(e, "beforeActionReaction", 0, ability, champ.Id, null, hostCard.Id);
            Accept(e, command); Settle(e);
            Check(!e.State.Cards[hostCard.Id].Bowed, name + ": the action's effects on the unit's card were negated");
            var control = Rig(true, true, out _, out var controlHost); Settle(control);
            Check(control.State.Cards[controlHost.Id].Bowed, name + ": declined, the bow lands");
            var elsewhere = Rig(true, false, out var c2, out _);
            Check(elsewhere.State.PendingChoice is null && !CanUse(elsewhere, 0, ability, c2.Id), name + ": an action on a card outside the unit opens nothing");
            var quiet = Rig(false, true, out var c3, out _);
            NeverOpened(quiet, 0, ability, c3.Id, command with { CardId = c3.Id });
        }
        // Champion of Thunder's own Ranged 6 Attack is a Battle action of the card.
        var champion = Def("champion-of-thunder-attack", "Champion of Thunder");
        var s2 = Table(Phase.Battle, 0);
        var wielder = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s2, champion, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var enemy = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var a = MatchEngine.FromTestState(s2);
        Accept(a, Offer(a, 0, "champion-of-thunder-attack", follower.Id, enemy.Id));
        Eq(a.State.Cards[enemy.Id].Zone, Zone.Dead, "its Ranged 6 Attack resolved (nobody held an Attack Reaction) and destroyed the Force 3 target");
        Check(wielder.Id.Length > 0);
    }

    private static void KisadaRedirect()
    {
        const string ability = "kisada-redirect";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool targetKisada, out CardInstance kisada, out CardInstance ally, out CardInstance home)
        {
            var s = Table(Phase.Battle, 1);
            kisada = Add(s, def, 0, Zone.Battlefield, province: 0);
            ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            home = Add(s, "training-crane-samurai", 0, Zone.Home);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, targetKisada ? kisada.Id : ally.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var kisada, out var ally, out var home);
        var command = InWindow(e, "beforeActionReaction", 0, ability, kisada.Id, null, ally.Id);
        var offers = Options(e, 0, ability, kisada.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId!).ToArray();
        Check(offers.SequenceEqual([ally.Id]), "only the other Personality at his location is offered: " + string.Join(",", offers) + $" (home {home.Id})");
        Accept(e, command); Settle(e);
        Check(e.State.Cards[ally.Id].Bowed && !e.State.Cards[kisada.Id].Bowed, "the action landed on the chosen Personality instead");
        var control = Rig(true, true, out var k2, out _, out _); Settle(control);
        Check(control.State.Cards[k2.Id].Bowed, "declined, Kisada is bowed");
        var away = Rig(true, false, out var k3, out _, out _);
        Check(away.State.PendingChoice is null && !CanUse(away, 0, ability, k3.Id), "an action on another Personality opens nothing");
        var quiet = Rig(false, true, out var k4, out _, out _);
        NeverOpened(quiet, 0, ability, k4.Id, command with { CardId = k4.Id });
        // His Battle action: destroy an enemy card with lower Force.
        var s = Table(Phase.Battle, 0);
        var kisada5 = Add(s, def, 0, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 0, "kisada-destroy", kisada5.Id, weak.Id));
        Eq(b.State.Cards[weak.Id].Zone, Zone.Dead, "his Battle action destroyed the lower-Force card");
    }

    private static void HundredFoldCut()
    {
        const string ability = "hundred-fold-cut-destroy";
        var def = Def(ability);
        var conviction = Def("relentless-conviction-destroy");
        MatchEngine Rig(bool act, out CardInstance cut, out CardInstance kensai, out CardInstance weapon)
        {
            var s = Table(Phase.Battle, 1);
            kensai = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Kensai"); kensai.Dishonorable = true;
            weapon = Add(s, "training-costly-weapon", 0, Zone.Attached, province: 0, parent: kensai.Id);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var card = Add(s, conviction, 1, Zone.Hand);
            cut = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Use(e, 1, "relentless-conviction-destroy", card.Id, kensai.Id));
            return e;
        }
        var e = Rig(true, out var cut, out var kensai, out var weapon);
        var command = InWindow(e, "beforeActionReaction", 0, ability, cut.Id);
        Accept(e, command); Settle(e);
        Check(e.State.Cards[weapon.Id].Zone == Zone.FateDiscard, "the Weapon was destroyed as the cost");
        Eq(e.State.Cards[kensai.Id].Zone, Zone.Battlefield, "the Kensai's destruction from the action was negated");
        Eq(e.State.Cards[cut.Id].Zone, Zone.FateDiscard, "Hundred-Fold Cut is spent");
        var control = Rig(true, out _, out var k2, out _); Settle(control);
        Eq(control.State.Cards[k2.Id].Zone, Zone.Dead, "declined, the Kensai dies");
        var quiet = Rig(false, out var c3, out _, out _);
        NeverOpened(quiet, 0, ability, c3.Id, command with { CardId = c3.Id });
        // Its Battle action: bow the Kensai or his Weapon, draw two.
        var s = Table(Phase.Battle, 0);
        var kensai2 = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Kensai");
        var blade = Add(s, "training-costly-weapon", 0, Zone.Attached, province: 0, parent: kensai2.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var cut2 = Add(s, def, 0, Zone.Hand);
        var b = MatchEngine.FromTestState(s);
        var deck = b.State.Players[0].FateDeck.Count;
        Accept(b, Enabled(b, 0, "hundred-fold-cut-draw", cut2.Id, blade.Id).Command!);
        Eq(b.State.Players[0].FateDeck.Count, deck - 2, "two cards were drawn");
    }

    private static void KaeFavor()
    {
        const string ability = "kae-favor-negate";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool favor, out CardInstance kae)
        {
            var s = Table(Phase.Battle, 1);
            if (favor) s.FavorPlayerId = 0;
            kae = Add(s, def, 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, kae.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var kae);
        var command = InWindow(e, "beforeActionReaction", 0, ability, kae.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded");
        Check(!e.State.Cards[kae.Id].Bowed, "her bowing from the action is negated");
        var nofavor = Rig(true, false, out var k2);
        Check(!CanUse(nofavor, 0, ability, k2.Id), "no Favor, nothing to discard: not offered"); Settle(nofavor);
        Check(nofavor.State.Cards[k2.Id].Bowed, "and she is bowed");
        var quiet = Rig(false, true, out var k3);
        NeverOpened(quiet, 0, ability, k3.Id, command with { CardId = k3.Id });
        // Her Battle action: move home.
        var s = Table(Phase.Battle, 0);
        var kae4 = Add(s, def, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Enabled(b, 0, "kae-move-home", kae4.Id).Command!);
        Eq(b.State.Cards[kae4.Id].Zone, Zone.Home, "Kae moved home");
    }

    private static void RequestAuthorization()
    {
        const string ability = "request-authorization-negate";
        var def = Def(ability);
        MatchEngine Rig(bool act, out CardInstance card, out CardInstance mover, out CardInstance strategy)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            card = Add(s, def, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            mover = Add(s, "training-lion-samurai", 1, Zone.Home);
            strategy = Add(s, "training-move-battle-strategy", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "training-move-to-battle", strategy.Id, mover.Id));
            return e;
        }
        var e = Rig(true, out var card, out var mover, out _);
        var command = InWindow(e, "beforeActionReaction", 0, ability, card.Id, null, mover.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "his movement to the current battlefield was negated");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var control = Rig(true, out _, out var m2, out _); Settle(control);
        Eq(control.State.Cards[m2.Id].Zone, Zone.Battlefield, "declined, he moved to the battle");
        var quiet = Rig(false, out var c3, out _, out _);
        NeverOpened(quiet, 0, ability, c3.Id, command with { CardId = c3.Id });
        // A Battle action that moves nobody to the battle opens no window.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        Add(s, def, 0, Zone.Hand);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var b = Open(s, 1, Bow, bower.Id, target.Id);
        Check(b.State.PendingChoice?.Type != "beforeActionReaction", "a bow moves nobody: no window");
        // Its Battle action: bow your performing Courtier at any location and move a Personality home.
        var t = Table(Phase.Battle, 0);
        var courtier = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Courtier");
        var foe = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card2 = Add(t, def, 0, Zone.Hand);
        var m = MatchEngine.FromTestState(t);
        var move = m.QueryActions(0).FirstOrDefault(a => a.CardId == card2.Id && a.Command?.AbilityId == "request-authorization-move" && a.Enabled && a.Command.PerformerId == courtier.Id && (a.TargetIds?.Contains(foe.Id) ?? false));
        Check(move is not null, "request-authorization-move offered: " + string.Join("; ", m.QueryActions(0).Where(a => a.CardId == card2.Id).Select(a => a.Label + " " + a.Enabled + " " + a.Reason)));
        Accept(m, move!.Command! with { TargetId = foe.Id });
        Eq(m.State.Cards[foe.Id].Zone, Zone.Home, "the target moved home");
    }

    private static void TamokoNegates()
    {
        const string ability = "tamoko-negate";
        var def = Def(ability, "Tamori Tamoko");
        var treachery = Def("treachery-deceit-move");
        MatchEngine Rig(bool act, out CardInstance tamoko, out CardInstance big)
        {
            var s = Table(Phase.Battle, 1);
            var card = Add(s, treachery, 1, Zone.Hand);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            big = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: big.Id);
            tamoko = Add(s, def, 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "treachery-deceit-move", card.Id, big.Id));
            return e;
        }
        var e = Rig(true, out var tamoko, out var big);
        var command = InWindow(e, "beforeActionReaction", 0, ability, tamoko.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[big.Id].Zone, Zone.Battlefield, "negated: he never moved");
        Check(e.State.Events.Any(x => x.Type == "moveNegated"), "the negation is in the log");
        var control = Rig(true, out _, out var b2); Settle(control);
        Eq(control.State.Cards[b2.Id].Zone, Zone.Home, "declined, he moved home");
        var quiet = Rig(false, out var t3, out _);
        NeverOpened(quiet, 0, ability, t3.Id, command with { CardId = t3.Id });
        // Her Open action, when it is not her turn: target an unbowed Personality of the active player.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var tam = Add(s, def, 0, Zone.Home);
        var active = Add(s, "training-lion-samurai", 1, Zone.Home);
        var o = MatchEngine.FromTestState(s);
        Accept(o, Offer(o, 0, "tamoko-open", tam.Id, active.Id));
    }

    private static void DarkNagaForest()
    {
        const string ability = "dark-naga-forest-negate";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool forest, out CardInstance naga, out CardInstance grove)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            naga = Add(s, def, 0, Zone.Battlefield, province: 0);
            grove = Add(s, "training-marketplace", 0, Zone.Home); if (forest) Kw(grove, "Forest");
            var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, Destroy, destroyer.Id, naga.Id));
            return e;
        }
        var e = Rig(true, true, out var naga, out var grove);
        var command = InWindow(e, "beforeActionReaction", 0, ability, naga.Id);
        Eq(command.TargetId, grove.Id, "the Forest to bow is the Reaction's target");
        Accept(e, command); Settle(e);
        Check(e.State.Cards[grove.Id].Bowed, "the Forest was bowed");
        Eq(e.State.Cards[naga.Id].Zone, Zone.Battlefield, "his destruction from the action was negated");
        var bare = Rig(true, false, out var n2, out _);
        Check(!CanUse(bare, 0, ability, n2.Id), "no Forest, no Reaction"); Settle(bare);
        Eq(bare.State.Cards[n2.Id].Zone, Zone.Dead, "destroyed");
        var quiet = Rig(false, true, out var n3, out _);
        NeverOpened(quiet, 0, ability, n3.Id, command with { CardId = n3.Id });
        // His Battle action: take control of a Nonhuman enemy Personality.
        var s = Table(Phase.Battle, 0);
        var naga4 = Add(s, def, 0, Zone.Battlefield, province: 0);
        var beast = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Nonhuman");
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 0, "dark-naga-take", naga4.Id, beast.Id));
        Eq(b.State.Cards[beast.Id].OwnerId, 0, "a Nonhuman joins his army");
    }

    private static void LoyaltyOfTheScorpion()
    {
        const string ability = "loyalty-scorpion-move";
        var def = Def(ability);
        var mihato = Def("mihato-move");
        MatchEngine Rig(bool act, bool scorpionTarget, out CardInstance strategy, out CardInstance target, out CardInstance sacrifice)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            target = Add(s, scorpionTarget ? "training-scorpion-veteran" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            sacrifice = Add(s, "training-scorpion-veteran", 0, Zone.Home);
            strategy = Add(s, def, 0, Zone.Hand);
            var actor = Add(s, mihato, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "mihato-move", actor.Id, target.Id));
            return e;
        }
        var e = Rig(true, true, out var strategy, out var target, out var sacrifice);
        var command = InWindow(e, "beforeActionReaction", 0, ability, strategy.Id, sacrifice.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the target's movement was negated");
        Eq(e.State.Cards[sacrifice.Id].Zone, Zone.Dead, "the performing Scorpion Clan Personality was destroyed as the cost");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var control = Rig(true, true, out _, out var t2, out _); Settle(control);
        Eq(control.State.Cards[t2.Id].Zone, Zone.Home, "declined, he moved home");
        var other = Rig(true, false, out var s3, out _, out _);
        Check(!CanUse(other, 0, ability, s3.Id), "a target who is not a Scorpion Clan Personality gives it nothing to negate");
        var quiet = Rig(false, true, out var s4, out _, out _);
        NeverOpened(quiet, 0, ability, s4.Id, command with { CardId = s4.Id });
    }

    private static void WardOfAir()
    {
        const string ability = "ward-of-air-negate";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool hitShugenja, out CardInstance ward, out CardInstance shugenja)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            ward = Add(s, def, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "training-honor-cost-attack", strategy.Id, hitShugenja ? shugenja.Id : other.Id));
            return e;
        }
        var e = Rig(true, true, out var ward, out var shugenja);
        var command = InWindow(e, "attackReaction", 0, ability, ward.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[shugenja.Id].Zone, Zone.Battlefield, "his destruction was negated");
        Eq(e.State.Cards[ward.Id].Zone, Zone.FateDiscard, "Ward of Air was destroyed as the cost");
        var control = Rig(true, true, out _, out var s2); Settle(control);
        Eq(control.State.Cards[s2.Id].Zone, Zone.Dead, "declined, the Ranged 3 Attack kills the Force 1 Shugenja");
        var other = Rig(true, false, out var w3, out _);
        Check(!CanUse(other, 0, ability, w3.Id), "not Air: another Personality is not protected");
        var quiet = Rig(false, true, out var w4, out _);
        NeverOpened(quiet, 0, ability, w4.Id, command with { CardId = w4.Id });
        // Its Battle action: move this unit home.
        var t = Table(Phase.Battle, 0);
        var host = Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var ward5 = Add(t, def, 0, Zone.Attached, province: 0, parent: host.Id);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(t);
        Accept(b, Enabled(b, 0, "ward-of-air-move-home", ward5.Id).Command!);
        Eq(b.State.Cards[host.Id].Zone, Zone.Home, "the Shugenja's unit moved home");
    }

    private static void JinnKuenNegate()
    {
        const string ability = "jinn-kuen-negate";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool merchant, out CardInstance jinn, out CardInstance target)
        {
            var s = Table(Phase.Battle, 1);
            jinn = Add(s, def, 0, Zone.Battlefield, province: 0);
            target = Add(s, merchant ? "training-merchant" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, target.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var jinn, out var merchant);
        var command = InWindow(e, "beforeActionReaction", 0, ability, jinn.Id);
        Check(command.SourceIds is { Length: > 0 }, "with the engine's suggested 2 Gold payment");
        Accept(e, command); Settle(e);
        Check(command.SourceIds!.All(id => e.State.Cards[id].Bowed), "the Gold sources were bowed");
        Check(!e.State.Cards[merchant.Id].Bowed, "the whole action was negated");
        var control = Rig(true, true, out _, out var m2); Settle(control);
        Check(control.State.Cards[m2.Id].Bowed, "declined, the Merchant is bowed");
        var plain = Rig(true, false, out var j3, out _);
        Check(plain.State.PendingChoice is null && !CanUse(plain, 0, ability, j3.Id), "a non-Merchant is not protected");
        var quiet = Rig(false, true, out var j4, out _);
        NeverOpened(quiet, 0, ability, j4.Id, command with { CardId = j4.Id });
        // His Open action straightens a Holding or Stronghold.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var jinn5 = Add(s, def, 0, Zone.Home);
        var farm = Add(s, "training-farm", 0, Zone.Home, bowed: true);
        var o = MatchEngine.FromTestState(s);
        Accept(o, Offer(o, 0, "jinn-kuen-straighten", jinn5.Id, farm.Id));
        Check(!o.State.Cards[farm.Id].Bowed, "the Holding was straightened");
    }

    private static void SakumaPolitical()
    {
        const string ability = "sakuma-political-negate";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool political, out CardInstance sakuma, out CardInstance ally)
        {
            var s = Table(Phase.Action, 1);
            sakuma = Add(s, def, 0, Zone.Home);
            ally = Add(s, "training-crane-samurai", 0, Zone.Home);
            var actor = Add(s, political ? "training-seeds5-political-courtier" : "training-bow-any-source", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "training-bow-any", actor.Id, ally.Id));
            return e;
        }
        var e = Rig(true, true, out var sakuma, out var ally);
        var command = InWindow(e, "beforeActionReaction", 0, ability, sakuma.Id);
        Accept(e, command);
        Check(e.State.Cards[sakuma.Id].Bowed, "bowing Sakuma is the cost");
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the acting player decides: pay 2 Gold or the action is negated");
        var negate = e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "negate");
        Accept(e, negate.Command!); Settle(e);
        Check(!e.State.Cards[ally.Id].Bowed, "the Political action's remaining effects were negated");
        var paid = Rig(true, true, out var s2, out var a2);
        Accept(paid, Enabled(paid, 0, ability, s2.Id).Command!);
        Accept(paid, paid.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "pay").Command!); Settle(paid);
        Check(paid.State.Cards[a2.Id].Bowed, "he paid 2 Gold, so the action resolved");
        var plain = Rig(true, false, out var s3, out _);
        Check(plain.State.PendingChoice is null && !CanUse(plain, 0, ability, s3.Id), "a non-Political action opens nothing");
        var quiet = Rig(false, true, out var s4, out _);
        NeverOpened(quiet, 0, ability, s4.Id, command with { CardId = s4.Id });
    }

    private static void SolidDefense()
    {
        const string ability = "solid-defense-negate";
        var def = Def(ability);
        var advantage = Def("exploited-advantage-move");
        MatchEngine Rig(bool act, out CardInstance solid, out CardInstance foe)
        {
            var b = Table(Phase.Battle, 0);
            var card = Add(b, advantage, 0, Zone.Hand);
            var paragon = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon");
            foe = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            solid = Add(b, def, 1, Zone.Hand);
            var wall = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Force(wall, 5 - CardCatalog.Get(wall.DefinitionId).Force);
            var e = MatchEngine.FromTestState(b);
            if (act) Accept(e, Offer(e, 0, "exploited-advantage-move", card.Id, foe.Id, paragon.Id));
            return e;
        }
        var honor = Honor(Rig(false, out _, out _), 0);
        var e = Rig(true, out var solid, out var foe);
        var command = InWindow(e, "honorChangeReaction", 1, ability, solid.Id);
        Accept(e, command); Settle(e);
        Eq(Honor(e, 0), honor, "Solid Defense negated the Honor loss");
        Eq(e.State.Cards[foe.Id].Zone, Zone.Home, "the rest of the action happened");
        var control = Rig(true, out _, out _); Settle(control);
        Eq(Honor(control, 0), honor - 2, "declined, the 2 Honor are lost");
        var quiet = Rig(false, out var s3, out _);
        NeverOpened(quiet, 1, ability, s3.Id, command with { CardId = s3.Id, PlayerId = 1 });
        // Its Battle action: move a Personality without attachments home.
        var t = Table(Phase.Battle, 1);
        var card = Add(t, def, 1, Zone.Hand);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var victim = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var m = MatchEngine.FromTestState(t);
        Accept(m, Offer(m, 1, "solid-defense-move", card.Id, victim.Id));
        Eq(m.State.Cards[victim.Id].Zone, Zone.Home, "the target moved home");
    }

    private static void EffortlessCounterattack()
    {
        const string ability = "effortless-counterattack-negate";
        var def = Def(ability);
        MatchEngine Rig(bool additional, out CardInstance counter, out CardInstance second)
        {
            var s = Table(Phase.Battle, 0);
            var creator = Add(s, "training-grant-bower", 0, Zone.Battlefield, province: 0);
            var follow = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            var first = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            second = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            counter = Add(s, def, 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 0, "training-grant-bow", creator.Id, first.Id));
            if (additional) Accept(e, Offer(e, 0, Bow, follow.Id, second.Id));
            return e;
        }
        var e = Rig(true, out var counter, out var second);
        var command = InWindow(e, "additionalActionReaction", 1, ability, counter.Id);
        Accept(e, command); Settle(e);
        Check(!e.State.Cards[second.Id].Bowed, "the additional action's effects were negated");
        var control = Rig(true, out _, out var s2); Settle(control);
        Check(control.State.Cards[s2.Id].Bowed, "declined, the additional action resolves");
        var first = Rig(false, out var c3, out _);
        Check(first.State.PendingChoice?.Type != "additionalActionReaction" && !CanUse(first, 1, ability, c3.Id), "the creating action is not an additional action: no window");
        // Its Battle action: with your unbowed performer, move home a Personality without attachments.
        var t = Table(Phase.Battle, 1);
        var card = Add(t, def, 1, Zone.Hand);
        var performer = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var victim = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var m = MatchEngine.FromTestState(t);
        var move = m.QueryActions(1).FirstOrDefault(a => a.CardId == card.Id && a.Command?.AbilityId == "effortless-counterattack-move" && a.Enabled && a.Command.PerformerId == performer.Id && (a.TargetIds?.Contains(victim.Id) ?? false));
        Check(move is not null, "effortless-counterattack-move offered: " + string.Join("; ", m.QueryActions(1).Where(a => a.CardId == card.Id).Select(a => a.Label + " " + a.Enabled + " " + a.Reason)));
        Accept(m, move!.Command! with { TargetId = victim.Id });
        Eq(m.State.Cards[victim.Id].Zone, Zone.Home, "the target moved home");
    }

    // ---- after-action, bow, battle-action, engage, resolution and other windows ----------------------------------------------------------------------------------------------
    private static void HizumiBowWatch()
    {
        const string ability = "hizumi-negate-straighten";
        var def = Def(ability);
        MatchEngine Rig(bool act, out CardInstance hizumi, out CardInstance bowed)
        {
            var s = Table(Phase.Battle, 0);
            var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            bowed = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            hizumi = Add(s, def, 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 0, Bow, bower.Id, bowed.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var hizumi, out var target);
        var command = InWindow(e, "bowReaction", 1, ability, hizumi.Id, null, target.Id);
        Accept(e, command);
        Eq(e.State.Cards[target.Id].StraightenBlockedTurn, e.State.TurnNumber, "his next straightening this turn is negated");
        var control = Rig(true, out _, out var t2); Settle(control);
        Check(control.State.Cards[t2.Id].StraightenBlockedTurn != control.State.TurnNumber, "declined, nothing is blocked");
        var quiet = Rig(false, out var h3, out _);
        NeverOpened(quiet, 1, ability, h3.Id, command with { CardId = h3.Id, PlayerId = 1 });
        // His Battle action: the battlefield's province gets +4 strength.
        var s = Table(Phase.Battle, 0);
        var hz = Add(s, def, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        var before = b.ProvinceStrengthForTest(1, 0);
        Accept(b, Enabled(b, 0, "hizumi-province-raise", hz.Id).Command!);
        Eq(b.ProvinceStrengthForTest(1, 0), before + 4, "his Battle action gave the province +4 strength");
    }

    private static void TsukimiReflexes()
    {
        const string ability = "tsukimi-battlefield";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool own, out CardInstance tsukimi, out CardInstance victim)
        {
            var s = Table(Phase.Battle, own ? 0 : 1);
            tsukimi = Add(s, def, 0, Zone.Home);
            victim = Add(s, "training-crane-samurai", own ? 1 : 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", own ? 0 : 1, Zone.Battlefield, province: 0);
            return act ? Open(s, own ? 0 : 1, Bow, bower.Id, victim.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, false, out var tsukimi, out _);
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, tsukimi.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[tsukimi.Id].Zone, Zone.Battlefield, "moved to the current battlefield");
        var mine = Rig(true, true, out var t2, out _);
        Check(!CanUse(mine, 0, ability, t2.Id), "only another player's Battle action");
        var quiet = Rig(false, false, out var t3, out _);
        NeverOpened(quiet, 0, ability, t3.Id, command with { CardId = t3.Id });
        // Her Melee 6 Attack is a Battle action of her own.
        var s = Table(Phase.Battle, 0);
        var tsu = Add(s, def, 0, Zone.Battlefield, province: 0);
        var six = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Force(six, 6 - CardCatalog.Get(six.DefinitionId).Force);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 0, "tsukimi-attack", tsu.Id, six.Id)); Settle(b);
        Eq(b.State.Cards[six.Id].Zone, Zone.Dead, "Melee 6 destroyed a Force 6 target");
    }

    private static void HawadoMoves()
    {
        const string ability = "hawado-exp-move-battlefield";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool atBattle, out CardInstance hawado, out CardInstance performer)
        {
            var s = Table(Phase.Battle, 1); s.UnresolvedBattlefields = [0, 1];
            hawado = atBattle ? Add(s, def, 0, Zone.Battlefield, province: 0) : Add(s, def, 0, Zone.Home);
            var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            performer = Add(s, "training-force-seven-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, performer.Id, victim.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var hawado, out var performer);
        var option = Options(e, 0, ability, hawado.Id).FirstOrDefault(o => o.Enabled);
        Check(option is not null && option.Provinces!.SequenceEqual([1]), "another unresolved battlefield is the destination");
        InWindow(e, "battleActionReaction", 0, ability, hawado.Id);
        Accept(e, option!.Command! with { Province = 1 }); Settle(e);
        Check(e.State.Cards[performer.Id].Zone == Zone.Battlefield && e.State.Cards[performer.Id].ProvinceIndex == 1, "the performer moved to the other battlefield");
        var away = Rig(true, false, out var h2, out _);
        Check(!CanUse(away, 0, ability, h2.Id) && !CanUse(away, 0, "hawado-exp-move-home", h2.Id), "Hawado at home is not at the current battlefield");
        Reject(away, new GameCommand("useAbility", 0, away.Version, CardId: h2.Id, AbilityId: ability, Province: 1));
        var quiet = Rig(false, true, out var h3, out _);
        NeverOpened(quiet, 0, ability, h3.Id, option.Command! with { CardId = h3.Id, Province = 1 });
    }

    private static void DeceptionRevealed()
    {
        const string ability = "deception-revealed-straighten";
        var def = Def(ability);
        var chukage = Def("chukage-draw");
        MatchEngine Rig(bool act, out CardInstance card, out CardInstance ring)
        {
            var s = Table(Phase.Action, 0);
            var actor = Add(s, chukage, 0, Zone.Home);
            ring = Add(s, "training-ring-of-air", 0, Zone.Home);
            card = Add(s, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 0, "chukage-draw", actor.Id, ring.Id));
            return e;
        }
        var e = Rig(true, out var card, out var ring);
        Check(e.State.Cards[ring.Id].Bowed, "the action bowed the Ring");
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, card.Id, null, ring.Id);
        Accept(e, command);
        Check(!e.State.Cards[ring.Id].Bowed, "it was straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was discarded");
        var control = Rig(true, out _, out var r2); Settle(control);
        Check(control.State.Cards[r2.Id].Bowed, "declined, the Ring stays bowed");
        var quiet = Rig(false, out var c3, out _);
        NeverOpened(quiet, 0, ability, c3.Id, command with { CardId = c3.Id });
        // Its Battle action: -4F, and it bows if its player controls a Ring.
        var s = Table(Phase.Battle, 0);
        var card2 = Add(s, def, 0, Zone.Hand);
        Add(s, "training-ring-of-air", 0, Zone.Home);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        var force = b.CardForceForTest(foe.Id);
        Accept(b, Offer(b, 0, "deception-revealed-weaken", card2.Id, foe.Id));
        Eq(b.CardForceForTest(foe.Id), force - 4, "-4F");
        Check(b.State.Cards[foe.Id].Bowed, "bowed, because its player controls a Ring");
    }

    private static void FeignDeath()
    {
        const string ability = "feign-death-action";
        var def = Def(ability);
        MatchEngine Rig(bool act, out CardInstance feign, out CardInstance lost)
        {
            var s = Table(Phase.Battle, 1);
            feign = Add(s, def, 0, Zone.Hand);
            lost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
            var probe = MatchEngine.FromTestState(s);
            Force(lost, 2 - probe.CardForceForTest(lost.Id));
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "training-honor-cost-attack", strategy.Id, lost.Id));
            return e;
        }
        var e = Rig(true, out var feign, out var lost);
        Eq(e.State.Cards[lost.Id].Zone, Zone.Dead, "the Ranged Attack destroyed him");
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, feign.Id, null, lost.Id);
        var honor = Honor(e, 0);
        Accept(e, command);
        var back = e.State.Cards[lost.Id];
        Check(back.Zone == Zone.Home && back.Bowed && back.Dishonorable, "he is back in his home, bowed and dishonored");
        Eq(Honor(e, 0), honor - 2, "lose 2 Honor");
        Eq(e.State.Cards[feign.Id].Zone, Zone.FateDiscard, "Feign Death is spent");
        var control = Rig(true, out _, out var l2); Settle(control);
        Eq(control.State.Cards[l2.Id].Zone, Zone.Dead, "declined, he stays dead");
        var quiet = Rig(false, out var f3, out _);
        NeverOpened(quiet, 0, ability, f3.Id, command with { CardId = f3.Id });
    }

    private static void TachikazeCollect()
    {
        const string ability = "w739-tachikaze-collect";
        var def = Def(ability);
        var furugaroDef = Def("furugaro-charge");
        MatchEngine Rig(bool act, out CardInstance tachikaze, out CardInstance furugaro)
        {
            var s = Table(Phase.Action, 0);
            tachikaze = Add(s, def, 0, Zone.Home);
            var fudo = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Fudo");
            furugaro = Add(s, furugaroDef, 0, Zone.Attached, parent: fudo.Id);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 0, "furugaro-charge", furugaro.Id, fudo.Id));
            return e;
        }
        var e = Rig(true, out var tachikaze, out var furugaro);
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, tachikaze.Id, null, furugaro.Id);
        Accept(e, command);
        Eq(e.State.Cards[furugaro.Id].Zone, Zone.Underneath, "Furugaro is under the Tachikaze");
        Check(e.State.Cards[furugaro.Id].FaceUp && e.State.Cards[furugaro.Id].HostCardId == tachikaze.Id, "face up, under it");
        var control = Rig(true, out _, out var f2); Settle(control);
        Check(control.State.Cards[f2.Id].Zone != Zone.Underneath, "declined, it stays in the discard pile");
        var quiet = Rig(false, out var t3, out var f3);
        NeverOpened(quiet, 0, ability, t3.Id, command with { CardId = t3.Id, TargetId = f3.Id });
        // Its Limited action attaches a Follower from under it, paying its cost.
        var s = Table(Phase.Action, 0);
        var holding = Add(s, def, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var under = Add(s, "training-follower", 0, Zone.FateDiscard);
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var f = MatchEngine.FromTestState(s);
        f.PutUnder(under.Id, holding.Id, true);
        var g = MatchEngine.FromTestState(f.State);
        var attach = Options(g, 0, "w739-tachikaze-limited", holding.Id).First(o => o.Enabled).Command! with { Selections = [Pick("attachment", under.Id), Pick("personality", host.Id)] };
        Accept(g, attach);
        Eq(g.State.Cards[under.Id].AttachedTo, host.Id, "the Follower was attached from under the Holding");
    }

    private static void ToResolution(MatchEngine e)
    {
        for (var i = 0; i < 60 && e.State.PendingChoice?.Type != "afterResolutionReaction" && e.State.Phase == Phase.Battle; i++)
        {
            var pass = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, "the battle can pass to its resolution");
            Accept(e, pass!.Command!);
        }
    }

    // Player 1's Force 20 raider beats player 0's lone defender, who is destroyed; `reactor` (a card in hand) answers the resolution.
    private static MatchEngine Resolution(string reactorDef, Action<MatchState, CardInstance>? tweakVictim = null, string victimDef = "training-crane-samurai", bool victimDies = true)
    {
        var s = Table(Phase.Battle, 1);
        var victim = Add(s, victimDef, 0, Zone.Battlefield, province: 0);
        tweakVictim?.Invoke(s, victim);
        var raider = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Force(raider, victimDies ? 20 : -20);
        if (CardCatalog.Get(reactorDef).Type == CardType.Strategy) Add(s, reactorDef, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        ToResolution(e);
        return e;
    }

    private static void CrippledSensei()
    {
        const string ability = "crippled-sensei-create-battle";
        var def = Def(ability);
        var e = Resolution(def);
        var sensei = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        var command = InWindow(e, "afterResolutionReaction", 0, ability, sensei.Id);
        Accept(e, command);
        Eq(e.State.Cards.Values.Count(c => c.DefinitionId == "user-scan-maimed-retainer"), 1, "Maimed Retainer was created");
        var ok = Resolution(def, victimDies: false);
        Check(ok.State.PendingChoice?.Type != "afterResolutionReaction", "no Personality destroyed, no window");
        var s = Table(Phase.Battle, 1);
        var sensei2 = Add(s, def, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var quiet = MatchEngine.FromTestState(s);
        NeverOpened(quiet, 0, ability, sensei2.Id, command with { CardId = sensei2.Id });
        Check(AbilityCatalog.All["crippled-sensei-focus"].Timing == "FocusEffect", "its other ability is a Focus Effect, not a Battle/Open action");
    }

    private static void RokutaBattle()
    {
        const string ability = "rokuta-battle-create";
        var def = Def(ability);
        var e = Resolution(def, null, def);
        var rokuta = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        Eq(rokuta.Zone, Zone.Dead, "the resolution destroyed Rokuta");
        var option = Options(e, 0, ability, rokuta.Id).FirstOrDefault(o => o.Enabled && o.Command!.TargetPlayerId == 1);
        Check(option is not null, "Rokuta's Reaction is offered naming a player: " + string.Join("; ", e.QueryActions(0).Select(a => a.Type + ":" + a.Label + "[" + a.Enabled + "] " + a.Reason)));
        var command = InWindow(e, "afterResolutionReaction", 0, ability, rokuta.Id);
        var honor = Honor(e, 1);
        Accept(e, option!.Command!);
        Eq(Honor(e, 1), honor - 3, "the chosen player lost 3 Honor");
        Check(e.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Home && c.Id != rokuta.Id && CardCatalog.Cards.TryGetValue(c.DefinitionId, out var d) && d.CreatedOnly), "an Apprentice Personality was created in his home");
        var survived = Resolution(def, null, def, victimDies: false);
        Check(survived.State.PendingChoice?.Type != "afterResolutionReaction", "Rokuta survived: no window");
        Check(command.AbilityId == ability);
    }

    private static void WordsHaveStrength()
    {
        const string ability = "words-strength-resolution";
        var def = Def(ability);
        var e = Resolution(def, (s, v) => { Kw(v, "Courtier"); });
        var word = e.State.Cards.Values.Single(c => c.DefinitionId == def);
        var option = Options(e, 0, ability, word.Id).FirstOrDefault(o => o.Enabled);
        Check(option is not null, "offered after a Courtier was destroyed");
        var command = InWindow(e, "afterResolutionReaction", 0, ability, word.Id);
        var honor = Honor(e, 1);
        Accept(e, command with { TargetPlayerId = 1 });
        Eq(Honor(e, 1), honor - 3, "the chosen player lost 3 Honor");
        var plain = Resolution(def);
        Check(plain.State.PendingChoice?.Type != "afterResolutionReaction", "only a plain Samurai was destroyed: no window");
        var s = Table(Phase.Battle, 1);
        var word2 = Add(s, def, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var quiet = MatchEngine.FromTestState(s);
        NeverOpened(quiet, 0, ability, word2.Id, command with { CardId = word2.Id, TargetPlayerId = 1 });
        Check(AbilityCatalog.All["words-strength-action"].Timing == "Reaction", "its other ability is the twin Reaction after a Battle action, not a Battle/Open action");
    }

    private static void StockpiledResources()
    {
        const string ability = "stockpiled-attach";
        var def = Def(ability);
        MatchEngine Rig(bool engage, out CardInstance card, out CardInstance actor, out CardInstance item)
        {
            var t = Table(Phase.BattleSelection, 0); t.PriorityPlayerId = 0; t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
            t.Cards[t.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; for (var i = 0; i < 4; i++) Add(t, "training-marketplace", 0, Zone.Home);
            actor = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            item = Add(t, "training-costly-weapon", 0, Zone.Hand); card = Add(t, def, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(t);
            if (engage) Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            return e;
        }
        var e = Rig(true, out var card, out var actor, out var item);
        var command = InWindow(e, "engageReaction", 0, ability, card.Id, null, item.Id) with { PerformerId = actor.Id };
        Accept(e, command);
        Eq(e.State.Cards[item.Id].AttachedTo, actor.Id, "attached to the performing Personality");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var quiet = Rig(false, out var c2, out var a2, out var i2);
        NeverOpened(quiet, 0, ability, c2.Id, command with { CardId = c2.Id, PerformerId = a2.Id, TargetId = i2.Id });
        // Not in an ordinary Battle phase either: the battle already begun, the Engage window is gone.
        var s = Table(Phase.Battle, 0);
        var card3 = Add(s, def, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-costly-weapon", 0, Zone.Hand);
        var battle = MatchEngine.FromTestState(s);
        Check(!CanUse(battle, 0, ability, card3.Id), "not offered once the battle is under way");
        Check((CardCatalog.Get(def).Abilities ?? []).All(a => AbilityCatalog.All[a].Timing == "Reaction"), "Stockpiled Resources prints only Reactions");
    }

    private static void QalyarRecon()
    {
        const string ability = "qalyar-recon-minus";
        var def = Def(ability);
        MatchEngine Engaging(bool engage, bool home, out CardInstance qalyar)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            qalyar = home ? Add(s, def, 0, Zone.Home) : Add(s, def, 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            if (engage) Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            return e;
        }
        var e = Engaging(true, false, out var qalyar);
        var before = e.GetView(0).Players[1].Provinces[0].Strength;
        var command = InWindow(e, "engageReaction", 0, ability, qalyar.Id);
        Accept(e, command);
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Qalyar's controller has Reconnaissance at the battlefield");
        Eq(e.GetView(0).Players[1].Provinces[0].Strength, before - 2, "the battlefield's province has -2 strength");
        var away = Engaging(true, true, out var q2);
        Check(!CanUse(away, 0, ability, q2.Id), "a Qalyar at home cannot react to the engagement");
        var quiet = Engaging(false, false, out var q3);
        NeverOpened(quiet, 0, ability, q3.Id, command with { CardId = q3.Id });
        Check((CardCatalog.Get(def).Abilities ?? []).All(a => AbilityCatalog.All[a].Timing == "Reaction"), "Qalyar prints only Reactions");
    }

    private static void ForgingDestiny()
    {
        const string ability = "forging-destiny-negate";
        var def = Def(ability);
        var festivalDef = CardCatalog.Cards.Values.First(c => c.Name == "Cherry Blossom Festival").Id;
        MatchEngine Rig(bool withForging, out CardInstance festival, out CardInstance? forging)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
            festival = Add(s, festivalDef, 1, Zone.Province, province: 3); festival.FaceUp = false;
            if (s.Players[1].Provinces[3].CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
            s.Players[1].Provinces[3].CardId = festival.Id;
            forging = withForging ? Add(s, def, 0, Zone.Hand) : null;
            var e = MatchEngine.FromTestState(s);
            RunUntil(e, () => e.State.PendingChoice is { Type: "p3BeforeEventReaction" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
            return e;
        }
        var e = Rig(true, out var festival, out var forging);
        var command = InWindow(e, "p3BeforeEventReaction", 0, ability, forging!.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[festival.Id].Zone, Zone.DynastyDiscard, "the negated Event was discarded without its effects");
        Eq(e.State.Cards[forging.Id].Zone, Zone.FateDiscard, "Forging Destiny is spent");
        var none = Rig(false, out _, out _);
        Check(none.State.PendingChoice?.Type != "p3BeforeEventReaction", "no Forging Destiny held: no window");
        // Its Battle action: the performer moves home and his unit straightens. Not offered as a Reaction with no Event.
        var b = Table(Phase.Battle, 0);
        var card = Add(b, def, 0, Zone.Hand);
        var lead = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var m = MatchEngine.FromTestState(b);
        Check(!CanUse(m, 0, ability, card.Id), "not offered in an ordinary Battle phase");
        Accept(m, Enabled(m, 0, "forging-destiny-move", card.Id, lead.Id).Command!);
        Eq(m.State.Cards[lead.Id].Zone, Zone.Home, "the performer moved home");
    }

    private static void NoTimeForGames()
    {
        const string ability = "no-time-for-games-negate";
        var def = Def(ability);
        MatchEngine Rig(bool lead, bool act, out CardInstance card, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1);
            s.BattleNumber = 1; s.FirstActionPlayerId = 1;
            if (lead) s.AttackerLeadBattle = 1;
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            card = Add(s, def, 0, Zone.Hand);
            return act ? Open(s, 1, Bow, bower.Id, victim.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var card, out var victim);
        var command = InWindow(e, "announceReaction", 0, ability, card.Id);
        Accept(e, command); Settle(e);
        Check(!e.State.Cards[victim.Id].Bowed, "the Attacker's Battle action was negated");
        var nolead = Rig(false, true, out var c2, out var v2);
        Check(nolead.State.PendingChoice?.Type != "announceReaction" && !CanUse(nolead, 0, ability, c2.Id), "without the Attacker's lead nothing is offered");
        Check(nolead.State.Cards[v2.Id].Bowed, "and the action resolved");
        var quiet = Rig(true, false, out var c3, out _);
        NeverOpened(quiet, 0, ability, c3.Id, command with { CardId = c3.Id });
        // Its Open action: rehonor a dishonorable performer.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var dishonored = Add(s, "training-lion-samurai", 0, Zone.Home); dishonored.Dishonorable = true;
        var card4 = Add(s, def, 0, Zone.Hand);
        var o = MatchEngine.FromTestState(s);
        Accept(o, Use(o, 0, "no-time-for-games-rehonor", card4.Id, performerId: dishonored.Id));
        Check(!o.State.Cards[dishonored.Id].Dishonorable, "rehonored");
    }

    private static void GovernorsEstate()
    {
        const string ability = "w821-governor-create";
        var def = Def(ability);
        (MatchEngine E, CardInstance Governor) Rig(bool owned)
        {
            var s = Table(Phase.Action, 0);
            CardInstance? governor = null;
            if (owned) { governor = Add(s, def, 0, Zone.Stronghold); s.Players[0].StrongholdId = governor.Id; }
            for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
            return (MatchEngine.FromTestState(s), governor!);
        }
        void ToDynasty(MatchEngine e)
        {
            for (var guard = 0; guard < 8 && e.State.Phase != Phase.Dynasty; guard++)
                Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(o => o.Type == "pass" && o.Enabled).Command!);
            Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase began");
        }
        var (e, governor) = Rig(true);
        var faceDown = e.State.Players[0].Provinces[0].CardId!;
        // In the Action phase, with the Dynasty Phase not begun, it is not offered.
        Check(!CanUse(e, 0, ability, governor.Id), "not offered in the Action phase");
        ToDynasty(e);
        var command = InWindow(e, Wave821Abilities.DynastyWindow, 0, ability, governor.Id, null, faceDown);
        command = command with { SourceIds = e.PaymentOptions(0, 2).First().SourceIds };
        Accept(e, command);
        Eq(e.State.Cards[faceDown].Zone, Zone.RemovedFromGame, "the card is removed from the game");
        var estate = e.State.Cards.Values.Single(c => CardCatalog.Cards.TryGetValue(c.DefinitionId, out var d) && d.CreatedOnly && d.Name.Contains("Estate"));
        Check(estate.Zone == Zone.Home && estate.Bowed && estate.OwnerId == 0, "a bowed Imperial Estate was created in his home");
        var (plain, _) = Rig(false);
        ToDynasty(plain);
        Check(plain.State.PendingChoice is null, "no window without the Estate");
        // Its Limited action: gain 2 Honor.
        var (limited, gov2) = Rig(true);
        var honor = Honor(limited, 0);
        Accept(limited, Enabled(limited, 0, "w821-governor-honor", gov2.Id).Command!);
        Eq(Honor(limited, 0), honor + 2, "gains 2 Honor");
    }

    private static void LibraryOfRebirth()
    {
        const string ability = "library-rebirth-gold";
        const string Swell = "oracle-l5r-8366", FireShugenja = "oracle-l5r-592", SmallFarm = "oracle-l5r-7303";
        var def = Def(ability);
        (MatchEngine E, CardInstance Library, CardInstance Fire, CardInstance Spell) Rig(Phase phase, bool libraryDef)
        {
            var s = Table(phase, phase == Phase.Battle ? 1 : 0); s.PriorityPlayerId = 0;
            var library = s.Cards[s.Players[0].StrongholdId]; if (libraryDef) library.DefinitionId = def; library.Bowed = true;
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Id != library.Id && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            Add(s, SmallFarm, 0, Zone.Home);
            var inBattle = phase == Phase.Battle;
            var fire = Add(s, FireShugenja, 0, inBattle ? Zone.Battlefield : Zone.Home, province: inBattle ? 0 : null);
            if (inBattle) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var spell = Add(s, Swell, 0, Zone.Hand);
            return (MatchEngine.FromTestState(s), library, fire, spell);
        }
        // During the Combat Segment the bowed Library pays 4 of the Spell's cost.
        var (e, library, fire, spell) = Rig(Phase.Battle, true);
        var equip = Enabled(e, 0, "equip-battle", performerId: fire.Id);
        Check(!Listed(e, 0, ability).Any(a => a.Enabled) && !Listed(e, 1, ability).Any(a => a.Enabled), "never a stand-alone Reaction offer");
        Accept(e, equip.Command! with { TargetId = spell.Id });
        Eq(e.State.Cards[spell.Id].AttachedTo, fire.Id, "the Spell was equipped, paid with the Library's 4 Gold");
        Check(e.State.Cards[library.Id].Bowed, "the Library stays bowed");
        // Without the Library the same position cannot pay.
        var (no, _, nf, _) = Rig(Phase.Battle, false);
        Check(!no.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == nf.Id), "without the Library the Spell is not affordable");
        // Outside the Combat Segment (the Action phase) the Library produces nothing.
        var (open, _, _, openSpell) = Rig(Phase.Action, true);
        Check(!open.QueryActions(0).Any(a => a.Type == "equip" && a.CardId == openSpell.Id && a.Enabled), "in the Action phase the Library's Reaction is not a payment source");
        // Its Battle action: straighten a Shugenja.
        var s2 = Table(Phase.Battle, 0);
        var lib2 = s2.Cards[s2.Players[0].StrongholdId]; lib2.DefinitionId = def;
        var shugenja = Add(s2, FireShugenja, 0, Zone.Battlefield, province: 0, bowed: true);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s2);
        Accept(b, Enabled(b, 0, "library-rebirth-straighten", lib2.Id, shugenja.Id).Command!);
        Check(!b.State.Cards[shugenja.Id].Bowed, "the Shugenja was straightened");
    }

    private static void NearMissGold()
    {
        const string ability = "pay-near-miss-gold";
        var def = Def(ability);
        MatchEngine Poor(out CardInstance host, out CardInstance item, out CardInstance miss)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            host = Add(s, "training-crane-samurai", 0, Zone.Home); item = Add(s, "training-item", 0, Zone.Hand); miss = Add(s, def, 0, Zone.Hand);
            Add(s, def, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Poor(out var host, out var item, out var miss);
        var missId = miss.Id + "#near-miss";
        var attach = new PaymentSubject(item, PaymentKind.Equip, Wielder: host);
        Check(e.PaymentSourcesForTest(0, attach).Any(x => x.Id == missId && x.Produced == 2), "in hand it is worth 2 Gold for an attachment");
        Check(!e.PaymentSourcesForTest(1, attach).Any(x => x.Id.EndsWith("#near-miss", StringComparison.Ordinal) && x.Id.StartsWith(miss.Id, StringComparison.Ordinal)), "never a source for the other player");
        Check(!CanUse(e, 0, ability, miss.Id) && !CanUse(e, 1, ability, miss.Id), "never a stand-alone Reaction offer");
        foreach (var kind in new[] { PaymentKind.Recruit, PaymentKind.Play, PaymentKind.Action })
            Check(!e.PaymentSourcesForTest(0, new PaymentSubject(item, kind)).Any(x => x.Id == missId), $"not a source for {kind}");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, TargetId: host.Id, SourceIds: [missId]));
        Eq(e.State.Cards[item.Id].AttachedTo, host.Id, "the attachment was paid for");
        Eq(e.State.Cards[miss.Id].Zone, Zone.FateDiscard, "choosing it discarded Near Miss");
        // Its other Reaction (negate an attachment's destruction) is a before-action Reaction of its own.
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var weapon = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: wielder.Id);
        var miss2 = Add(s, def, 0, Zone.Hand);
        var killer = Add(s, "training-card-bower", 1, Zone.Battlefield, province: 0);
        Check(killer.Id.Length > 0 && weapon.Id.Length > 0 && miss2.Id.Length > 0 && AbilityCatalog.All["pay-near-miss-negate"].Trigger == "targeted-by-action", "the twin Reaction is the targeted-by-action negation, covered by its own tests");
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-reaction-1", [
        ("Togashi Hizumi: negate a bowed Personality's next straightening, only in the bow window; his Battle province +4 works", HizumiBowWatch),
        ("Shiba Tsukimi: move to the battlefield after another player's Battle action, only in that window; her Melee 6 Attack works", TsukimiReflexes),
        ("Shosuro Hawado: move a performer to another unresolved battlefield after a Battle action, only in the battle-action window and only at the battlefield", HawadoMoves),
        ("Deception Revealed: straighten a Ring an action bowed, only in the after-action window; its Battle -4F works", DeceptionRevealed),
        ("Feign Death: bring back a Personality an action destroyed, only in the after-action window", FeignDeath),
        ("The Tachikaze: put a destroyed Follower under it, only in the after-action window; its Limited attach works", TachikazeCollect),
        ("Crippled Sensei: create Maimed Retainer after a battle destroyed a Personality of yours, only in the after-resolution window", CrippledSensei),
        ("Shosuro Rokuta: create an Apprentice and make a player lose 3 Honor after a battle destroyed him, only in the after-resolution window", RokutaBattle),
        ("Words Have Strength: a player loses 3 Honor after a battle destroyed your Courtier, only in the after-resolution window", WordsHaveStrength),
        ("Stockpiled Resources: attach from hand after engaging, only in the engage window", StockpiledResources),
        ("Qalyar: Reconnaissance and -2 province strength after engaging at her battlefield, only in the engage window and only at the battlefield", QalyarRecon),
        ("Forging Destiny: negate an Event before it resolves, only in the before-Event window; its Battle move works", ForgingDestiny),
        ("No Time for Games: negate an early Attacker Battle action, only in the announce window and only with the Attacker's lead; its Open rehonor works", NoTimeForGames),
        ("The Governor's Estate: create an Imperial Estate after the Dynasty Phase begins, only in that window; its Limited Honor works", GovernorsEstate),
        ("Library of Rebirth: 4 Gold when paying for a Spell in the Combat Segment, never a stand-alone Reaction; its Battle straighten works", LibraryOfRebirth),
        ("Near Miss: 2 Gold from hand when paying for an attachment only, never a stand-alone Reaction", NearMissGold),
        ("Champion of Thunder / O-Yoroi Troops: negate an action's effects on a card in the unit, only in the before-action window and only for a unit card; the Ranged 6 Attack works", ChampionOfThunder),
        ("Hida Kisada: redirect a Battle action aimed at him to another Personality at his location, only in the before-action window; his destroy action works", KisadaRedirect),
        ("Hundred-Fold Cut: destroy a Weapon to save a targeted Kensai, only in the before-action window; its Battle draw works", HundredFoldCut),
        ("Kakita Kae: discard the Imperial Favor to negate a Battle action's bowing, only in the before-action window and only with the Favor; her move-home works", KaeFavor),
        ("Request Authorization: negate a movement to the battlefield, only in the before-action window; its Battle move works", RequestAuthorization),
        ("Tamori Tamoko: negate a movement, only in the before-action window; her Open action out of turn works", TamokoNegates),
        ("The Dark Naga: bow a Forest to negate his destruction, only in the before-action window and only with a Forest; his Battle take works", DarkNagaForest),
        ("The Loyalty of the Scorpion: destroy a performing Scorpion to negate a movement, only in the before-action window for a Scorpion target", LoyaltyOfTheScorpion),
        ("Ward of Air: destroy it to negate a Shugenja's destruction by an Attack, only in the attack window; its Battle move home works", WardOfAir),
        ("Yasuki Jinn-Kuen: pay 2 Gold to negate an action on a Merchant, only in the before-action window; his Open straighten works", JinnKuenNegate),
        ("Yoritomo Sakuma: negate a Political action unless its player pays 2 Gold, only in the before-action window and only for Political actions", SakumaPolitical),
        ("Solid Defense: negate an Honor loss from another player's action, only in the Honor-change window; its Battle move works", SolidDefense),
        ("Effortless Counterattack: negate an additional Battle action, only in the additional-action window; its Battle move works", EffortlessCounterattack),
        ("A Close Call: negates a Ranged or Melee Attack on an Armored Personality in the attack window only; its Battle +2F still works", ACloseCall),
        ("Isawa Kaname: +2 to a Ranged Attack only (never Melee), in the attack window only; his Ranged Attack action works", KanameAttackBoost),
        ("Moshi Umiko: +2 to a Ranged Attack only (never Melee), in the attack window only; her -2 Reaction is independent", UmikoAttackBoost),
        ("Precision: +2 to a Ranged or Melee Attack in the attack window only; its Ranged 3 Attack action works", PrecisionBoost),
        ("Strong Defenses: -3 to a Ranged or Melee Attack in the attack window only; its Battle +5F still works", StrongDefenses),
        ("Trusting Instinct: -2 to a Ranged or Melee Attack in the attack window only; its Battle move still works", TrustingInstinct),
        ("Training attack reactor: negates a pending Attack in the attack window only; its boost Reaction is independent", TrainingAttackNegate),
        ("Akodo Tsudoken: his entering-play trait (lose 3 Honor) is offered to his controller in the entry window only; his straighten action is independent", TsudokenEntry),
        ("Asako Izuna: the three entering-play Reactions are offered to his controller only in the entry window, dishonor and keyword the target, and share one use", IzunaEntry),
        ("Asako Kikugoro: the entering-play look at the top two Dynasty cards is offered to his controller in the entry window only", KikugoroEntry),
        ("Kitsuki Horume: gain/lose 1 Honor for either chosen player, offered to his controller in the entry window only", HorumeEntry),
        ("Kitsune Yuko: Invest 2 Gold for 1 Honor in the entry window only; her Limited bow still works", YukoEntry),
        ("Iweko Seiken: the entering-play recruit of a cheaper non-Unique Samurai only in the entry window; his Open favor action is independent", SeikenEntry),
        ("Celestial Sword of the Crane: 2 Honor on entering play, only in the entry window; its Open action works after", CraneSwordEntry),
        ("Celestial Sword of the Scorpion: choose a player to lose 2 Honor on entering play, only in the entry window; its Battle bow works independently", ScorpionSwordEntry),
    ]);
}
