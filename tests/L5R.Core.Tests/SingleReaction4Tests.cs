using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #833, single-Reaction audit slice 4: cards printing exactly one Reaction. Each test opens the real trigger window through ordinary engine play
// (a purchase, an equip, an engage, a Battle action ...), proves the Reaction is offered enabled to its own player only, accepted with the printed
// effect checked against the card text, and then not offered (and its raw command refused) once the window is closed or never opened.
internal static partial class SingleReaction4Tests
{
    public static void Run(TestRunner runner) => runner.Run("single-reaction-4", [
        ("Chuda Seki: after he enters play the Reaction gives any Personality a -1C Plague token, to his controller only, once, and only in his entry window", SekiPlague),
        ("Kitsu Kagako: after she enters play the Reaction creates a 2F/2C/3PH Lion Ancestor Spirit at home and gains 1 Honor", KagakoCreate),
        ("Kitsune Gina: after she enters play the Reaction creates the Mantis Fox, whose trait gives enemy attackers and their Followers -1F at its battlefield", GinaCreate),
        ("Kitsune Tokoru: after she enters play the Reaction creates the 4F/2C/3PH Bear, whose Battle ability bows an attacking enemy card and destroys an attachment", TokoruCreate),
        ("Togashi Meiyu: after she enters play the Reaction gives her three +1F Fire tokens", MeiyuTokens),
        ("Bayushi Mugoshi: after he enters play the Political Reaction makes the chosen player (either) lose 1 Honor", MugoshiSlander),
        ("Shinjo Itao: after he enters play the Reaction attaches a Follower from his controller's hand to him, paying all costs", ItaoAttach),
        ("Vanquisher's Blades: after it enters play (equipped) the Reaction creates and attaches a +2F/+1C Weapon to its Personality", VanquishersBlades),
        ("Shard of the Great Death's Bones: on entering play (equip) the trait loses 2 Honor and creates three 1F Raptor Followers dealt to the chosen Personalities", ShardCreate),
        ("Training item with entry reaction: equipping the Item opens its entry window, whose Reaction gains 1 Honor for its controller only, once", TrainingItemEntry),
        ("Iuchi Shaocheng: after he enters play the Reaction turns a province card face up and discards a non-Unique Personality, which becomes honorably dead", ShaochengTurn),
        ("Daidoji Yuki: the Recon Reaction after engaging at his battlefield, if all his controller's Personalities there are Scouts, gives Reconnaissance and the first Battle opportunity (attacking or defending)", YukiRecon),
        ("Dragon's Breath Castle: after engaging at one of its controller's provinces (even bowed) the Attacker bows one of his unbowed Personalities there, for the Defender only", DragonsBreathBow),
        ("Shosuro Toson: after engaging at his battlefield the Reaction discards the province's card, and a discarded Personality gives the first Battle action, performed by Toson", TosonEngage),
        ("Scouting the Pass: the Attacker's Reaction after engaging with an unbowed Scout gives Reconnaissance, a private look at two cards of the Defender's hand and the Scout-only first action", ScoutingPass),
        ("Steady Resolve (and its Training twin): before a battle resolves, either side's performing opposed Samurai at the battlefield gets +2F, only for its controller", SteadyResolve),
        ("The Bestial Ones: the Engage Reaction of the attached Follower gives a target enemy Personality or Follower -2F, to its controller in the Engage window only", BestialEngage),
        ("The Snow Has Teeth: after engaging, either player's Reaction caps the province's strength at base, negates the next move of its controller's Personality and destroys the next Terrain", SnowHasTeeth),
        ("Training Engage honor: the Engage Reaction with no first-action claim gains 1 Honor for its player only, once", TrainingEngageHonor),
        ("Ancestral Protection: after an action targets your Personality, destroying your performing Ancestor negates the action's effects (targeted player only)", AncestralProtection),
        ("Undetectable Enemy: after another player's action targets your Oni the Reaction negates the action's effects", UndetectableEnemy),
        ("Iron Will: after another player's Battle action by a lower Personal Honor Personality targets your Paragon, costs are ignored, effects negated and an additional Battle action granted", IronWill),
        ("Moshi Ira: after an action targets a card in his unit the performer gets -3F once the action has resolved", IraWeaken),
        ("Waystation of the Path: after another player's action targets your Henshin another Personality receives his attachments and tokens", WaystationReborn),
        ("Footsteps of Madness: paying the Stronghold's base Gold Production before an action from a Stronghold resolves negates its effects", FootstepsOfMadness),
        ("Training untargeted-action negator: before another player's action resolves (whatever it targets) its effects are negated, for the other player's actions only", TrainingNegateUntargeted),
        ("Advanced Intelligence: after a Recon Strategy's action resolves, one copy per battle draws a card per Recon Strategy action taken, for its player only", AdvancedIntelligence),
        ("Asako Chukage: even bowed, after an action moved your Personality he moves to that Personality's location and his unit straightens", ChukageFollow),
        ("Blade of Perfection: after another player's action destroyed it or its Personality it is attached to a performing Personality, paying its costs", BladeOfPerfection),
        ("Grief: after an action destroyed a Personality, dishonors a Personality of the same player who loses Honor equal to its base Personal Honor, one copy per resolution", GriefDishonor),
        ("Hida Bakishi: after his action destroyed a card at the battlefield, the next action targeting him this turn by another player has its effects on him negated", BakishiShield),
        ("Hida Yamadera: from the dead pile, after another player's Battle action destroyed him, a Melee 8 Attack against the destroyer", YamaderaAttack),
        ("Ikoma Ayumu: even bowed, after you announce a Tactical action while he is at the current battlefield, he straightens", AyumuStraighten),
        ("Kakita Maratai: after a duel your Personality won, bowing her gains 2 Honor and takes the Imperial Favor, once", MaratayStatue),
        ("Quagmire: after an action bowed a Personality at this province or an adjacent one, bowing the Region negates his next straightening this turn", QuagmireNegate),
        ("Yung: after one of your Rings enters play, bowing him searches the Fate deck for a Ring and puts it in hand", YungSearch),
        ("Retribution: after an Attack Phase in which you were the Defender, an additional Attack Phase with one assigned Personality who gets +1F", RetributionDeclare),
        ("Mastering the Elements: after it enters play, paying the Spell's base Gold Cost, it permanently copies the Spell's keywords, traits and base abilities, once", MasteringCopy),
        ("No Hiding Place: after you announce a Ranged Attack, -1 strength and a card in the Defender's home may be targeted", NoHidingPlace),
        ("Lookout: after you announce a Ranged Attack, destroying the Follower lets that one Attack target a card in the Defender's home", LookoutDestroy),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static string AnyDef(string ability) =>
        CardCatalog.Cards.Values.Where(c => (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }

    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
            Accept(e, e.State.PendingChoice!.Type == "overconfidentDraw" ? new GameCommand("overconfidentDraw", e.State.PendingChoice.PlayerId, e.Version, Choice: "decline")
                : e.State.PendingChoice.Type == "attackTargetSelect" ? e.QueryActions(e.State.PendingChoice.PlayerId).First(a => a.Type == "chooseAttackTarget" && a.Command?.Choice == "none").Command!
                : new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // Declines the announce window only, leaving whatever follows it (an Attack's target selection) open.
    private static void CloseAnnounce(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "announceReaction" } && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner, listed for nobody else, refused if the other player submits it; on a copy where the
    // window is declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`).
    private static GameCommand InWindow(MatchEngine e, string windowType, int owner, string ability, string cardId, string? performerId = null, string? targetId = null)
    {
        Check(e.State.PendingChoice?.Type == windowType || windowType == "entryReaction" && e.State.PendingChoice?.Type == "proclaim", ability + ": the window is " + e.State.PendingChoice?.Type);
        if (e.State.PriorityPlayerId != owner && e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner} (pending {e.State.PendingChoice?.Type}, priority {e.State.PriorityPlayerId}; options: {string.Join("|", options.Select(o => o.Enabled + " " + o.Reason))})" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
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

    // A position in which the card stands in play and no window was ever opened: not offered to anyone, and its command is refused.
    private static void NeverOpened(MatchEngine e, int owner, string ability, string cardId, GameCommand command)
    {
        Check(e.State.PendingChoice is null, "a window is open");
        Check(!CanUse(e, owner, ability, cardId) && !CanUse(e, 1 - owner, ability, cardId), ability + " is offered with no window open");
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = owner });
    }

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

    // The same card standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, out CardInstance card, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        card = Add(s, def, 0, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    // An Item equipped in the Action phase from hand, leaving its entering-play window open.
    private static (MatchEngine E, CardInstance Item, CardInstance Host) Equip(string def, string? keyword, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        if (keyword is not null) Kw(host, keyword);
        var item = Add(s, def, 0, Zone.Hand);
        Gold(s, 0, 6);
        tweak?.Invoke(s, host);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == item.Id);
        Check(equip.Command is not null, "equip offered: " + equip.Reason);
        Accept(e, equip.Command! with { TargetId = host.Id });
        Check(e.State.PendingChoice?.Type is "entryReaction", $"{def} equipped, but the window is {e.State.PendingChoice?.Type}");
        return (e, item, host);
    }

    private static CardInstance[] Created(MatchEngine e, HashSet<string> before) => e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();

    // ---- entering-play windows ----------------------------------------------------------------------------------------------------------------------------------------------
    private static void SekiPlague()
    {
        const string ability = "seki-plague";
        var def = Def(ability, "Chuda Seki");
        CardInstance enemy = null!, own = null!, follower = null!;
        var (e, id) = Buy(def, s => { enemy = Add(s, "training-lion-samurai", 1, Zone.Home); own = Add(s, "training-crane-samurai", 0, Zone.Home); follower = Add(s, "training-follower", 1, Zone.Home); });
        var command = InWindow(e, "entryReaction", 0, ability, id, null, enemy.Id);
        var targets = Enabled(e, 0, ability, id).TargetIds!;
        Check(targets.Contains(own.Id) && targets.Contains(id) && targets.Contains(enemy.Id), $"any Personality, either side: {string.Join(",", targets)} vs {own.Id} {id} {enemy.Id}");
        Check(!targets.Contains(follower.Id), "not a Follower");
        var chi = e.PersonalityChiForTest(enemy.Id);
        Accept(e, command);
        Check(e.State.Cards[enemy.Id].Tokens.Any(t => t.Type == "Plague" && t.Stat == "Chi" && t.Amount == -1), "a -1C Plague token");
        Eq(e.PersonalityChiForTest(enemy.Id), chi - 1, "-1 Chi");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        var h = AtHome(def, out var seki);
        NeverOpened(h, 0, ability, seki.Id, command with { CardId = seki.Id });
    }

    private static void KagakoCreate()
    {
        const string ability = "kagako-create";
        var def = Def(ability, "Kitsu Kagako");
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var before = e.State.Cards.Keys.ToHashSet(); var honor = Honor(e, 0); var other = Honor(e, 1);
        Accept(e, command);
        var ancestor = Created(e, before).Single();
        var made = CardCatalog.Get(ancestor.DefinitionId);
        Check(made.CreatedOnly && made.Type == CardType.Personality && made.Force == 2 && made.Chi == 2 && made.PersonalHonor == 3
            && new HashSet<string>(made.Keywords!).SetEquals(["Lion Clan", "Samurai", "Ancestor", "Spirit"]), "a 2F/2C/3PH Lion Clan Samurai Ancestor Spirit Personality");
        Check(ancestor.OwnerId == 0 && ancestor.Zone == Zone.Home, "created at home for her controller");
        Eq(Honor(e, 0), honor + 1, "gained 1 Honor"); Eq(Honor(e, 1), other, "the opponent gained nothing");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        var h = AtHome(def, out var kagako);
        NeverOpened(h, 0, ability, kagako.Id, command with { CardId = kagako.Id });
    }

    private static void GinaCreate()
    {
        const string ability = "gina-create";
        var def = Def(ability, "Kitsune Gina");
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var fox = Created(e, before).Single();
        var made = CardCatalog.Get(fox.DefinitionId);
        Check(made.CreatedOnly && made.Type == CardType.Personality && made.Force == 2 && made.Chi == 2 && made.PersonalHonor == 3
            && new HashSet<string>(made.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Fox", "Spirit"]), "a 2F/2C/3PH Mantis Clan Nonhuman Fox Spirit Personality");
        Check(fox.OwnerId == 0 && fox.Zone == Zone.Home, "created at home for her controller");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        // The Fox's trait: enemy attacking Personalities and Followers at its battlefield have -1F.
        MatchState Fight(bool withFox)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-follower", 1, Zone.Attached, parent: a.Id);
            if (withFox) Add(s, fox.DefinitionId, 0, Zone.Battlefield, province: 0);
            return s;
        }
        var plain = MatchEngine.FromTestState(Fight(false)); var weak = MatchEngine.FromTestState(Fight(true));
        var attacker = plain.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var follower = plain.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Attached);
        Eq(weak.CardForceForTest(attacker.Id), plain.CardForceForTest(attacker.Id) - 1, "the attacking Personality has -1F");
        Eq(weak.CardForceForTest(follower.Id), plain.CardForceForTest(follower.Id) - 1, "and so does his Follower");
        var h = AtHome(def, out var gina);
        NeverOpened(h, 0, ability, gina.Id, command with { CardId = gina.Id });
    }

    private static void TokoruCreate()
    {
        const string ability = "tokoru-create";
        var def = Def(ability, "Kitsune Tokoru");
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var bear = Created(e, before).Single();
        var made = CardCatalog.Get(bear.DefinitionId);
        Check(made.CreatedOnly && made.Type == CardType.Personality && made.Force == 4 && made.Chi == 2 && made.PersonalHonor == 3
            && new HashSet<string>(made.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Bear", "Spirit"]), "a 4F/2C/3PH Mantis Clan Nonhuman Bear Spirit Personality");
        Check(bear.OwnerId == 0 && bear.Zone == Zone.Home, "created at home for her controller");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        // The Bear's ability: "Battle: Target an attacking enemy card: Bow it. Destroy it if it is an attachment."
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var cub = Add(s, bear.DefinitionId, 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: attacker.Id);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 0, "tokoru-bear-bow", cub.Id, attacker.Id));
        Check(b.State.Cards[attacker.Id].Bowed && b.State.Cards[attacker.Id].Zone == Zone.Battlefield, "an attacking Personality is bowed, not destroyed");
        var c = MatchEngine.FromTestState(s);
        Accept(c, Offer(c, 0, "tokoru-bear-bow", cub.Id, item.Id));
        Check(c.State.Cards[item.Id].Zone != Zone.Attached && c.State.Cards[item.Id].AttachedTo is null, "an attachment is destroyed");
        var h = AtHome(def, out var tokoru);
        NeverOpened(h, 0, ability, tokoru.Id, command with { CardId = tokoru.Id });
    }

    private static void MeiyuTokens()
    {
        const string ability = "meiyu-fire-tokens";
        var def = Def(ability, "Togashi Meiyu");
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var tokens = e.State.Cards[id].Tokens;
        Eq(tokens.Count, 3, "three tokens");
        Check(tokens.All(t => t.Type == "Fire" && t.Stat == "Force" && t.Amount == 1), "each a +1F Fire token");
        Eq(e.CardForceForTest(id), CardCatalog.Get(def).Force + 3, "printed Force plus three");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        var h = AtHome(def, out var meiyu);
        NeverOpened(h, 0, ability, meiyu.Id, command with { CardId = meiyu.Id });
        Eq(h.State.Cards[meiyu.Id].Tokens.Count, 0, "no tokens without the entry");
    }

    private static void MugoshiSlander()
    {
        const string ability = "mugoshi-slander";
        var def = Def(ability, "Bayushi Mugoshi");
        foreach (var victim in new[] { 0, 1 })
        {
            var (e, id) = Buy(def);
            var offers = Options(e, 0, ability, id).Where(o => o.Enabled).ToArray();
            Check(offers.Select(o => o.Command!.TargetPlayerId).OrderBy(x => x).SequenceEqual(new int?[] { 0, 1 }), "one option per player");
            InWindow(e, "entryReaction", 0, ability, id);
            var command = offers.Single(o => o.Command!.TargetPlayerId == victim).Command!;
            var h0 = Honor(e, 0); var h1 = Honor(e, 1);
            Accept(e, command with { ExpectedVersion = e.Version });
            Eq(Honor(e, 0), victim == 0 ? h0 - 1 : h0, "player 0 Honor");
            Eq(Honor(e, 1), victim == 1 ? h1 - 1 : h1, "player 1 Honor");
            Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        }
        var h = AtHome(def, out var mugoshi);
        Check(!CanUse(h, 0, ability, mugoshi.Id) && !CanUse(h, 1, ability, mugoshi.Id), "not offered at home");
        Reject(h, new GameCommand("useAbility", 0, h.Version, CardId: mugoshi.Id, AbilityId: ability, TargetPlayerId: 1));
    }

    private static void ItaoAttach()
    {
        const string ability = "itao-attach";
        var def = Def(ability, "Shinjo Itao");
        CardInstance follower = null!, item = null!, theirs = null!;
        var (e, id) = Buy(def, s => { follower = Add(s, "training-follower", 0, Zone.Hand); item = Add(s, "training-item", 0, Zone.Hand); theirs = Add(s, "training-follower", 1, Zone.Hand); });
        var targets = Enabled(e, 0, ability, id).TargetIds!;
        Check(targets.Contains(follower.Id) && !targets.Contains(item.Id) && !targets.Contains(theirs.Id), "a Follower in your hand only: " + string.Join(",", targets));
        var command = InWindow(e, "entryReaction", 0, ability, id, null, follower.Id);
        var gold = Enabled(e, 0, ability, id).Gold;
        Eq(gold?.Cost, CardCatalog.Get("training-follower").GoldCost, "paying all costs");
        Accept(e, command);
        Check(e.State.Cards[follower.Id].Zone == Zone.Attached && e.State.Cards[follower.Id].AttachedTo == id, "the Follower is attached to Itao");
        Check(!e.State.Players[0].Hand.Contains(follower.Id) && e.State.Players[0].Hand.Contains(item.Id), "it left the hand; the Item stayed");
        var h = AtHome(def, out var itao, (s, c) => Add(s, "training-follower", 0, Zone.Hand));
        NeverOpened(h, 0, ability, itao.Id, command with { CardId = itao.Id });
    }

    private static void VanquishersBlades()
    {
        const string ability = "vanquishers-blades-create";
        var def = Def(ability, "Vanquisher's Blades");
        var (e, blades, host) = Equip(def, "Kensai");
        var command = InWindow(e, "entryReaction", 0, ability, blades.Id);
        var force = e.CardForceForTest(host.Id); var chi = e.PersonalityChiForTest(host.Id);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var weapon = Created(e, before).Single();
        var made = CardCatalog.Get(weapon.DefinitionId);
        Check(made.CreatedOnly && made.Type == CardType.Item && (made.Keywords ?? []).Contains("Weapon"), "a Weapon Item");
        Check(weapon.Zone == Zone.Attached && weapon.AttachedTo == host.Id, "attached to this Personality");
        Eq(e.CardForceForTest(host.Id), force + 2, "+2F"); Eq(e.PersonalityChiForTest(host.Id), chi + 1, "+1C");
        Check(!CanUse(e, 0, ability, blades.Id), "taken once for this entry");
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var wielder = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Kensai");
        var inPlay = Add(s, def, 0, Zone.Attached, parent: wielder.Id);
        var h = MatchEngine.FromTestState(s);
        NeverOpened(h, 0, ability, inPlay.Id, command with { CardId = inPlay.Id });
    }

    private static void ShardCreate()
    {
        const string ability = "proxy677-shard-create";
        var def = AnyDef(ability);
        var (e, shard, host) = Equip(def, null, (s, h) => { Add(s, "training-crane-samurai", 0, Zone.Home); });
        var other = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && c.Id != host.Id);
        var command = Use(e, 0, ability, shard.Id, null, null, Pick("hosts", host.Id, other.Id));
        Check(e.State.PendingChoice?.Type == "entryReaction", "the entry window is open");
        Check(!CanUse(e, 1, ability, shard.Id), "not listed for the other player");
        Reject(e, command with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, shard.Id), "not offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, command);
        var birds = Created(e, before);
        Eq(birds.Length, 3, "three Followers");
        Check(birds.All(x => x.Zone == Zone.Attached && CardCatalog.Get(x.DefinitionId).Force == 1 && CardCatalog.Get(x.DefinitionId).Type == CardType.Follower
            && new HashSet<string>(CardCatalog.Get(x.DefinitionId).Keywords!).SetEquals(["Nonhuman", "Raptor", "Shadowlands", "Undead", "Cavalry"])), "each a 1F Nonhuman Raptor Shadowlands Undead Cavalry Follower");
        Check(birds.All(x => x.AttachedTo == host.Id || x.AttachedTo == other.Id), "attached to the chosen Personalities of the controller");
        Check(!CanUse(e, 0, ability, shard.Id), "once");
    }

    private static void TrainingItemEntry()
    {
        const string ability = "training-item-entry-reaction";
        var (e, item, _) = Equip("training-item-entry-reaction", null);
        var command = InWindow(e, "entryReaction", 0, ability, item.Id);
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, command);
        Eq(Honor(e, 0), h0 + 1, "gained 1 Honor"); Eq(Honor(e, 1), h1, "the opponent unchanged");
        Check(!CanUse(e, 0, ability, item.Id), "once");
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
        var inPlay = Add(s, "training-item-entry-reaction", 0, Zone.Attached, parent: wielder.Id);
        NeverOpened(MatchEngine.FromTestState(s), 0, ability, inPlay.Id, command with { CardId = inPlay.Id });
    }

    private static void ShaochengTurn()
    {
        const string ability = "wlpt-shaocheng-turn";
        var def = AnyDef(ability);
        string victimId = "";
        var (e, id) = Buy(def, s =>
        {
            var lion = s.Cards[s.Players[1].Provinces[1].CardId!]; lion.DefinitionId = "training-lion-samurai"; lion.FaceUp = false; victimId = lion.Id;
        });
        var command = InWindow(e, "entryReaction", 0, ability, id, null, victimId);
        var targets = Enabled(e, 0, ability, id).TargetIds!;
        Check(targets.Contains(victimId) && !targets.Contains(id), "a card in a province, face down, not a card in play");
        Check(!e.State.Cards[victimId].FaceUp, "the target starts face down");
        Accept(e, command);
        Eq(e.State.Cards[victimId].Zone, Zone.Dead, "a non-Unique Personality is discarded and becomes dead");
        Check(!e.State.Cards[victimId].Dishonorable, "honorably dead");
        Check(e.State.Players[1].Provinces[1].CardId is { } refill && refill != victimId, "the province is refilled as after any discard from it");
        // A Unique Personality and a Holding are only turned face up.
        var unique = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && (c.Keywords ?? []).Contains("Unique") && !c.ScenarioOnly).Id;
        foreach (var (turned, label) in new[] { (unique, "a Unique Personality"), ("training-farm", "a Holding") })
        {
            string cardId = "";
            var (f, sid) = Buy(def, s => { var c = s.Cards[s.Players[1].Provinces[2].CardId!]; c.DefinitionId = turned; c.FaceUp = false; cardId = c.Id; });
            var turn = InWindow(f, "entryReaction", 0, ability, sid, null, cardId);
            Accept(f, turn);
            Check(f.State.Cards[cardId].FaceUp && f.State.Cards[cardId].Zone == Zone.Province, $"{label} is turned face up and stays");
        }
        var h = AtHome(def, out var shaocheng);
        NeverOpened(h, 0, ability, shaocheng.Id, command with { CardId = shaocheng.Id });
    }

    // ---- engage windows -----------------------------------------------------------------------------------------------------------------------------------------------------
    // `attacker` engages province 0 of the other player; the Engage window is left open (the Attacker holds the first opportunity in it).
    private static MatchEngine Engaged(int attacker, Action<MatchState> arrange, bool window = true)
    {
        var s = Table(Phase.BattleSelection, attacker);
        s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", attacker, e.Version, Province: 0));
        Check(!window || e.State.PendingChoice?.Type == "engageReaction", "the Engage window is open: " + e.State.PendingChoice?.Type);
        return e;
    }

    private static CardInstance SetProvince(MatchState s, int player, int index, string def)
    {
        var p = s.Players[player].Provinces[index];
        if (p.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        var c = Add(s, def, player, Zone.Province, province: index);
        p.CardId = c.Id;
        return c;
    }

    private static void YukiRecon()
    {
        const string ability = "yuki-engage-recon";
        var def = Def(ability, "Daidoji Yuki");
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance yuki = null!;
            var e = Engaged(0, s =>
            {
                yuki = Add(s, def, owner, Zone.Battlefield, province: 0);
                Add(s, "training-scout-samurai", owner, Zone.Battlefield, province: 0);
                Add(s, "training-crane-samurai", 1 - owner, Zone.Battlefield, province: 0);
            });
            var command = InWindow(e, "engageReaction", owner, ability, yuki.Id);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            Check(!e.State.Players[1].Provinces[0].Reconnaissance[1 - owner], "and the other player does not");
            Settle(e);
            Eq(e.State.FirstActionPlayerId, owner, "Yuki's controller has the first opportunity to take a Battle action or pass");
        }
        // Not every Personality of his controller there is a Scout: not offered.
        CardInstance y2 = null!;
        var mixed = Engaged(0, s => { y2 = Add(s, def, 0, Zone.Battlefield, province: 0); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); }, false);
        Check(!CanUse(mixed, 0, ability, y2.Id) && !CanUse(mixed, 1, ability, y2.Id), "a non-Scout Personality of his controller at the battlefield stops it");
        Reject(mixed, Use(mixed, 0, ability, y2.Id));
        // Yuki at home is not at the engaged battlefield.
        CardInstance y3 = null!;
        var home = Engaged(0, s => { y3 = Add(s, def, 0, Zone.Home); Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); }, false);
        Check(!CanUse(home, 0, ability, y3.Id), "at home he is not at the battlefield");
        Reject(home, Use(home, 0, ability, y3.Id));
        var h = AtHome(def, out var idle);
        NeverOpened(h, 0, ability, idle.Id, Use(h, 0, ability, idle.Id));
    }

    private static void DragonsBreathBow()
    {
        const string ability = "dbc-attacker-bow";
        var castleDef = Def(ability);
        foreach (var bowed in new[] { false, true })
        {
            CardInstance a = null!, b = null!, spent = null!, castle = null!;
            var e = Engaged(0, s =>
            {
                castle = s.Cards[s.Players[1].StrongholdId]; castle.DefinitionId = castleDef; castle.Bowed = bowed;
                a = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); b = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
                spent = Add(s, "training-crane-veteran", 0, Zone.Battlefield, bowed: true, province: 0);
                Add(s, "training-water-personality", 1, Zone.Battlefield, province: 0);
            });
            var command = InWindow(e, "engageReaction", 1, ability, castle.Id);
            Accept(e, command);
            Eq(e.State.PendingChoice?.Type, "deciderChoice", "the Attacker is asked" + (bowed ? " (even though the Castle is bowed)" : ""));
            Eq(e.State.PendingChoice?.PlayerId, 0, "the Attacker decides");
            var picks = e.QueryActions(0).Where(x => x.Type == "chooseDecider").Select(x => x.CardId).ToArray();
            Check(picks.Length == 2 && picks.Contains(a.Id) && picks.Contains(b.Id), "his unbowed Personalities at the battlefield only: " + string.Join(",", picks));
            Accept(e, new GameCommand("chooseDecider", 0, e.Version, TargetId: b.Id));
            Check(e.State.Cards[b.Id].Bowed && !e.State.Cards[a.Id].Bowed && e.State.Cards[spent.Id].Bowed, "the chosen Personality, and only it, was bowed");
            Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open again");
        }
        // A battle at the Attacker's own province: the Attacker's Castle is no Defender's province.
        CardInstance own = null!;
        var mine = Engaged(0, s =>
        {
            own = s.Cards[s.Players[0].StrongholdId]; own.DefinitionId = castleDef;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-water-personality", 1, Zone.Battlefield, province: 0);
        }, false);
        Check(!CanUse(mine, 0, ability, own.Id) && !CanUse(mine, 1, ability, own.Id), "an Attacker's own Castle does not react to his own attack");
        Reject(mine, Use(mine, 0, ability, own.Id));
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var idle = s2.Cards[s2.Players[1].StrongholdId]; idle.DefinitionId = castleDef;
        NeverOpened(MatchEngine.FromTestState(s2), 1, ability, idle.Id, new GameCommand("useAbility", 1, 0, idle.Id, AbilityId: ability));
    }

    private static void TosonEngage()
    {
        const string ability = "toson-exp2-engage";
        var def = Def(ability, "Shosuro Toson");
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance toson = null!, other = null!, enemy = null!, discarded = null!;
            var e = Engaged(0, s =>
            {
                toson = Add(s, def, owner, Zone.Battlefield, province: 0); other = Add(s, "training-battle-bower", owner, Zone.Battlefield, province: 0);
                enemy = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                discarded = SetProvince(s, 1, 0, "training-lion-courtier");
            });
            var command = InWindow(e, "engageReaction", owner, ability, toson.Id);
            Accept(e, command);
            Eq(e.State.Cards[discarded.Id].Zone, Zone.DynastyDiscard, "the province's Personality was discarded");
            Check(e.State.Players[1].Provinces[0].CardId is { } refill && refill != discarded.Id, "the province was refilled");
            Settle(e);
            Eq(e.State.FirstActionPlayerId, owner, "Toson's controller has the first opportunity");
            Eq(e.State.FirstActionRestriction?.PerformerId, toson.Id, "which Toson must perform");
            Check(Rejection(e, Use(e, owner, "training-bow-at-battle", other.Id, enemy.Id)).Contains("Shosuro Toson"), "another performer is refused, naming Toson");
            // A Holding is discarded too, but no opportunity is claimed.
            CardInstance holding = null!, toson2 = null!;
            var f = Engaged(0, s =>
            {
                toson2 = Add(s, def, owner, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                holding = SetProvince(s, 1, 0, "training-marketplace");
            });
            if (f.State.PriorityPlayerId != owner) Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
            Accept(f, Enabled(f, owner, ability, toson2.Id).Command!);
            Eq(f.State.Cards[holding.Id].Zone, Zone.DynastyDiscard, "the Holding was discarded");
            Settle(f);
            Eq(f.State.FirstActionPlayerId, 1, "no claim: the Defender acts first");
            Check(f.State.FirstActionRestriction is null, "and no performer limit");
        }
        CardInstance away = null!;
        var home = Engaged(0, s => { away = Add(s, def, 0, Zone.Home); Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); }, false);
        Check(!CanUse(home, 0, ability, away.Id), "at home he is not at the battlefield");
        Reject(home, Use(home, 0, ability, away.Id));
        var h = AtHome(def, out var idle);
        NeverOpened(h, 0, ability, idle.Id, Use(h, 0, ability, idle.Id));
    }

    private static void ScoutingPass()
    {
        const string ability = "p5-scouting-pass";
        var cardDef = Def(ability, "Scouting the Pass");
        string scout = "", card = "", other = "", enemy = "", sleeper = "";
        var e = Engaged(0, s =>
        {
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0).Id;
            sleeper = Add(s, "training-scout-samurai", 0, Zone.Battlefield, bowed: true, province: 0).Id;
            other = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0).Id;
            card = Add(s, cardDef, 0, Zone.Hand).Id;
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Id;
            Add(s, "training-crane-samurai", 1, Zone.Hand); Add(s, "training-lion-samurai", 1, Zone.Hand); Add(s, "training-follower", 1, Zone.Hand);
        });
        Check(!CanUse(e, 0, ability, card, sleeper), "a bowed Scout is no performer");
        Check(!CanUse(e, 0, ability, card, other), "a non-Scout is no performer");
        var command = InWindow(e, "engageReaction", 0, ability, card, scout);
        Accept(e, command);
        Eq(e.GetView(0).HandSights?.Length, 2, "the Attacker sees two cards of the Defender's hand");
        Check(e.GetView(1).HandSights is null, "the Defender's view carries nothing");
        Settle(e);
        Eq(e.State.FirstActionPlayerId, 0, "the Attacker has the first chance");
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance there");
        Check(!e.State.Players[1].Provinces[0].Reconnaissance[1], "for the Attacker only");
        Check(Rejection(e, Use(e, 0, "training-bow-at-battle", other, enemy)).Length > 0, "his first Battle action must be performed by that Scout");
        Eq(e.State.Cards[card].Zone, Zone.FateDiscard, "the Strategy was played");
        // The Defender may not play it.
        string dScout = "", dCard = "";
        var d = Engaged(0, s =>
        {
            Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            dScout = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0).Id; dCard = Add(s, cardDef, 1, Zone.Hand).Id;
        }, false);
        Check(!CanUse(d, 1, ability, dCard, dScout) && !CanUse(d, 0, ability, dCard, dScout), "the Defender may not play it");
        Reject(d, Use(d, 1, ability, dCard, performerId: dScout));
        var h = Table(Phase.Action, 0); h.PriorityPlayerId = 0;
        var hs = Add(h, "training-scout-samurai", 0, Zone.Home); var hc = Add(h, cardDef, 0, Zone.Hand);
        NeverOpened(MatchEngine.FromTestState(h), 0, ability, hc.Id, command with { CardId = hc.Id, PerformerId = hs.Id });
    }

    private static void SteadyResolve()
    {
        const string ability = "steady-resolve-boost";
        foreach (var def in new[] { Def(ability, "Steady Resolve"), "training-steady-resolve" })
            foreach (var owner in new[] { 0, 1 })
            {
                CardInstance mine = null!, theirs = null!, steady = null!;
                var s = Table(Phase.Battle, 1);
                mine = Add(s, "training-scout-samurai", owner, Zone.Battlefield, province: 0); theirs = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                steady = Add(s, def, owner, Zone.Hand);
                var e = MatchEngine.FromTestState(s);
                for (var guard = 0; guard < 8 && e.State.PendingChoice?.Type != "beforeBattleReaction"; guard++) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
                var command = InWindow(e, "beforeBattleReaction", owner, ability, steady.Id, mine.Id);
                Check(!CanUse(e, owner, ability, steady.Id, theirs.Id), "the opposing Samurai is no performer of yours");
                var force = e.CardForceForTest(mine.Id); var foe = e.CardForceForTest(theirs.Id);
                Accept(e, command);
                Eq(e.CardForceForTest(mine.Id), force + 2, $"{def}: +2F for player {owner}'s Samurai"); Eq(e.CardForceForTest(theirs.Id), foe, "and nobody else");
                Eq(e.State.Cards[steady.Id].Zone, Zone.FateDiscard, "the Strategy is discarded after it is played");
            }
        // A performer that is not a Samurai: no window ever opens and the card is not played.
        var t = Table(Phase.Battle, 1);
        Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(t, "training-steady-resolve", 0, Zone.Hand);
        var n = MatchEngine.FromTestState(t);
        for (var guard = 0; guard < 8 && n.State.PendingChoice is null && n.State.Phase == Phase.Battle; guard++) Accept(n, new GameCommand("pass", n.State.PriorityPlayerId, n.Version));
        Check(n.State.PendingChoice?.Type != "beforeBattleReaction" && !CanUse(n, 0, ability, card.Id), "no Samurai of yours: not offered");
        Eq(n.State.Cards[card.Id].Zone, Zone.Hand, "and still in hand");
    }

    private static void BestialEngage()
    {
        const string ability = "wl-bestial-engage";
        var def = Def(ability, "The Bestial Ones");
        foreach (var owner in new[] { 0, 1 })
            foreach (var hitFollower in new[] { false, true })
            {
                CardInstance follower = null!, foe = null!, foeFollower = null!, host = null!;
                var e = Engaged(0, s =>
                {
                    host = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                    follower = Add(s, def, owner, Zone.Attached, province: 0, parent: host.Id);
                    foe = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                    foeFollower = Add(s, "training-follower", 1 - owner, Zone.Attached, province: 0, parent: foe.Id);
                });
                var target = hitFollower ? foeFollower : foe;
                var command = InWindow(e, "engageReaction", owner, ability, follower.Id, null, target.Id);
                var targets = Enabled(e, owner, ability, follower.Id).TargetIds!;
                Check(targets.Contains(foe.Id) && targets.Contains(foeFollower.Id) && !targets.Contains(host.Id), "an enemy Personality or Follower only: " + string.Join(",", targets));
                var force = e.CardForceForTest(target.Id); var other = e.CardForceForTest((hitFollower ? foe : foeFollower).Id);
                Accept(e, command);
                Eq(e.CardForceForTest(target.Id), Math.Max(0, force - 2), "-2F on the target");
                Eq(e.CardForceForTest((hitFollower ? foe : foeFollower).Id), other, "and not on the other");
            }
        {
            // Rules lead (issue #838): Engage is battlefield-specific. A Follower whose Personality is at home is not offered the Reaction at a battlefield engage, and its penalty reaches only cards at that battlefield.
            CardInstance homeFollower = null!;
            var away = Engaged(0, s =>
            {
                Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                var homeHost = Add(s, "training-crane-samurai", 0, Zone.Home);
                homeFollower = Add(s, def, 0, Zone.Attached, parent: homeHost.Id);
                Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            }, window: false);
            Check(away.State.PendingChoice?.Type != "engageReaction" && !away.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId == ability), "the Follower's Personality is at home: not offered at a battlefield engage");
            CardInstance there = null!, absent = null!, bf = null!;
            var present = Engaged(0, s =>
            {
                var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                there = Add(s, def, 0, Zone.Attached, province: 0, parent: host.Id);
                bf = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                absent = Add(s, "training-lion-samurai", 1, Zone.Home);
            });
            var legal = Enabled(present, 0, ability, there.Id).TargetIds!;
            Check(legal.Contains(bf.Id) && !legal.Contains(absent.Id), "only a card at the engaged battlefield is a target: " + string.Join(",", legal));
            Reject(present, Use(present, 0, ability, there.Id, absent.Id));
        }
        var idle = Table(Phase.Action, 0); idle.PriorityPlayerId = 0;
        var ih = Add(idle, "training-crane-samurai", 0, Zone.Home); var it = Add(idle, def, 0, Zone.Attached, parent: ih.Id); var ie = Add(idle, "training-lion-samurai", 1, Zone.Home);
        NeverOpened(MatchEngine.FromTestState(idle), 0, ability, it.Id, Use(MatchEngine.FromTestState(idle), 0, ability, it.Id, ie.Id));
    }

    private static void SnowHasTeeth()
    {
        const string ability = "snow-has-teeth";
        var def = Def(ability, "The Snow Has Teeth");
        const string terrain = "oracle-l5r-2996"; // Hasty Fortifications
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance snow = null!, mover = null!, mover2 = null!, enemy = null!, hasty = null!;
            var e = Engaged(0, s =>
            {
                Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                mover = Add(s, "training-lion-veteran", owner, Zone.Battlefield, province: 0); mover2 = Add(s, "training-scout-samurai", owner, Zone.Battlefield, province: 0);
                enemy = Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                snow = Add(s, def, owner, Zone.Hand); hasty = Add(s, terrain, 0, Zone.Hand);
                s.Players[1].Provinces[0].Strength += 3;
            });
            var raised = e.ProvinceStrengthForTest(1, 0);
            var command = InWindow(e, "engageReaction", owner, ability, snow.Id);
            Accept(e, command);
            Eq(e.ProvinceStrengthForTest(1, 0), raised - 3, "capped at its base strength");
            Settle(e);
            // The next effect moving one of the controller's Personalities from its battlefield is negated; the one after is not; the other player's are never affected.
            var theirs = Edit(e, st => { });
            Check(theirs.MoveToBattlefieldForTest(enemy.Id, 1), "the other player's Personality is not covered");
            Check(!theirs.MoveToBattlefieldForTest(mover.Id, 1), "and that move did not use up the protection");
            var moved = Edit(e, st => { });
            Check(!moved.MoveToBattlefieldForTest(mover.Id, 1), "the first move is negated");
            Eq(moved.State.Cards[mover.Id].ProvinceIndex, 0, "he stays at the battlefield");
            Check(moved.MoveToBattlefieldForTest(mover2.Id, 1), "the next one happens");
            // The next Terrain to enter play this battle is destroyed.
            var play = Edit(e, st => st.PriorityPlayerId = 0);
            Accept(play, new GameCommand("playStrategy", 0, play.Version, hasty.Id));
            Eq(play.State.Cards[hasty.Id].Zone, Zone.FateDiscard, "the Terrain was destroyed as it entered play");
        }
        var h = Table(Phase.Action, 0); h.PriorityPlayerId = 0;
        var hs = Add(h, def, 0, Zone.Hand);
        var idle = MatchEngine.FromTestState(h);
        Check(!CanUse(idle, 0, ability, hs.Id) && !CanUse(idle, 1, ability, hs.Id), "not offered outside an Engage window");
        Reject(idle, Use(idle, 0, ability, hs.Id));
    }

    private static void TrainingEngageHonor()
    {
        const string ability = "training-engage-gain-honor";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance card = null!;
            var e = Engaged(0, s =>
            {
                Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                card = Add(s, AnyDef(ability), owner, Zone.Hand);
            });
            var command = InWindow(e, "engageReaction", owner, ability, card.Id);
            var h0 = Honor(e, 0); var h1 = Honor(e, 1);
            Accept(e, command);
            Eq(Honor(e, owner), (owner == 0 ? h0 : h1) + 1, "gained 1 Honor"); Eq(Honor(e, 1 - owner), owner == 0 ? h1 : h0, "the other player unchanged");
            Settle(e);
            Eq(e.State.FirstActionPlayerId, 1, "no first-action claim: the Defender acts first as usual");
            Check(!CanUse(e, owner, ability, card.Id), "the played card is spent");
        }
    }

    // ---- "after an action targets ..." / "before an action resolves" windows ---------------------------------------------------------------------------------------------
    // Player 1 (the Attacker, active) bows player 0's Personality with a Battle action: the targeted player's before-action window is held open.
    private static (MatchEngine E, CardInstance Bower, CardInstance Victim) Bowed(Action<MatchState, CardInstance, CardInstance> arrange)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        arrange(s, bower, victim);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, victim.Id));
        return (e, bower, victim);
    }

    private static void AncestralProtection()
    {
        const string ability = "ancestral-protection-negate";
        var def = Def(ability, "Ancestral Protection");
        CardInstance ancestor = null!, card = null!;
        var (e, _, victim) = Bowed((s, b, v) => { ancestor = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Ancestor"); card = Add(s, def, 0, Zone.Hand); });
        var command = InWindow(e, "beforeActionReaction", 0, ability, card.Id, ancestor.Id);
        Check(!CanUse(e, 0, ability, card.Id, victim.Id), "a Personality who is not an Ancestor cannot be the destroyed performer");
        Accept(e, command);
        Settle(e);
        Eq(e.State.Cards[ancestor.Id].Zone, Zone.Dead, "the performing Ancestor was destroyed");
        Check(!e.State.Cards[victim.Id].Bowed, "the action's effects were negated: the targeted Personality was not bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        // Without an Ancestor to destroy no window opens and the action goes through.
        var (n, _, v2) = Bowed((s, b, v) => Add(s, def, 0, Zone.Hand));
        Check(n.State.PendingChoice is null && n.State.Cards[v2.Id].Bowed, "no Ancestor: no window, the Personality is bowed");
        // "After an action targets your Personality" (any player's action, not only another player's): his own action opens it too.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var src = Add(t, "training-bow-any-source", 0, Zone.Home); var own = Add(t, "training-crane-samurai", 0, Zone.Home); var anc2 = Kw(Add(t, "training-crane-veteran", 0, Zone.Home), "Ancestor"); var card2 = Add(t, def, 0, Zone.Hand);
        var o = MatchEngine.FromTestState(t);
        Accept(o, Offer(o, 0, "training-bow-any", src.Id, own.Id));
        Eq(o.State.PendingChoice?.Type, "beforeActionReaction", "his own action targeting his Personality holds the window");
        Accept(o, Enabled(o, 0, ability, card2.Id, anc2.Id).Command!);
        Settle(o);
        Check(!o.State.Cards[own.Id].Bowed && o.State.Cards[anc2.Id].Zone == Zone.Dead, "negated by his own Reaction");
        // Nothing in hand and no action against him: never offered.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var idle = Add(s2, def, 0, Zone.Hand); var anc = Kw(Add(s2, "training-crane-veteran", 0, Zone.Home), "Ancestor");
        NeverOpened(MatchEngine.FromTestState(s2), 0, ability, idle.Id, Use(MatchEngine.FromTestState(s2), 0, ability, idle.Id, null, anc.Id));
    }

    private static void UndetectableEnemy()
    {
        const string ability = "undetectable-enemy-negate";
        var def = Def(ability, "Undetectable Enemy");
        CardInstance card = null!;
        var (e, _, oni) = Bowed((s, b, v) => { Kw(v, "Oni"); card = Add(s, def, 0, Zone.Hand); });
        var command = InWindow(e, "beforeActionReaction", 0, ability, card.Id);
        Accept(e, command);
        Settle(e);
        Check(!e.State.Cards[oni.Id].Bowed, "the action's effects were negated: the Oni was not bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "Undetectable Enemy was spent");
        // Not an Oni: no window, the effect happens.
        var (n, _, plain) = Bowed((s, b, v) => Add(s, def, 0, Zone.Hand));
        Check(n.State.PendingChoice is null && n.State.Cards[plain.Id].Bowed, "not an Oni: no window and he is bowed");
        // Not another player's action: the Oni's own player bows him.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var source = Add(t, "training-bow-any-source", 0, Zone.Home); var own = Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Oni"); var mine = Add(t, def, 0, Zone.Hand);
        var o = MatchEngine.FromTestState(t);
        Accept(o, Offer(o, 0, "training-bow-any", source.Id, own.Id));
        Check(!CanUse(o, 0, ability, mine.Id) && o.State.Cards[own.Id].Bowed, "his own player's action does not open it");
    }

    private static void IronWill()
    {
        const string ability = "iron-will-negate";
        var def = Def(ability, "Iron Will");
        var request = Def("request-authorization-move");
        MatchEngine Setup(int honorBonus, bool paragon, out CardInstance card, out CardInstance target, out CardInstance courier, out CardInstance act)
        {
            var s = Batch336Support.Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            card = Add(s, def, 0, Zone.Hand);
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (paragon) Kw(target, "Paragon");
            target.Modifiers.Add(new("test-fixture", "test-fixture", "PersonalHonor", honorBonus));
            courier = Kw(Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0), "Courtier"); // Personal Honor 3
            act = Add(s, request, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Setup(2, true, out var ironWill, out var paragon, out var courier, out var act);
        Accept(e, Offer(e, 1, "request-authorization-move", act.Id, paragon.Id, courier.Id));
        var command = InWindow(e, "beforeActionReaction", 0, ability, ironWill.Id);
        Accept(e, command);
        Settle(e);
        Eq(e.State.Cards[paragon.Id].Zone, Zone.Battlefield, "its effects were negated: the Paragon did not move");
        Check(!e.State.Cards[courier.Id].Bowed, "its costs were ignored: the Courtier did not bow");
        Eq(e.State.Cards[act.Id].Zone, Zone.FateDiscard, "the action was still taken (its Strategy is spent)");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 1), "that player may take an additional Battle action");
        // Equal Personal Honor: not lower.
        var eq = Setup(1, true, out _, out var p2, out var c2, out var a2);
        Accept(eq, Offer(eq, 1, "request-authorization-move", a2.Id, p2.Id, c2.Id));
        Check(eq.State.PendingChoice?.Type != "beforeActionReaction", "equal Personal Honor: no window");
        // Not a Paragon.
        var np = Setup(2, false, out _, out var p3, out var c3, out var a3);
        Accept(np, Offer(np, 1, "request-authorization-move", a3.Id, p3.Id, c3.Id));
        Check(np.State.PendingChoice?.Type != "beforeActionReaction", "not a Paragon: no window");
    }

    private static void IraWeaken()
    {
        const string ability = "wlpt-ira-weaken";
        var def = Def(ability, "Moshi Ira");
        foreach (var attached in new[] { false, true })
        {
            CardInstance ira = null!, bower = null!, target = null!;
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            ira = Add(s, def, 0, Zone.Battlefield, province: 0);
            bower = Add(s, Def("tsuji-bow"), 1, Zone.Battlefield, province: 0); Force(bower, 5);
            target = attached ? Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: ira.Id) : ira;
            var e = MatchEngine.FromTestState(s);
            var force = e.CardForceForTest(bower.Id);
            Accept(e, Offer(e, 1, "tsuji-bow", bower.Id, target.Id));
            var command = InWindow(e, "beforeActionReaction", 0, ability, ira.Id);
            Accept(e, command);
            Eq(e.CardForceForTest(bower.Id), force, "the -3F waits for the action to resolve");
            Settle(e);
            Check(e.State.Cards[target.Id].Bowed, "the action itself went on");
            Eq(e.CardForceForTest(bower.Id), force - 3, "the performer has -3F after the action resolved");
        }
        // A card outside his unit is not covered.
        var o = Table(Phase.Battle, 1); o.PriorityPlayerId = 1;
        var ira2 = Add(o, def, 0, Zone.Battlefield, province: 0); var buddy = Add(o, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var b2 = Add(o, Def("tsuji-bow"), 1, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 1, "tsuji-bow", b2.Id, buddy.Id));
        Check(!CanUse(oe, 0, ability, ira2.Id) && oe.State.PendingChoice is null, "a card in another unit does not open it");
        Reject(oe, Use(oe, 0, ability, ira2.Id));
    }

    private static void WaystationReborn()
    {
        const string ability = "waystation-reborn";
        var def = Def(ability, "Waystation of the Path");
        CardInstance henshin = null!, other = null!, plain = null!, weapon = null!, sh = null!;
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1;
        sh = s.Cards[s.Players[0].StrongholdId]; sh.DefinitionId = def;
        henshin = Add(s, "training-henshin-personality", 0, Zone.Home);
        other = Add(s, "training-crane-samurai", 0, Zone.Home); other.Bowed = true;
        weapon = Add(s, "training-weapon", 0, Zone.Attached, parent: henshin.Id);
        henshin.Tokens.Add(new CardToken("fx-token-1", "Fire", "test", "Force", 1));
        plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var source = Add(s, "training-bow-any-source", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-any", source.Id, henshin.Id));
        var command = InWindow(e, "beforeActionReaction", 0, ability, sh.Id, null, other.Id);
        var offered = Options(e, 0, ability, sh.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId).OrderBy(x => x).ToArray();
        Check(offered.SequenceEqual(new[] { other.Id, plain.Id }.OrderBy(x => x)), "one option per other Personality of the controller's, never the Henshin himself");
        Accept(e, command);
        Settle(e);
        Check(e.State.Cards[henshin.Id].Bowed, "the action's bow still happened to the Henshin");
        Check(e.State.Cards[weapon.Id].AttachedTo == other.Id && e.State.Cards[other.Id].Tokens.Any(t => t.Id == "fx-token-1") && e.State.Cards[henshin.Id].Tokens.Count == 0, "his attachments and tokens moved to the other Personality");
        // An action whose effects would destroy him (an Attack): he is overlaid onto the other Personality instead of dying.
        var b = Table(Phase.Battle, 1); b.PriorityPlayerId = 1;
        var wsh = b.Cards[b.Players[0].StrongholdId]; wsh.DefinitionId = def;
        var esh = b.Cards[b.Players[1].StrongholdId]; esh.DefinitionId = Def("otoro-attack");
        var hen = Bf(b, "training-henshin-personality", 0); var oth = Add(b, "training-crane-samurai", 0, Zone.Home); oth.Bowed = true;
        var wp = Add(b, "training-weapon", 0, Zone.Attached, province: 0, parent: hen.Id); hen.Tokens.Add(new CardToken("fx-token-2", "Fire", "test", "Force", 1));
        var ninja = Kw(Bf(b, "training-ninja-source", 1), "Ninja");
        var k = MatchEngine.FromTestState(b);
        Accept(k, Offer(k, 1, "otoro-attack", esh.Id, hen.Id, ninja.Id));
        var overlay = InWindow(k, "attackReaction", 0, ability, wsh.Id, null, oth.Id);
        Accept(k, overlay);
        Settle(k);
        var covering = k.State.Cards[hen.Id];
        Check(covering.Zone == Zone.Home && covering.Bowed, "the Henshin is not destroyed: he takes the other Personality's place and state (at home, bowed)");
        Eq(k.State.Cards[oth.Id].Zone, Zone.RemovedFromGame, "the other Personality's card is the one covered");
        Check(k.State.Cards[wp.Id].AttachedTo == hen.Id && covering.Tokens.Any(t => t.Id == "fx-token-2"), "his attachment and token are on the overlaying card");
        // A non-Henshin target does not open it; neither does the controller's own action.
        var t = Table(Phase.Action, 1); t.PriorityPlayerId = 1;
        var sh2 = t.Cards[t.Players[0].StrongholdId]; sh2.DefinitionId = def;
        var pl = Add(t, "training-crane-samurai", 0, Zone.Home); var src2 = Add(t, "training-bow-any-source", 1, Zone.Home);
        var n = MatchEngine.FromTestState(t);
        Accept(n, Offer(n, 1, "training-bow-any", src2.Id, pl.Id));
        Check(n.State.PendingChoice is null && !CanUse(n, 0, ability, sh2.Id), "a Personality that is not a Henshin: no window");
        var u = Table(Phase.Action, 0); u.PriorityPlayerId = 0;
        var sh3 = u.Cards[u.Players[0].StrongholdId]; sh3.DefinitionId = def;
        var h3 = Add(u, "training-henshin-personality", 0, Zone.Home); Add(u, "training-crane-samurai", 0, Zone.Home); var src3 = Add(u, "training-bow-any-source", 0, Zone.Home);
        var m = MatchEngine.FromTestState(u);
        Accept(m, Offer(m, 0, "training-bow-any", src3.Id, h3.Id));
        Check(m.State.PendingChoice is null && !CanUse(m, 0, ability, sh3.Id), "his controller's own action does not open it");
    }

    private static void FootstepsOfMadness()
    {
        const string ability = "footsteps-madness-negate";
        var def = Def(ability, "Footsteps of Madness");
        var estate = Def("khans-estate-draw");
        MatchEngine Build(bool canPay, out CardInstance card, out CardInstance stronghold, out CardInstance tactician)
        {
            var s = Table(Phase.Action, 1);
            stronghold = s.Cards[s.Players[1].StrongholdId]; stronghold.DefinitionId = estate; // base Gold Production 5
            tactician = Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Tactician");
            var deck = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, deck.Id);
            card = Add(s, def, 0, Zone.Hand);
            Add(s, "training-border-keep", 0, Zone.Home); Add(s, "training-border-keep", 0, Zone.Home);
            if (!canPay) foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var footsteps, out var stronghold, out var tactician);
        var hand = e.State.Players[1].Hand.Count;
        Accept(e, Enabled(e, 1, "khans-estate-draw", stronghold.Id, tactician.Id).Command!);
        var command = InWindow(e, "unimpeachableNameReaction", 0, ability, footsteps.Id);
        Eq(Enabled(e, 0, ability, footsteps.Id).Gold?.Cost, 5, "the price is the Stronghold's base Gold Production");
        Accept(e, command);
        Settle(e);
        Eq(e.State.Players[1].Hand.Count, hand, "the negated action drew no card");
        Check(e.State.Cards[stronghold.Id].AbilityUses.Count > 0, "the negated action was still taken: its use is spent");
        Eq(e.State.Cards[footsteps.Id].Zone, Zone.FateDiscard, "Footsteps of Madness was discarded");
        Check(e.State.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0 && c.Zone is Zone.Home or Zone.Stronghold).Any(c => c.Bowed), "the Gold was paid by bowing producers");
        // Unable to pay 5 Gold: no window, and the action resolves.
        var f = Build(false, out _, out var sh2, out var tact2);
        var hand2 = f.State.Players[1].Hand.Count;
        Accept(f, Enabled(f, 1, "khans-estate-draw", sh2.Id, tact2.Id).Command!);
        Check(f.State.PendingChoice is null, "with no Gold to pay no window opens");
        Eq(f.State.Players[1].Hand.Count, hand2 + 1, "the action resolved: a card was drawn");
        // An action not from a Stronghold: no window.
        var s3 = Table(Phase.Action, 1);
        var strategy = Add(s3, Def("forefather-honor"), 1, Zone.Hand); Kw(Add(s3, "training-lion-samurai", 1, Zone.Home), "Ancestor"); Add(s3, def, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(s3);
        Accept(g, Enabled(g, 1, "forefather-honor", strategy.Id).Command!);
        Check(g.State.PendingChoice is null, "an action on a Strategy is not an action from a Stronghold");
    }

    private static void TrainingNegateUntargeted()
    {
        const string ability = "training-negate-untargeted-action";
        var def = AnyDef(ability);
        var forefather = Def("forefather-honor");
        MatchEngine Build(int actor, out CardInstance negator, out CardInstance act)
        {
            var s = Table(Phase.Action, actor); s.PriorityPlayerId = actor;
            Kw(Add(s, "training-lion-samurai", actor, Zone.Home), "Ancestor");
            act = Add(s, forefather, actor, Zone.Hand);
            negator = Add(s, def, 1 - actor, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        foreach (var actor in new[] { 1, 0 })
        {
            var e = Build(actor, out var negator, out var act);
            var honor = Honor(e, actor);
            Accept(e, Enabled(e, actor, "forefather-honor", act.Id).Command!);
            var command = InWindow(e, "beforeActionReaction", 1 - actor, ability, negator.Id);
            Accept(e, command);
            Settle(e);
            Eq(Honor(e, actor), honor, "the untargeted action's effects were negated: no Honor gained");
            Eq(e.State.Cards[act.Id].Zone, Zone.FateDiscard, "the action was still taken");
        }
        // Another player's action only: the negator's own player's action opens nothing for him.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        Kw(Add(s2, "training-lion-samurai", 0, Zone.Home), "Ancestor"); var own = Add(s2, forefather, 0, Zone.Hand); var mine = Add(s2, def, 0, Zone.Home);
        var o = MatchEngine.FromTestState(s2);
        var h0 = Honor(o, 0);
        Accept(o, Enabled(o, 0, "forefather-honor", own.Id).Command!);
        Check(o.State.PendingChoice is null && !CanUse(o, 0, ability, mine.Id), "his own action opens no window for him");
        Check(Honor(o, 0) > h0, "and it resolves");
        Reject(o, Use(o, 0, ability, mine.Id));
    }

    // ---- "after an action resolves" and other windows -------------------------------------------------------------------------------------------------------------------
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, province: host.ProvinceIndex, parent: host.Id);
    private static void Grant(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));

    private static void AdvancedIntelligence()
    {
        const string ability = "advanced-intelligence-draw";
        var def = Def(ability, "Advanced Intelligence");
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var actor = Bf(s, "training-recon-actor", 0); Bf(s, "training-crane-veteran", 1);
        var first = Add(s, def, 0, Zone.Hand); var second = Add(s, def, 0, Zone.Hand);
        for (var i = 0; i < 4; i++) { var c = Add(s, "training-weapon", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, c.Id); }
        var e = MatchEngine.FromTestState(s);
        // A Recon action of a PERSONALITY is not a Recon Strategy's action: no window.
        Accept(e, Enabled(e, 0, "training-recon-attack", actor.Id).Command! with { TargetId = null });
        Check(e.State.PendingChoice is null && !CanUse(e, 0, ability, first.Id), "a Personality's Recon action opens nothing");
        Eq(e.State.ReconStrategyActionsThisBattle[0], 0, "and is not counted");
        // A Recon Strategy's action is counted and opens the window for its player only.
        var st = e.State; st.PriorityPlayerId = 0;
        var recon = Add(st, "training-recon-keyword-strategy", 0, Zone.Hand);
        recon.GrantedAbilities.Add(new("training-recon-attack", "test-fixture", GrantDuration.Permanent));
        var f = MatchEngine.FromTestState(st);
        Accept(f, Enabled(f, 0, "training-recon-attack", recon.Id).Command! with { TargetId = null });
        Eq(f.State.ReconStrategyActionsThisBattle[0], 1, "one Recon Strategy action this battle");
        var command = InWindow(f, "afterActionResolvedReaction", 0, ability, first.Id);
        var hand = f.State.Players[0].Hand.Count;
        Accept(f, command);
        Eq(f.State.Players[0].Hand.Count, hand - 1 + 1, "played (one card less) and one card drawn for the one Recon Strategy action");
        Eq(f.State.Cards[first.Id].Zone, Zone.FateDiscard, "the played copy is discarded");
        // Two Recon Strategy actions this battle: a second window, a second copy still refused, and the draw counts both.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        Bf(s2, "training-crane-veteran", 0); Bf(s2, "training-crane-veteran", 1);
        var copy = Add(s2, def, 0, Zone.Hand);
        var r1 = Add(s2, "training-recon-keyword-strategy", 0, Zone.Hand); Grant(r1, "training-recon-attack");
        var r2 = Add(s2, "training-recon-keyword-strategy", 0, Zone.Hand); Grant(r2, "training-recon-attack");
        for (var i = 0; i < 4; i++) { var c = Add(s2, "training-weapon", 0, Zone.FateDeck); s2.Players[0].FateDeck.Insert(0, c.Id); }
        var g = MatchEngine.FromTestState(s2);
        Accept(g, Enabled(g, 0, "training-recon-attack", r1.Id).Command! with { TargetId = null });
        Settle(g);
        for (var guard = 0; guard < 6 && g.State.PriorityPlayerId != 0 && g.State.PendingChoice is null; guard++) Accept(g, new GameCommand("pass", g.State.PriorityPlayerId, g.Version));
        if (CanUse(g, 0, "training-recon-attack", r2.Id))
        {
            Accept(g, Enabled(g, 0, "training-recon-attack", r2.Id).Command! with { TargetId = null });
            Eq(g.State.ReconStrategyActionsThisBattle[0], 2, "two Recon Strategy actions this battle");
            var handBefore = g.State.Players[0].Hand.Count;
            Accept(g, InWindow(g, "afterActionResolvedReaction", 0, ability, copy.Id));
            Eq(g.State.Players[0].Hand.Count, handBefore - 1 + 2, "one card drawn for each Recon Strategy action taken this battle");
        }
        else Check(false, "the second Recon Strategy action was not available");
        // One copy per battle: the second copy is refused in this battle.
        Check(!CanUse(f, 0, ability, second.Id), "a second copy this battle is not offered");
        Reject(f, Use(f, 0, ability, second.Id));
        // Out of any window it is not offered.
        var h = Table(Phase.Battle, 1); h.PriorityPlayerId = 0;
        var idle = Add(h, def, 0, Zone.Hand);
        NeverOpened(MatchEngine.FromTestState(h), 0, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void ChukageFollow()
    {
        const string ability = "chukage-follow";
        var def = Def(ability);
        const string halls = "oracle-l5r-2950";
        MatchEngine Build(int ownerOfChukage, bool chukageHome, out CardInstance attacker, out CardInstance chukage, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            s.Cards[s.Players[0].StrongholdId].DefinitionId = halls;
            Bf(s, "training-crane-samurai", 0);
            attacker = Bf(s, "training-lion-samurai", 1);
            chukage = Add(s, def, ownerOfChukage, chukageHome ? Zone.Home : Zone.Battlefield, bowed: true, province: chukageHome ? null : 0);
            follower = Add(s, "training-follower", ownerOfChukage, Zone.Attached, bowed: true, province: chukageHome ? null : 0, parent: chukage.Id);
            return MatchEngine.FromTestState(s);
        }
        // Player 0 sends the enemy attacker home with the Favor's Battle action; the attacker's controller's bowed Chukage follows him.
        var e = Build(1, false, out var attacker, out var chukage, out var follower);
        Accept(e, Offer(e, 0, "favor-battle", null!, attacker.Id));
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker was moved home");
        var command = InWindow(e, "afterActionResolvedReaction", 1, ability, chukage.Id, null, attacker.Id);
        Accept(e, command);
        Eq(e.State.Cards[chukage.Id].Zone, Zone.Home, "Chukage moved to the Personality's location");
        Check(!e.State.Cards[chukage.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "he moved, so his unit straightened (though he was bowed when he reacted)");
        // An enemy Personality was moved: his controller's Chukage has nothing to react to.
        var n = Build(0, true, out var enemyMoved, out var own, out _);
        Accept(n, Offer(n, 0, "favor-battle", null!, enemyMoved.Id));
        Check(!CanUse(n, 0, ability, own.Id) && !CanUse(n, 1, ability, own.Id), "an enemy Personality's move is not one of his controller's Personalities moving");
        // Never opened: no action has resolved.
        var h = Build(0, true, out _, out var idle, out _);
        NeverOpened(h, 0, ability, idle.Id, command with { CardId = idle.Id, PlayerId = 0 });
    }

    private static void BladeOfPerfection()
    {
        const string ability = "blade-perfection-attach";
        var blade = Def(ability, "Blade of Perfection");
        var daikyu = Def("tsuruchi-daikyu-attack");
        const string michitsu = "oracle-l5r-10060";
        MatchEngine Destroyed(bool personality, out CardInstance ally, out CardInstance b, out CardInstance w)
        {
            var d = Table(Phase.Battle, 1); d.PriorityPlayerId = 1;
            w = Bf(d, "training-crane-courtier", 0); b = Attach(d, blade, w);
            ally = Add(d, "training-crane-veteran", 0, Zone.Home); Gold(d, 0, 3);
            var attacker = Bf(d, "training-lion-samurai", 1);
            if (personality) { var bow = Attach(d, daikyu, attacker); var en = MatchEngine.FromTestState(d); Accept(en, Offer(en, 1, "tsuruchi-daikyu-attack", bow.Id, w.Id)); return en; }
            var m = Bf(d, michitsu, 1); var em = MatchEngine.FromTestState(d); Accept(em, Offer(em, 1, "michitsu-destroy", m.Id, b.Id)); return em;
        }
        var e = Destroyed(true, out var ally, out var blade3, out var wearer);
        Check(e.State.Cards[wearer.Id].Zone == Zone.Dead && e.State.Cards[blade3.Id].Zone == Zone.FateDiscard, "the wearer and his Blade are in the discard piles");
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, blade3.Id, ally.Id);
        var offer = Enabled(e, 0, ability, blade3.Id, ally.Id);
        Eq(offer.Gold?.Cost, 4, "paying all costs: its own Gold cost of 4");
        Accept(e, command);
        Check(e.State.Cards[blade3.Id].Zone == Zone.Attached && e.State.Cards[blade3.Id].AttachedTo == ally.Id, "it is attached to the chosen performing Personality");
        Check(e.State.Events.Any(ev => ev.Type == "payment" && ev.Message.Contains("paid 4 Gold for Blade of Perfection")), "and its cost of 4 Gold was paid");
        // The Blade itself destroyed (Akodo Michitsu): reattached to the same Personality.
        var f = Destroyed(false, out var ally4, out var blade4, out var wearer4);
        Check(f.State.Cards[blade4.Id].Zone == Zone.FateDiscard && f.State.Cards[wearer4.Id].Zone == Zone.Battlefield, "only the Blade was destroyed");
        Accept(f, Enabled(f, 0, ability, blade4.Id, wearer4.Id).Command!);
        Eq(f.State.Cards[blade4.Id].AttachedTo, wearer4.Id, "it may go back to the same Personality");
        // A destruction it was not part of, and the Blade's own player's action, offer nothing.
        var n = Table(Phase.Battle, 1); n.PriorityPlayerId = 1;
        var wn = Bf(n, "training-crane-samurai", 0); var bn = Attach(n, blade, wn); var other = Bf(n, "training-crane-samurai", 0);
        Gold(n, 0, 3); var ln = Bf(n, "training-lion-samurai", 1); var bowN = Attach(n, daikyu, ln);
        var en2 = MatchEngine.FromTestState(n);
        Accept(en2, Offer(en2, 1, "tsuruchi-daikyu-attack", bowN.Id, other.Id));
        Check(en2.State.PendingChoice is null && en2.State.Cards[bn.Id].Zone == Zone.Attached, "another Personality's destruction offers nothing");
        // Outside that window (declined away) it is not offered from the discard pile.
        var late = Destroyed(true, out _, out var blade5, out _);
        Settle(late);
        Check(late.State.Cards[blade5.Id].Zone == Zone.FateDiscard && !CanUse(late, 0, ability, blade5.Id), "declining leaves it in the discard pile and unavailable");
        Reject(late, Use(late, 0, ability, blade5.Id));
    }

    private static void GriefDishonor()
    {
        const string ability = "grief-dishonor";
        var def = Def(ability, "Grief");
        var fury = Def("burning-fury-attack");
        var s = Table(Phase.Battle, 0);
        var spell = Add(s, fury, 0, Zone.Hand);
        var grief = Add(s, def, 0, Zone.Hand); var grief2 = Add(s, def, 0, Zone.Hand);
        var monk = Bf(s, "training-crane-veteran", 0);
        var victim = Bf(s, "training-lion-courtier", 1);
        var mourner = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "burning-fury-attack", spell.Id, victim.Id, monk.Id));
        for (var guard = 0; e.State.PendingAttack is not null && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the action destroyed the Lion's Personality");
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, grief.Id, null, mourner.Id);
        var targets = Enabled(e, 0, ability, grief.Id).TargetIds!;
        Check(targets.Contains(mourner.Id) && !targets.Contains(monk.Id), "Grief targets a Personality of the player who lost one");
        var before = Honor(e, 1); var mine = Honor(e, 0);
        Accept(e, command);
        Check(e.State.Cards[mourner.Id].Dishonorable, "he is dishonored");
        Eq(Honor(e, 1), before - CardCatalog.Get("training-lion-veteran").PersonalHonor, "his controller lost his base Personal Honor");
        Eq(Honor(e, 0), mine, "and the reacting player lost nothing");
        Check(!CanUse(e, 0, ability, grief2.Id), "only one copy of Grief reacts to that one resolution");
        // No window and no Personality destroyed: not offered.
        var h = Table(Phase.Battle, 0);
        var idle = Add(h, def, 0, Zone.Hand); Add(h, "training-lion-veteran", 1, Zone.Home);
        NeverOpened(MatchEngine.FromTestState(h), 0, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void BakishiShield()
    {
        const string ability = "bakishi-shield";
        var def = Def(ability, "Hida Bakishi");
        const string destroy = "koiso-battle-destroy";
        var s = Table(Phase.Battle, 1);
        var bakishi = Bf(s, def, 1); Grant(bakishi, destroy);
        var victim = Bf(s, "training-crane-samurai", 0);
        var attacker = Bf(s, "training-crane-samurai", 0); Grant(attacker, destroy);
        var second = Bf(s, "training-crane-samurai", 0); Grant(second, destroy);
        var helper = Bf(s, "training-lion-samurai", 1); Grant(helper, destroy);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, destroy, bakishi.Id, victim.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" } && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "his action destroyed a card at the battlefield");
        var command = InWindow(e, "afterActionResolvedReaction", 1, ability, bakishi.Id);
        Accept(e, command);
        Settle(e);
        Eq(e.State.PriorityPlayerId, 0, "play moves to the other player");
        // The next time another player's action targets Bakishi, its effects on him are negated.
        Accept(e, Offer(e, 0, destroy, attacker.Id, bakishi.Id));
        Settle(e);
        Eq(e.State.Cards[bakishi.Id].Zone, Zone.Battlefield, "the destruction was negated");
        // Only that one: the next action destroys him.
        if (e.State.PriorityPlayerId == 1) Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, Offer(e, 0, destroy, second.Id, bakishi.Id));
        Settle(e);
        Eq(e.State.Cards[bakishi.Id].Zone, Zone.Dead, "the second action is not negated");
        // An action that destroyed nothing, and one he did not perform, open no Reaction.
        var t = Table(Phase.Battle, 1);
        var b2 = Bf(t, def, 1); Grant(b2, "training-bow-at-battle"); var foe = Bf(t, "training-crane-samurai", 0);
        var n = MatchEngine.FromTestState(t);
        Accept(n, Offer(n, 1, "training-bow-at-battle", b2.Id, foe.Id));
        Settle(n);
        Check(!CanUse(n, 1, ability, b2.Id), "an action that destroyed nothing: no Reaction");
        var u = Table(Phase.Battle, 1);
        var b3 = Bf(u, def, 1); var killer = Bf(u, "training-lion-samurai", 1); Grant(killer, destroy); var foe2 = Bf(u, "training-crane-samurai", 0);
        var o = MatchEngine.FromTestState(u);
        Accept(o, Offer(o, 1, destroy, killer.Id, foe2.Id));
        for (var guard = 0; o.State.PendingChoice is { Type: "beforeActionReaction" } && guard < 4; guard++) Accept(o, new GameCommand("declineReaction", o.State.PriorityPlayerId, o.Version));
        Check(!CanUse(o, 1, ability, b3.Id), "an action another Personality performed: Bakishi has no Reaction");
        Reject(o, Use(o, 1, ability, b3.Id));
    }

    private static void YamaderaAttack()
    {
        const string ability = "yamadera-attack";
        var def = Def(ability);
        var s = Table(Phase.Battle, 1);
        var yamadera = Bf(s, def, 0);
        var destroyer = Bf(s, "training-no-attachments-destroyer", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, yamadera.Id));
        Eq(e.State.Cards[yamadera.Id].Zone, Zone.Dead, "the enemy action destroyed Yamadera");
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, yamadera.Id, null, destroyer.Id);
        Accept(e, command);
        for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[destroyer.Id].Zone, Zone.Dead, "the Melee 8 Attack destroyed the Force 4 attacker");
        // A destroyer of Force 9 or more survives the Melee 8 Attack.
        var t = Table(Phase.Battle, 1);
        var y2 = Bf(t, def, 0); var big = Bf(t, "training-no-attachments-destroyer", 1); Force(big, 8);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 1, "training-destroy-no-attachments", big.Id, y2.Id));
        Accept(f, Offer(f, 0, ability, y2.Id, big.Id));
        for (var guard = 0; f.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 4; guard++) Accept(f, new GameCommand("declineReaction", f.State.PriorityPlayerId, f.Version));
        Check(f.State.Cards[big.Id].Zone == Zone.Battlefield, "Melee 8 does not destroy a Force above 8");
        // Alive and in play (not destroyed by the action): no window for him.
        var u = Table(Phase.Battle, 1);
        var y3 = Bf(u, def, 0); var other = Bf(u, "training-crane-samurai", 0); var d3 = Bf(u, "training-no-attachments-destroyer", 1);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Offer(g, 1, "training-destroy-no-attachments", d3.Id, other.Id));
        Check(g.State.Cards[y3.Id].Zone == Zone.Battlefield && !CanUse(g, 0, ability, y3.Id), "Yamadera was not destroyed: nothing to react to");
        Reject(g, Use(g, 0, ability, y3.Id, d3.Id));
    }

    private static void AyumuStraighten()
    {
        const string ability = "ayumu-straighten";
        var def = Def(ability, "Ikoma Ayumu");
        MatchEngine Build(int actor, int ayumuOwner, bool home, out CardInstance ayumu, out CardInstance tactician, out CardInstance discard, out CardInstance bower, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, actor);
            s.Cards[s.Players[actor].StrongholdId].DefinitionId = "oracle-l5r-1218"; // a published Stronghold: the rulebook's player abilities (Tactical Advantage) are enabled
            ayumu = home ? Add(s, def, ayumuOwner, Zone.Home, bowed: true) : Add(s, def, ayumuOwner, Zone.Battlefield, bowed: true, province: 0);
            tactician = Kw(Bf(s, "training-crane-samurai", actor), "Tactician");
            bower = Bf(s, "training-battle-bower", actor);
            discard = Add(s, "training-focus-strategy", actor, Zone.Hand);
            enemy = Bf(s, "training-lion-samurai", 1 - actor);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(0, 0, false, out var ayumu, out var tactician, out var discard, out _, out _);
        Accept(e, new GameCommand("useAbility", 0, e.Version, TargetId: discard.Id, AbilityId: "tactical-advantage", PerformerId: tactician.Id));
        var command = InWindow(e, "announceReaction", 0, ability, ayumu.Id);
        Accept(e, command);
        Check(!e.State.Cards[ayumu.Id].Bowed, "even bowed, he straightened");
        Settle(e);
        Eq(e.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the Tactical action went on");
        // Another Battle action (not Tactical): no window.
        var o = Build(0, 0, false, out var ay2, out _, out _, out var bower, out var enemy);
        Accept(o, Offer(o, 0, "training-bow-at-battle", bower.Id, enemy.Id));
        Check(o.State.PendingChoice?.Type != "announceReaction" && o.State.Cards[ay2.Id].Bowed, "not a Tactical action: no window, still bowed");
        // At home: not at the current battlefield.
        var h = Build(0, 0, true, out var ay3, out var t2, out var d2, out _, out _);
        Accept(h, new GameCommand("useAbility", 0, h.Version, TargetId: d2.Id, AbilityId: "tactical-advantage", PerformerId: t2.Id));
        Check(h.State.PendingChoice?.Type != "announceReaction" && !CanUse(h, 0, ability, ay3.Id), "Ayumu at home: no window");
        // "after YOU announce": the other player's Tactical action gives his Ayumu nothing.
        var w = Build(1, 0, false, out var ay4, out var t3, out var d3, out _, out _);
        Accept(w, new GameCommand("useAbility", 1, w.Version, TargetId: d3.Id, AbilityId: "tactical-advantage", PerformerId: t3.Id));
        Check(!CanUse(w, 0, ability, ay4.Id) && w.State.Cards[ay4.Id].Bowed, "an opponent's Tactical action does not let Ayumu straighten");
        Reject(w, Use(w, 0, ability, ay4.Id));
    }

    private static void MaratayStatue()
    {
        const string ability = "maratai-statue";
        var def = Def(ability, "Kakita Maratai");
        MatchEngine Duel(bool ownWins, bool maratBowed, int favor, out CardInstance maratai)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            if (favor >= 0) s.FavorPlayerId = favor;
            maratai = Add(s, def, 1, Zone.Battlefield, maratBowed, province: 0);
            var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
            var challenger = Bf(s, ownWins ? "training-crane-veteran" : "training-crane-samurai", 1);
            var foe = Bf(s, ownWins ? "training-crane-samurai" : "training-crane-veteran", 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: foe.Id, AbilityId: "training-duel-challenge"));
            Accept(e, new GameCommand("acceptDuel", 0, e.Version));
            Accept(e, new GameCommand("strike", 0, e.Version));
            Check(e.State.PendingDuel is null, "the duel is fully resolved");
            return e;
        }
        var e = Duel(true, false, 0, out var maratai);
        var command = InWindow(e, "duelWonReaction", 1, ability, maratai.Id);
        // (InWindow already closed a copy: Maratai is offered while the window is open, to her controller alone.)
        var honor = Honor(e, 1);
        Accept(e, command);
        Check(e.State.Cards[maratai.Id].Bowed, "bow Maratai: the printed cost");
        Eq(Honor(e, 1), honor + 2, "gain 2 Honor");
        Eq(e.State.FavorPlayerId, 1, "take the Imperial Favor (from the player who held it)");
        Check(!CanUse(e, 1, ability, maratai.Id), "bowed now, and once only");
        // The enemy's Personality won: nothing for Maratai's controller. A bowed Maratai could not pay her cost.
        var lost = Duel(false, false, -1, out var m2);
        Check(lost.State.PendingChoice?.Type != "duelWonReaction" && !CanUse(lost, 1, ability, m2.Id), "an enemy win opens nothing for her controller");
        var bowed = Duel(true, true, -1, out var m3);
        Check(!CanUse(bowed, 1, ability, m3.Id), "a bowed Maratai cannot pay her bow cost");
        Reject(bowed, Use(bowed, 1, ability, m3.Id));
    }

    private static void QuagmireNegate()
    {
        const string ability = "quagmire-negate-straighten";
        var def = Def(ability, "Quagmire");
        (MatchEngine E, CardInstance Victim, CardInstance Bower, CardInstance Region) Setup(int regionProvince, int regionOwner = 0, bool regionBowed = false)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var victim = Bf(s, "training-crane-samurai", 0); Bf(s, "training-lion-samurai", 1);
            var bower = Bf(s, "training-battle-bower", 1);
            var region = Add(s, def, regionOwner, Zone.Region, bowed: regionBowed, province: regionProvince);
            Add(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
            return (MatchEngine.FromTestState(s), victim, bower, region);
        }
        foreach (var province in new[] { 0 }) // presence rule (#1133): a Region reacts only at a battle on its own province, so an adjacent Region no longer qualifies (moved to the no-window list)
        {
            var (e, victim, bower, region) = Setup(province);
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, victim.Id));
            Check(e.State.Cards[victim.Id].Bowed, "he was bowed");
            var command = InWindow(e, "bowReaction", 0, ability, region.Id, null, victim.Id);
            Accept(e, command);
            Check(e.State.Cards[region.Id].Bowed, "the Region bowed");
            Eq(e.State.Cards[victim.Id].StraightenBlockedTurn, e.State.TurnNumber, "his next straightening this turn is negated");
            Settle(e);
            // The next straightening is negated; the one after is not.
            var go = Edit(e, st => st.PriorityPlayerId = 0);
            var straightener = go.State.Cards.Values.First(c => c.DefinitionId == "training-battle-straightener");
            Accept(go, Offer(go, 0, "training-straighten-at-battle", straightener.Id, victim.Id));
            Check(go.State.Cards[victim.Id].Bowed, "the first straightening was negated");
        }
        // Two provinces away, a bowed Region, or the Attacker's Region: no window.
        foreach (var (province, owner, bowed, why) in new[] { (1, 0, false, "an adjacent province (the Region is not at the battle)"), (2, 0, false, "two provinces away"), (0, 0, true, "a bowed Region"), (0, 1, false, "the Attacker's Region") })
        {
            var (e, victim, bower, region) = Setup(province, owner, bowed);
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, victim.Id));
            // An adjacent Region still opens an (unusable) bowReaction window in the engine (OpenBowReactionWindow ignores the presence rule): there the Region must at least be unusable.
            Check((province == 1 || e.State.PendingChoice is not { Type: "bowReaction" }) && !CanUse(e, 0, ability, region.Id), "no window for " + why);
            Check(e.State.Cards[victim.Id].Bowed && e.State.Cards[victim.Id].StraightenBlockedTurn != e.State.TurnNumber, "he stays bowed and nothing is negated");
        }
    }

    private static void YungSearch()
    {
        const string ability = "yung-search-ring";
        var def = Def(ability, "Yung");
        const string voidRing = "oracle-l5r-6338", airRing = "oracle-l5r-6333";
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var yung = Add(s, def, 0, Zone.Home);
        var ring = Add(s, voidRing, 0, Zone.Hand);
        var top = Add(s, airRing, 0, Zone.FateDeck); s.Players[0].Hand.Remove(top.Id); s.Players[0].FateDeck.Insert(0, top.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, ability, yung.Id), "no Ring has entered play yet");
        Accept(e, Use(e, 0, "ring-of-void-recruit", ring.Id));
        Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "the Ring entered play");
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, yung.Id);
        Accept(e, command);
        Check(e.State.Cards[yung.Id].Bowed, "Yung bowed as his cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search opens");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(entries.Any(x => x.DefinitionId == airRing && x.Eligible) && entries.Where(x => x.Eligible).All(x => x.Definition.Type == CardType.Ring), "only Rings are eligible");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: airRing));
        Check(e.State.Players[0].Hand.Select(id => e.State.Cards[id].DefinitionId).Contains(airRing), "the Ring was put in the hand");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == airRing), "and shown");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over and the window closed");
        // Another player's Ring entering play is not "one of your Rings".
        var t = Table(Phase.Action, 1);
        var y2 = Add(t, def, 0, Zone.Home); var enemyRing = Add(t, voidRing, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Use(f, 1, "ring-of-void-recruit", enemyRing.Id));
        Check(!CanUse(f, 0, ability, y2.Id), "an enemy Ring gives Yung nothing");
        Reject(f, Use(f, 0, ability, y2.Id));
    }

    private static void RetributionDeclare()
    {
        const string ability = "retribution-declare";
        var def = Def(ability, "Retribution");
        MatchEngine Build(bool holdsIt, out CardInstance card, out CardInstance lead, out CardInstance second, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            for (var i = 1; i < 4; i++) s.Players[1].Provinces[i].Destroyed = true; // player 1 keeps one province: one battlefield in the additional phase
            card = Add(s, def, 0, holdsIt ? Zone.Hand : Zone.FateDiscard);
            Bf(s, "training-lion-veteran", 1); Bf(s, "training-crane-samurai", 0);
            lead = Add(s, "training-crane-veteran", 0, Zone.Home); second = Add(s, "training-crane-samurai", 0, Zone.Home);
            follower = Add(s, "training-follower", 0, Zone.Attached, parent: lead.Id);
            Add(s, "training-lion-veteran", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var lead, out var second, out var follower);
        RunUntil(e, () => e.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Eq(e.State.PriorityPlayerId, 0, "the Defender holds the window");
        var command = InWindow(e, "attackPhaseEndReaction", 0, ability, card.Id);
        Check(!e.QueryActions(1).Any(o => o.Type == "useAbility"), "the Attacker is offered nothing");
        Accept(e, command);
        Eq(e.State.Phase, Phase.InfantryDeclaration, "an additional Attack Phase begins");
        Eq(e.State.ActivePlayerId, 0, "the Defender is its Attacker");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var attackOptions = e.QueryActions(0).Where(o => o.Type == "attack" && o.Command is not null).ToArray();
        Check(attackOptions.Length == 2 && attackOptions.All(o => o.Command!.Assignments!.Count == 1), "only one Personality may be assigned: one option per (unit, province)");
        Reject(e, new("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [lead.Id] = 0, [second.Id] = 0 }));
        Accept(e, attackOptions.Single(o => o.Command!.Assignments!.ContainsKey(lead.Id)).Command!);
        Eq(e.State.Cards[lead.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), 1, "the Personality has +1F after he assigns");
        Eq(e.State.Cards[follower.Id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), 1, "and so does each Follower in his unit");
        RunUntil(e, () => e.State.Phase == Phase.Dynasty);
        Eq(e.State.ActivePlayerId, 1, "player 1's turn goes on: the Dynasty Phase is his");
        // Without a copy in hand no window opens; as the Attacker of that phase there is none either.
        var n = Build(false, out _, out _, out _, out _);
        RunUntil(n, () => n.State.Phase == Phase.Dynasty);
        Check(n.State.ActivePlayerId == 1, "no window: the Dynasty Phase follows the last battle");
        var a = Table(Phase.Battle, 0); a.PriorityPlayerId = 0;
        Bf(a, "training-crane-veteran", 0); Bf(a, "training-lion-samurai", 1);
        var mine = Add(a, def, 0, Zone.Hand);
        var att = MatchEngine.FromTestState(a);
        RunUntil(att, () => att.State.Phase != Phase.Battle || att.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Check(att.State.PendingChoice?.Type != "attackPhaseEndReaction" && !CanUse(att, 0, ability, mine.Id), "the Attacker of the phase was not its Defender: not offered");
    }

    private static void MasteringCopy()
    {
        const string ability = "mastering-the-elements-copy";
        var def = Def(ability, "Mastering the Elements");
        const string flood = "oracle-l5r-6502";
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var wielder = Bf(s, "training-shugenja", 0); var host = Bf(s, "training-shugenja", 0); Bf(s, "training-lion-samurai", 1);
        var spell = Add(s, flood, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var mastering = Add(s, def, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: host.Id, TargetId: mastering.Id, SourceIds: []));
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entry window is open");
        Check(e.State.Cards[mastering.Id].Printed is null && !e.HasKeyword(mastering.Id, "Water"), "it copies nothing until it is used");
        var option = Options(e, 0, ability, mastering.Id).Single(o => o.Enabled);
        Check(option.TargetIds!.SequenceEqual(new[] { spell.Id }), "the one Spell of his is the target");
        Eq(option.TargetGold![spell.Id].Cost, 2, "Scouring Flood's base Gold Cost is asked");
        Check(!CanUse(e, 1, ability, mastering.Id), "not listed for the other player");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: mastering.Id, AbilityId: ability, TargetId: spell.Id, SourceIds: []));
        var pay = e.PaymentOptions(0, 2).First().SourceIds;
        var command = new GameCommand("useAbility", 0, e.Version, CardId: mastering.Id, AbilityId: ability, TargetId: spell.Id, SourceIds: pay);
        Reject(e, command with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, mastering.Id), "not offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        Accept(e, command);
        Check(pay.All(id => e.State.Cards[id].Bowed), "the Gold was paid from those sources");
        var m = e.State.Cards[mastering.Id];
        Check(e.HasKeyword(mastering.Id, "Water") && e.HasKeyword(mastering.Id, "Unique"), "the Water keyword is copied and its own kept");
        Eq(m.Printed!.Effect, "scouring-flood-force", "the trait");
        Check(m.Printed.Abilities!.SequenceEqual(new[] { ability, "scouring-flood-move-home" }), "its base ability was added to its own: " + string.Join(",", m.Printed.Abilities!));
        Eq(e.CardForceForTest(host.Id), CardCatalog.Get("training-shugenja").Force + 2, "its wielder has +2F from the copied trait");
        Check(!CanUse(e, 0, ability, mastering.Id), "once per game: not offered again");
        var battle = Edit(e, st => { st.PendingChoice = null; st.PriorityPlayerId = 0; st.Cards[mastering.Id].Bowed = false; });
        Check(CanUse(battle, 0, "scouring-flood-move-home", mastering.Id), "Mastering the Elements now has Scouring Flood's Battle action");
    }

    // The Defender's home is reached by announcing a Ranged Attack: No Hiding Place gives it -1 strength, Lookout is destroyed.
    private static (MatchEngine E, CardInstance Archer, CardInstance AtHome, CardInstance? Hiding) Archery(string reactorDef, int homeForce, bool withReactor = true, Action<MatchState>? more = null)
    {
        const string archerAbility = "shinjo-eun-sahng-attack";
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var archer = Add(s, Def(archerAbility), 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var atHome = Add(s, "training-crane-samurai", 0, Zone.Home); Force(atHome, homeForce - 3);
        var reactor = withReactor ? Add(s, reactorDef, 1, Zone.Hand) : null;
        more?.Invoke(s);
        return (MatchEngine.FromTestState(s), archer, atHome, reactor);
    }

    private static void NoHidingPlace()
    {
        const string ability = "no-hiding-place", archerAbility = "shinjo-eun-sahng-attack";
        var def = Def(ability, "No Hiding Place");
        var (e, archer, atHome, hiding) = Archery(def, 3);
        Accept(e, Enabled(e, 1, archerAbility, archer.Id).Command! with { TargetId = null });
        var command = InWindow(e, "announceReaction", 1, ability, hiding!.Id);
        Accept(e, command);
        CloseAnnounce(e);
        Eq(e.State.PendingChoice?.Type, "attackTargetSelect", "the Attack's target is selected after the announce window");
        var targets = e.QueryActions(1).Where(c => c.Type == "chooseAttackTarget" && c.Command?.Choice != "none").Select(c => c.Command!.TargetId).ToArray();
        Check(targets.Contains(atHome.Id), "a Personality without Followers in the Defender's home is a legal target now");
        Accept(e, e.QueryActions(1).Single(c => c.Type == "chooseAttackTarget" && c.Command?.TargetId == atHome.Id).Command!);
        for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 6; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[atHome.Id].Zone, Zone.Dead, "Ranged 4 - 1 = 3 against Force 3: destroyed in the Defender's home");
        Eq(e.State.Cards[hiding.Id].Zone, Zone.FateDiscard, "No Hiding Place was spent");
        // Without it a card at home is never a legal Attack target.
        var (n, archer2, home2, _) = Archery(def, 3, withReactor: false);
        Check(!Options(n, 1, archerAbility, archer2.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Contains(home2.Id), "not offered");
        Check(!n.Execute(Enabled(n, 1, archerAbility, archer2.Id).Command! with { TargetId = home2.Id }).Accepted, "a hand-made command is refused");
        Accept(n, Enabled(n, 1, archerAbility, archer2.Id).Command! with { TargetId = null });
        Check(n.State.PendingChoice?.Type != "announceReaction", "no copy in hand: no announce window");
        // The -1 strength makes the Attack miss a Force 4 Personality (Ranged 4 - 1 = 3).
        var (m, archer3, strong, hiding3) = Archery(def, 4);
        Accept(m, Enabled(m, 1, archerAbility, archer3.Id).Command! with { TargetId = null });
        Accept(m, Options(m, 1, ability, hiding3!.Id).First(o => o.Enabled).Command!);
        CloseAnnounce(m);
        Accept(m, m.QueryActions(1).Single(c => c.Type == "chooseAttackTarget" && c.Command?.TargetId == strong.Id).Command!);
        for (var guard = 0; m.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 6; guard++) Accept(m, new GameCommand("declineReaction", m.State.PriorityPlayerId, m.Version));
        Eq(m.State.Cards[strong.Id].Zone, Zone.Home, "strength 3 against Force 4: he survives");
        // The Attacker's own home, and a Personality with a Follower in the Defender's home, are not offered; the other player never has it.
        CardInstance own = null!, followed = null!;
        var (f, archer4, _, hiding4) = Archery(def, 1, more: st =>
        {
            own = Add(st, "training-lion-samurai", 1, Zone.Home);
            followed = Add(st, "training-crane-samurai", 0, Zone.Home); Add(st, "training-follower", 0, Zone.Attached, parent: followed.Id);
        });
        Accept(f, Enabled(f, 1, archerAbility, archer4.Id).Command! with { TargetId = null });
        Check(!CanUse(f, 0, ability, hiding4!.Id), "not listed for the other player");
        Accept(f, Options(f, 1, ability, hiding4.Id).First(o => o.Enabled).Command!);
        CloseAnnounce(f);
        var ids = f.QueryActions(1).Where(c => c.Type == "chooseAttackTarget").Select(c => c.Command!.TargetId).ToArray();
        Check(!ids.Contains(own.Id) && !ids.Contains(followed.Id), "neither the Attacker's own home nor a Personality with a Follower");
        // A Melee Attack announces no Ranged Attack: no window.
        CardInstance weapon = null!;
        var (g, _, _, hiding5) = Archery(def, 1, more: st => { var sw = Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0); weapon = Add(st, "training-reserve-item", 1, Zone.Attached, province: 0, parent: sw.Id); });
        Accept(g, Enabled(g, 1, "training-weapon-attack", weapon.Id).Command! with { TargetId = null });
        Check(g.State.PendingChoice?.Type != "announceReaction" && !CanUse(g, 1, ability, hiding5!.Id), "a Melee Attack gets nothing");
    }

    private static void LookoutDestroy()
    {
        const string ability = "tf-lookout-destroy", daikyu = "tsuruchi-daikyu-attack";
        var def = Def(ability, "Lookout");
        (MatchEngine E, CardInstance Lookout, CardInstance Bow, CardInstance Present, CardInstance HomeTarget) Table4(bool withLookout = true, bool ninjaKeyword = true)
        {
            var s = Table(Phase.Battle, 1);
            var archer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var bow = Attach(s, Def(daikyu), archer);
            var present = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var homeTarget = Add(s, "training-crane-samurai", 0, Zone.Home);
            CardInstance? lookout = null;
            if (withLookout)
            {
                var ninja = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                if (ninjaKeyword) Kw(ninja, "Ninja");
                lookout = Attach(s, def, ninja);
            }
            return (MatchEngine.FromTestState(s), lookout!, bow, present, homeTarget);
        }
        var (plain, _, pbow, ppresent, phome) = Table4(false);
        Reject(plain, Use(plain, 1, daikyu, pbow.Id, phome.Id));
        var (e, lookout, bow, present, homeTarget) = Table4();
        Accept(e, Use(e, 1, daikyu, bow.Id, present.Id));
        Eq(e.State.PendingChoice?.CardId, lookout.Id, "the announce window is the Lookout's");
        var command = InWindow(e, "announceReaction", 1, ability, lookout.Id);
        Accept(e, command);
        CloseAnnounce(e);
        Check(e.State.Cards[lookout.Id].Zone != Zone.Attached, "the Lookout was destroyed as the cost");
        Eq(e.State.PendingChoice?.Type, "attackTargetSelect", "the Attack's target is selected now");
        Check(e.State.PendingAttackTargetSelection!.Candidates.Contains(homeTarget.Id) && e.State.PendingAttackTargetSelection.Candidates.Contains(present.Id), "the Defender's home card is a legal target");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseAttackTarget" && a.Command?.TargetId == homeTarget.Id).Command!);
        Eq(e.State.Cards[homeTarget.Id].Zone, Zone.Dead, "the Ranged 5 Attack destroyed the card in the Defender's home");
        Eq(e.State.Cards[present.Id].Zone, Zone.Battlefield, "the card at the battlefield was not hit");
        Check(!e.State.AnnouncedAttackModifiers.Any(), "the permission was spent with that Attack");
        // "One Ranged Attack from that action": a later Ranged Attack of another action has no such permission.
        var (e2, lookout2, bow2, present2, home2) = Table4();
        Accept(e2, Use(e2, 1, daikyu, bow2.Id, present2.Id));
        Accept(e2, Enabled(e2, 1, ability, lookout2.Id).Command!);
        CloseAnnounce(e2);
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "chooseAttackTarget" && a.Command?.Choice == "none").Command!);
        Reject(e2, Use(e2, 1, daikyu, bow2.Id, home2.Id));
        // A Reaction is a choice: declining leaves the Lookout in play and the home card untouched.
        var (e3, lookout3, bow3, present3, home3) = Table4();
        Accept(e3, Use(e3, 1, daikyu, bow3.Id, present3.Id));
        Accept(e3, new GameCommand("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        Eq(e3.State.Cards[lookout3.Id].Zone, Zone.Attached, "declined: still in play");
        Eq(e3.State.Cards[home3.Id].Zone, Zone.Home, "and the home card is untouched");
        // The Defender never has it, and a Melee Attack announces no Ranged Attack.
        var (e4, lookout4, bow4, present4, _) = Table4();
        Accept(e4, Use(e4, 1, daikyu, bow4.Id, present4.Id));
        Check(!CanUse(e4, 0, ability, lookout4.Id), "not listed for the other player");
        Reject(e4, Use(e4, 0, ability, lookout4.Id));
        var m = Table(Phase.Battle, 1);
        var attacker = Bf(m, "training-lion-samurai", 1); var sword = Attach(m, "oracle-l5r-10124", attacker);
        var ninja2 = Kw(Add(m, "training-lion-samurai", 1, Zone.Home), "Ninja"); var lookoutM = Attach(m, def, ninja2);
        var victim = Bf(m, "training-crane-samurai", 0);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Use(me, 1, "unparalleled-sword-attack", sword.Id, victim.Id));
        Check(me.State.PendingChoice?.Type != "announceReaction" && me.State.Cards[lookoutM.Id].Zone == Zone.Attached, "no announce window for a Melee Attack");
    }
}
