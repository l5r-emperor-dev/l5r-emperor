using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #833, single-reaction slice 1: cards printing exactly one Reaction. Each test opens the real trigger window through ordinary engine play
// (a purchase, an equip, a Strategy's action, an engage, a battle's resolution ...), proves the Reaction is offered enabled to its own player only,
// accepted with the printed effect checked in concrete state, and then proves the identical command is refused (and not offered) once the window is
// closed, never opened, or when the printed condition is not met. A Reaction that is a trait of an entering-play window is taken through that window.
internal static partial class SingleReaction1Tests
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
    // window is declined away the very same command is no longer offered and is refused. Returns the offered command (completed by `complete`), unplayed in `e`.
    private static GameCommand InWindow(MatchEngine e, string windowType, int owner, string ability, string cardId, string? performerId = null, string? targetId = null, Func<GameCommand, GameCommand>? complete = null, Func<MatchEngine, GameCommand>? direct = null)
    {
        Check(e.State.PendingChoice?.Type == windowType || windowType == "entryReaction" && e.State.PendingChoice?.Type == "proclaim", ability + ": the window is " + e.State.PendingChoice?.Type);
        if (e.State.PriorityPlayerId != owner && e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        var options = Options(e, owner, ability, cardId, performerId);
        GameCommand command;
        if (direct is not null) { Check(options.Length > 0, ability + " is not even listed for its owner"); command = direct(e); }
        else
        {
            var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
            Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner}" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
            command = targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
        }
        if (complete is not null) command = complete(command);
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

    // A Follower equipped from the hand to a Personality in the Action phase, leaving its entering-play window open.
    private static (MatchEngine E, CardInstance Card, CardInstance Host) Equip(string def, Action<MatchState, CardInstance>? tweak = null, int gold = 12)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var card = Add(s, def, 0, Zone.Hand);
        Gold(s, 0, gold);
        tweak?.Invoke(s, host);
        var e = MatchEngine.FromTestState(s);
        var equip = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == card.Id);
        Check(equip.Command is not null, "equip offered: " + equip.Reason);
        Accept(e, equip.Command! with { TargetId = host.Id });
        Check(e.State.PendingChoice?.Type == "entryReaction", $"{def} equipped, but the window is {e.State.PendingChoice?.Type}");
        return (e, card, host);
    }

    public static void Run(TestRunner runner) => runner.Run("single-reaction-1", [
        ("Akodo Nojima: the entering-play Reaction loses 1 Honor for its controller only, once, inside the window", NojimaEntry),
        ("Band of Brothers: the entering-play Reaction creates and attaches a 1F Follower to each of one or two own Personalities, in the window only", BandOfBrothers),
        ("Kitsune Kichi: the entering-play Reaction creates the 3F/2C/3PH Mantis Badger at home, in the window only", KichiBadger),
        ("Village Guardian: the entering-play Reaction draws one card per player who has had a province destroyed, in the window only", VillageGuardian),
        ("Susumu Takada: Invest 3 on entering play discards a card for Honor equal to its Focus Value, in the window only", TakadaInvest),
        ("Hida Gojiro: Invest 4 on entering play gives two +1F tokens, then lose 1 Honor, in the window only", GojiroInvest),
        ("Sengmai, the Unbreakable: after entering play a Kiho in the discard pile goes to the hand, in the window only", SengmaiKiho),
        ("Otomo Suikihime: once per game after entering play, gain a province left of the leftmost, in the window only", SuikihimeLands),
        ("Caught in the Act: after another player's action targets one of your Personalities it negates the bowing from the action's effects, in a Battle or Action window, not for a non-bowing or own action", CaughtInTheAct),
        ("Armor of the Uruwashii: before another player's action resolves it negates the wielder's movement only, not offered when the wielder is not moved", UruwashiiNegateMove),
        ("Hunger: after an action targets its wielder it negates his movement from the action's effects, the rest of the action still happens", HungerNegateMove),
        ("Goju Sawaki: after another player's Battle action targets him, destroy him to create two 1F/1C/0PH Spider Shadowlands Ninja at the current battlefield, not for a non-Battle action", SawakiSpawn),
        ("Hantei's Guidance: discarding the Celestial negates a Political action and only a Political action", HanteiNegate),
        ("Snake Tattoo: after another player's Battle action targets your Tattooed Personality he moves to the adjacent battlefield and the action, no longer legal, is negated", SnakeTattooMove),
        ("Shifting Terrain: in play, another player's action targeting one of your Personalities may be redirected to your other Personality at the Terrain's battlefield", ShiftingTerrainRedirect),
        ("Training Merchant guard (free): after an action targets your Merchant, for 0 Gold the action's effects are negated; a non-Merchant opens nothing", MerchantGuardFree),
        ("Bend like a Reed: before Focus Effects resolve, the duelist who did not strike gets +2 to his duel stat; the striker, a second copy and a missing duel get nothing", BendReedBoost),
        ("Falling Rain Dojo: before Focus Effects resolve, your Duelist with the lower duel stat gets +1, as challenger or as challenged, and not otherwise", FallingRainBoost),
        ("A Champion in Court: after another player's action destroys your Courtier or Shugenja a Yojimbo gets three +1F Revenge tokens; not for another Personality or a missing Yojimbo", ChampionRevenge),
        ("Tsuruchi Yakusho: after you announce a Ranged action while at the battlefield, its Attack is not reduced; not for Melee, not from home, not for the other player", YakushoImmunity),
        ("Kitsuki Daisuke (Experienced): after he wins a duel his controller draws a card, once; not after a lost duel", DaisukeDraw),
        ("Jade's Roar: after engaging at its battlefield its controller (Attacker or Defender) has Reconnaissance and the first opportunity, which must be a Terrain action; not from home", JadesRoarRecon),
        ("Koshin Keep: after engaging its controller has Reconnaissance and a non-destroying Ranged Attack reduces the target's Force and grants an additional Ranged Attack on it", KoshinKeepRecon),
        ("Kensai's Blade: after engaging, attach it from the hand to your performing Personality at the battlefield, paying its Gold; not to one at home", KensaisBladeAttach),
        ("Nakanu Technique: after engaging, your unbowed opposed Personality gets two 1F Shadowlands Undead Followers and you lose 1 Honor; not a bowed or unopposed one", NakanuTechnique),
        ("Recruitment Officer: after engaging where you control a unit, bow it to create a 2F/2C/2PH Ronin Samurai there until the turn ends; not without a unit there", RecruitmentOfficer),
        ("Rokku: even bowed, after engaging with an enemy unit at the battlefield he moves there and his unit straightens", RokkuMove),
        ("Utaku Ryoko: after engaging at her battlefield a Force penalty equal to her Personal Honor on an opposing Personality or Follower; not from home", RyokoPenalty),
        ("The Wave Persists: after engaging, a Personality of yours at the battlefield goes home before the battle resolves and the Shugenja takes his place", WavePersistsMove),
        ("Isawa Tsumaro: Air Home Engage moves your Air Shugenja home and creates a 0F/3C/0PH Air Illusion Shugenja at the battlefield, removed when the battle ends", TsumaroIllusion),
        ("Training political Engage claim: after engaging, targeting an enemy Personality gives the Attacker the first opportunity; not an own Personality", EngageClaimPolitical),
        ("Shinjo Sihung: after your Tactical action resolves, the Personality who performed it gets +2F; not for a non-Tactical or the other player's action", SihungBoost),
        ("Magistrate's Stipend: after you announce a non-Iaijutsu action a performing Magistrate gets +1C until it ends; not for Iaijutsu or a non-Magistrate", StipendAnnounce),
        ("Mempo of the Amethyst Champion: after you discard the Imperial Favor with a rulebook Favor action you may take it back; not for the other player's discard", MempoTakeFavor),
        ("The Ivory Sword: after a dishonorable Personality opposing its wielder performs an action, his controller chooses and discards a card; not honorable, own or handless", IvoryDiscard),
        ("Deeds and Words: before an Honor change from an action in the Action Phase, destroy it to reduce that change to 0, for either player's change", DeedsAndWords),
        ("Coin Pouch: bowed with a Holding bowed for Gold in the same payment it adds 1 Gold, once a turn; never alone or while bowed", CoinPouchGold),
        ("Witness the Untold: after your Ring enters play, bow the Spell to look at the top three cards of your Dynasty or Fate deck and reorder them; not for the other player's Ring", WitnessLook),
        ("Inexorable: after another player announces a Reaction during the Combat Segment it negates that whole Reaction; not outside the Combat Segment or for a non-Reaction", InexorableNegate),
        ("Training before-action suppressor: after another player's action targets your card, it bows itself and your other before-action reactors, removing their eligibility", BeforeActionSuppress),
        ("Training Discipline counter: before an additional Battle action resolves, negate it, free from hand or paying its Discipline cost from a discard pile", DisciplineCounter),
//TESTS
    ]);

    private static void NojimaEntry()
    {
        const string ability = "nojima-entry";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var honor = Honor(e, 0);
        Accept(e, command);
        Eq(Honor(e, 0), honor - 1, "After Nojima enters play, lose 1 Honor");
        Check(!CanUse(e, 0, ability, id), "taken once for this entry");
        var h = AtHome(def, out var nojima);
        NeverOpened(h, 0, ability, nojima.Id, command with { CardId = nojima.Id });
    }

    private static void BandOfBrothers()
    {
        const string ability = "tf-band-brothers-create";
        var def = Def(ability);
        var conscripts = CardCatalog.Get(def);
        Check(conscripts.Type == CardType.Follower, "Band of Brothers is a Follower");
        var (e, card, host) = Equip(def, (s, _) => Add(s, "training-lion-samurai", 0, Zone.Home));
        var other = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality && c.Id != host.Id);
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, card.Id, complete: c => c with { TargetId = null, Selections = [Pick("hosts", host.Id, other.Id)] });
        Accept(e, command);
        var created = e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();
        Eq(created.Length, 2, "one Follower for each chosen Personality");
        Check(created.All(c => c.OwnerId == 0 && c.Zone == Zone.Attached && CardCatalog.Get(c.DefinitionId).Type == CardType.Follower && CardCatalog.Get(c.DefinitionId).Force == 1), "each a 1F Follower of its controller");
        Check(created.Any(c => c.AttachedTo == host.Id) && created.Any(c => c.AttachedTo == other.Id), "one attached to each chosen Personality");
        Check(!CanUse(e, 0, ability, card.Id), "taken once for this entry");
        // One Personality gets exactly one.
        var (f, card2, host2) = Equip(def);
        var b2 = f.State.Cards.Keys.ToHashSet();
        Accept(f, Enabled(f, 0, ability, card2.Id).Command! with { TargetId = null, Selections = [Pick("hosts", host2.Id)] });
        Eq(f.State.Cards.Values.Count(c => !b2.Contains(c.Id)), 1, "a single chosen Personality receives a single Follower");
        // The opponent's Personality is not a legal host, and nothing happened.
        var (g, card3, _) = Equip(def, (s, _) => Add(s, "training-lion-samurai", 1, Zone.Home));
        var foreign = g.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        Reject(g, Enabled(g, 0, ability, card3.Id).Command! with { TargetId = null, Selections = [Pick("hosts", foreign.Id)] });
        // Out of the window: refused.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var h = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var band = Add(s2, def, 0, Zone.Attached, parent: h.Id);
        NeverOpened(MatchEngine.FromTestState(s2), 0, ability, band.Id, command with { CardId = band.Id });
    }

    private static void KichiBadger()
    {
        const string ability = "kichi-create-badger";
        var def = Def(ability, "Kitsune Kichi");
        var (e, id) = Buy(def, s => s.Players[0].Clan = "Mantis");
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var badger = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var printed = CardCatalog.Get(badger.DefinitionId);
        Eq(badger.OwnerId, 0, "controlled by Kichi's player"); Eq(badger.Zone, Zone.Home, "created at home");
        Check(printed.Type == CardType.Personality && printed.Force == 3 && printed.Chi == 2 && printed.PersonalHonor == 3, $"a 3F/2C/3PH Personality, got {printed.Force}/{printed.Chi}/{printed.PersonalHonor}");
        foreach (var kw in new[] { "Mantis Clan", "Nonhuman", "Badger", "Spirit" }) Check((printed.Keywords ?? []).Contains(kw), "has the keyword " + kw);
        Check((printed.Abilities ?? []).Length == 1 && AbilityCatalog.All[printed.Abilities![0]].Timing == "Battle", "carries one Battle ability");
        Check(!CanUse(e, 0, ability, id), "once for this entry");
        var h = AtHome(def, out var kichi);
        NeverOpened(h, 0, ability, kichi.Id, command with { CardId = kichi.Id });
    }

    private static void VillageGuardian()
    {
        const string ability = "village-guardian-draw";
        var def = Def(ability);
        foreach (var (flags, draws) in new[] { ((false, false), 0), ((true, false), 1), ((false, true), 1), ((true, true), 2) })
        {
            var (e, card, _) = Equip(def, (s, _) => s.ProvinceEverDestroyed = [flags.Item1, flags.Item2]);
            var command = InWindow(e, "entryReaction", 0, ability, card.Id);
            var hand = e.State.Players[0].Hand.Count; var theirs = e.State.Players[1].Hand.Count;
            Accept(e, command);
            Eq(e.State.Players[0].Hand.Count, hand + draws, $"one card for each player who has had a province destroyed {flags}");
            Eq(e.State.Players[1].Hand.Count, theirs, "the opponent draws nothing");
        }
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; s2.ProvinceEverDestroyed = [true, true];
        var h = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var guardian = Add(s2, def, 0, Zone.Attached, parent: h.Id);
        NeverOpened(MatchEngine.FromTestState(s2), 0, ability, guardian.Id, Use(MatchEngine.FromTestState(s2), 0, ability, guardian.Id));
    }

    private static void TakadaInvest()
    {
        const string ability = "wlpa-takada-invest";
        var def = Def(ability);
        string discard = "";
        var (e, id) = Buy(def, s => { s.Players[0].Clan = "Spider"; discard = Add(s, "training-focus-strategy", 0, Zone.Hand).Id; });
        var focus = CardCatalog.Get("training-focus-strategy").Focus;
        Check(focus > 0, "the fixture has a Focus Value");
        var command = InWindow(e, "entryReaction", 0, ability, id, direct: x => Use(x, 0, ability, id, discard) with { SourceIds = x.PaymentOptions(0, 3).First().SourceIds });
        Check(command.SourceIds is { Length: > 0 }, "the engine suggests the 3 Gold payment");
        var honor = Honor(e, 0);
        Accept(e, command);
        Eq(Honor(e, 0), honor + focus, "Honor equal to the discarded card's Focus Value");
        Eq(e.State.Cards[discard].Zone, Zone.FateDiscard, "the card was discarded");
        Check(command.SourceIds!.All(g => e.State.Cards[g].Bowed), "the Invest Gold was paid");
        Check(!CanUse(e, 0, ability, id), "once");
        // Declined costs nothing.
        string d2 = "";
        var (f, id2) = Buy(def, s => { s.Players[0].Clan = "Spider"; d2 = Add(s, "training-focus-strategy", 0, Zone.Hand).Id; });
        var h0 = Honor(f, 0);
        Settle(f);
        Eq(Honor(f, 0), h0, "declined: no Honor"); Eq(f.State.Cards[d2].Zone, Zone.Hand, "and the card is kept");
        // A Takada already in play offers no Invest.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var inPlay = Add(s2, def, 0, Zone.Home); Add(s2, "training-focus-strategy", 0, Zone.Hand); Gold(s2, 0, 4);
        var h = MatchEngine.FromTestState(s2);
        Check(!CanUse(h, 0, ability, inPlay.Id), "Invest is only for the moment of entering play");
        Check(id2.Length > 0);
    }

    private static void GojiroInvest()
    {
        const string ability = "wl1-gojiro-invest";
        var def = Def(ability);
        var (e, id) = Buy(def);
        var honor = Honor(e, 0);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Check(Options(e, 0, ability, id).First(o => o.Enabled).Gold?.Cost == 4, "Invest 4 Gold");
        var force = e.CardForceForTest(id);
        Accept(e, command);
        Eq(e.CardForceForTest(id), force + 2, "two +1F tokens");
        Eq(e.State.Cards[id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 2, "two +1F tokens on Gojiro");
        Check(command.SourceIds!.All(g => e.State.Cards[g].Bowed), "the 4 Gold were paid");
        Eq(Honor(e, 0), honor - 1, "After Gojiro enters play, lose 1 Honor");
        Check(!CanUse(e, 0, ability, id), "once");
        // Declined: no tokens, the printed Honor loss stands.
        var (f, id2) = Buy(def);
        var h2 = Honor(f, 0);
        Settle(f);
        Check(f.State.Cards[id2].Tokens.Count == 0, "no tokens when declined");
        Eq(Honor(f, 0), h2 - 1, "the entering Honor loss is not optional");
        var h = AtHome(def, out var gojiro, (s, _) => Gold(s, 0, 4));
        NeverOpened(h, 0, ability, gojiro.Id, command with { CardId = gojiro.Id });
    }

    private static void SengmaiKiho()
    {
        const string ability = "w732-sengmai-kiho";
        var def = Def(ability);
        var kiho = CardCatalog.Cards.Values.First(c => !c.ScenarioOnly && c.Id == "oracle-l5r-7557").Id;
        string discarded = "", plain = "";
        var (e, id) = Buy(def, s =>
        {
            s.Players[0].Honor = -3;
            discarded = Add(s, kiho, 0, Zone.FateDiscard).Id;
            plain = Add(s, "training-focus-strategy", 0, Zone.FateDiscard).Id;
        });
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Eq(e.State.PendingSearch is not null, true, "the discard-pile search opens");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: kiho));
        Eq(e.State.Cards[discarded].Zone, Zone.Hand, "the Kiho is in the hand");
        Check(e.State.Players[0].Hand.Contains(discarded), "listed in the hand");
        Eq(e.State.Cards[plain].Zone, Zone.FateDiscard, "a non-Kiho stays in the discard pile");
        var h = AtHome(def, out var sengmai, (s, _) => Add(s, kiho, 0, Zone.FateDiscard));
        NeverOpened(h, 0, ability, sengmai.Id, command with { CardId = sengmai.Id });
    }

    private static void SuikihimeLands()
    {
        const string ability = "suikihime-lands";
        var def = Def(ability, "Otomo Suikihime");
        var (e, id) = Buy(def);
        var count = e.State.Players[0].Provinces.Count;
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Eq(e.State.Players[0].Provinces.Count, count + 1, "a province gained left of the leftmost");
        Check(!CanUse(e, 0, ability, id), "once per game");
        Reject(e, command with { ExpectedVersion = e.Version });
        // Another player controls The Forgotten Temple: not offered.
        var (f, id2) = Buy(def, s => Add(s, "oracle-l5r-8094", 1, Zone.Home));
        Check(!CanUse(f, 0, ability, id2), "not offered while another player controls The Forgotten Temple: " + string.Join("; ", Options(f, 0, ability, id2).Select(o => $"{o.Label}[{o.Enabled}] {o.Reason}")));
        Eq(f.State.Players[0].Provinces.Count, count, "no province gained");
        var h = AtHome(def, out var suiki);
        NeverOpened(h, 0, ability, suiki.Id, command with { CardId = suiki.Id });
    }
    //MORE

    // ---- duel windows --------------------------------------------------------------------------------------------------------------------------------------------------------------
    // Player `c` challenges player 1-c with a plain duel; the challenged focuses from the deck, the challenger focuses a card, the challenged strikes: the window before Focus Effects
    // is open with the challenger's Personality having not struck.
    private static MatchEngine DuelBeforeFocusEffects(int c, string reactorDef, Zone reactorZone, out CardInstance mine, out CardInstance theirs, out CardInstance reactor, int mineChi = 0, int theirChi = 0, string mineDef = "training-crane-samurai", bool strikeFirst = false, Action<MatchState>? more = null)
    {
        var d = 1 - c;
        var s = Table(Phase.Battle, c);
        mine = Add(s, mineDef, c, Zone.Battlefield, province: 0);
        theirs = Add(s, "training-lion-samurai", d, Zone.Battlefield, province: 0);
        if (mineChi != 0) mine.Modifiers.Add(new("test", "test", "Chi", mineChi));
        if (theirChi != 0) theirs.Modifiers.Add(new("test", "test", "Chi", theirChi));
        var duelCard = Add(s, "training-plain-duel-strategy", c, Zone.Hand);
        reactor = Add(s, reactorDef, c, reactorZone, province: reactorZone == Zone.Battlefield ? 0 : null);
        Add(s, "training-focus-strategy", c, Zone.Hand);
        foreach (var p in new[] { 0, 1 }) { var top = Add(s, "training-focus-strategy", p, Zone.FateDeck); s.Players[p].FateDeck.Insert(0, top.Id); }
        more?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, c, "steel-on-steel-duel", duelCard.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", d, e.Version, Choice: "deck"));
        var focus = e.State.Players[c].Hand.First(id => e.State.Cards[id].DefinitionId == "training-focus-strategy");
        Accept(e, new GameCommand("focus", c, e.Version, CardId: focus));
        if (strikeFirst) { Accept(e, new GameCommand("focus", d, e.Version, Choice: "deck")); Accept(e, new GameCommand("strike", c, e.Version)); }
        else Accept(e, new GameCommand("strike", d, e.Version));
        return e;
    }

    private static void BendReedBoost()
    {
        const string ability = "bend-reed-boost";
        var def = Def(ability);
        foreach (var c in new[] { 0, 1 })
        {
            var e = DuelBeforeFocusEffects(c, def, Zone.Hand, out var mine, out var theirs, out var bend);
            Eq(e.State.PendingDuel?.Stage, "FocusEffectsReaction", "a window before the Focus Effects");
            var command = InWindow(e, "duelReaction", c, ability, bend.Id);
            Accept(e, command);
            Settle(e);
            var duel = e.State.LastDuel!.Result!;
            var (stat, foe) = c == 0 ? (duel.ChallengerStat, duel.ChallengedStat) : (duel.ChallengerStat, duel.ChallengedStat);
            Eq(stat, foe + 2, $"challenger {c}: +2 over the tied Chi 3 stats");
            Eq(e.State.Cards[theirs.Id].Zone, Zone.Dead, "the +2 turned the tie into a win: the loser was destroyed");
            Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield, "and his Personality survived");
            Eq(e.State.Cards[bend.Id].Zone, Zone.FateDiscard, "the copy was discarded");
            var control = DuelBeforeFocusEffects(c, def, Zone.Hand, out var m2, out _, out _); Settle(control);
            Eq(control.State.Cards[m2.Id].Zone, Zone.Dead, $"challenger {c}: declined, the tie is lost and the challenger is destroyed");
        }
        // The Personality struck himself: no window for his player.
        var s = DuelBeforeFocusEffects(0, def, Zone.Hand, out _, out _, out var b2, strikeFirst: true);
        Check(!CanUse(s, 0, ability, b2.Id) && !CanUse(s, 1, ability, b2.Id), "the player who struck holds no such reaction");
        Eq(s.State.Cards[b2.Id].Zone, Zone.Hand, "the copy stays in the hand");
        // One copy per duel: with two copies in hand, after one is played the other is not offered.
        var two = DuelBeforeFocusEffects(0, def, Zone.Hand, out _, out _, out var first, more: st => Add(st, def, 0, Zone.Hand));
        var second = two.State.Cards.Values.First(x => x.DefinitionId == def && x.Id != first.Id);
        Accept(two, Enabled(two, 0, ability, first.Id).Command!);
        Check(!CanUse(two, 0, ability, second.Id), "only one copy of Bend like a Reed per duel");
        // No duel at all: not offered, refused.
        var t = Table(Phase.Battle, 0);
        var lone = Add(t, def, 0, Zone.Hand);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var quiet = MatchEngine.FromTestState(t);
        NeverOpened(quiet, 0, ability, lone.Id, Use(quiet, 0, ability, lone.Id));
    }

    // A duel the Duelist's player `owner` fights, as challenger or challenged, with the Duelist's Chi 3 against Chi 5 (or equal when `lower` is false); Falling Rain Dojo (when `dojoFor` is
    // given) sits at that player's home. Driven to the window before Focus Effects.
    private static MatchEngine RainDuel(int c, bool duelistIsChallenger, bool lower, int? dojoFor, bool dojoBowed = false, bool duelistKeyword = true)
    {
        var d = 1 - c;
        var owner = duelistIsChallenger ? c : d;
        var s = Table(Phase.Action, c); s.PriorityPlayerId = c;
        var duelist = Add(s, duelistKeyword ? "training-duelist-personality" : "training-crane-samurai", owner, Zone.Home);
        var rival = Add(s, "training-lion-samurai", 1 - owner, Zone.Home);
        if (lower) rival.Modifiers.Add(new("test", "test", "Chi", 2));
        if (dojoFor is int who) Add(s, Def("falling-rain-boost"), who, Zone.Home, bowed: dojoBowed);
        var strategy = Add(s, "training-duel-open", c, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var (mine, theirs) = duelistIsChallenger ? (duelist, rival) : (rival, duelist);
        Accept(e, Offer(e, c, "training-duel-open", strategy.Id, theirs.Id, mine.Id));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && e.State.PendingDuel is { Stage: "Reaction" } && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, new("focus", d, e.Version, Choice: "deck"));
        Accept(e, new("strike", c, e.Version));
        for (var guard = 0; e.State.PendingDuel is { Stage: "DuelistRefocus" } && guard < 3; guard++) Accept(e, new("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        return e;
    }

    private static void FallingRainBoost()
    {
        const string ability = "falling-rain-boost";
        foreach (var c in new[] { 0, 1 })
            foreach (var asChallenger in new[] { true, false })
            {
                var owner = asChallenger ? c : 1 - c;
                var e = RainDuel(c, asChallenger, true, owner);
                var dojo = e.State.Cards.Values.Single(x => x.DefinitionId == Def(ability));
                Eq(e.State.PendingDuel?.Stage, "FocusEffectsReaction", $"challenger {c}, Duelist is the {(asChallenger ? "challenger" : "challenged")}: the window before Focus Effects");
                var command = InWindow(e, "duelReaction", owner, ability, dojo.Id);
                Accept(e, command);
                for (var guard = 0; e.State.PendingDuel is not null && guard < 12; guard++)
                    Accept(e, e.State.PendingChoice?.Type == "duelReaction" ? new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", e.State.PriorityPlayerId, e.Version));
                var result = e.State.LastDuel!.Result!;
                var (mine, foe) = owner == c ? (result.ChallengerStat, result.ChallengedStat) : (result.ChallengedStat, result.ChallengerStat);
                Eq(mine, 4, "the Duelist's stat is Chi 3 + 1");
                Eq(foe, 5, "the other stat is untouched");
                Check(!CanUse(e, owner, ability, dojo.Id), "once");
            }
        var def = Def(ability);
        // Equal stats, a non-Duelist, a bowed Dojo and the other player's Dojo: no window at all.
        Check(RainDuel(0, true, false, 0).State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "an equal duel stat is not lower");
        Check(RainDuel(0, true, true, 0, duelistKeyword: false).State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "his Personality must be a Duelist");
        Check(RainDuel(0, true, true, 0, dojoBowed: true).State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "a bowed Dojo cannot react");
        Check(RainDuel(0, true, true, 1).State.PendingDuel is not { Stage: "FocusEffectsReaction" }, "only the Dojo of the Duelist's controller works");
        // Out of any duel: not offered, refused.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var home = Add(s, def, 0, Zone.Home);
        var h = MatchEngine.FromTestState(s);
        NeverOpened(h, 0, ability, home.Id, Use(h, 0, ability, home.Id));
    }

    private static void ChampionRevenge()
    {
        const string ability = "p2-champion-court-revenge";
        var def = Def(ability);
        MatchEngine Rig(string? keyword, bool withYojimbo, out CardInstance champion, out CardInstance victim, out CardInstance yojimbo)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            victim = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
            if (keyword is not null) Kw(victim, keyword);
            yojimbo = Add(s, "training-samurai", 0, Zone.Home);
            if (withYojimbo) Kw(yojimbo, "Yojimbo");
            champion = Add(s, def, 0, Zone.Hand);
            var kazuko = Add(s, "oracle-l5r-1734", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "kazuko-fear-melee", kazuko.Id, victim.Id));
            for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 8; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        foreach (var keyword in new[] { "Courtier", "Shugenja" })
        {
            var e = Rig(keyword, true, out var champion, out var victim, out var yojimbo);
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, keyword + " was destroyed by the other player's action");
            var force = e.CardForceForTest(yojimbo.Id);
            var command = InWindow(e, "afterActionResolvedReaction", 0, ability, champion.Id, yojimbo.Id);
            Accept(e, command);
            Eq(e.State.Cards[yojimbo.Id].Tokens.Count(t => t.Type == "Revenge" && t.Stat == "Force" && t.Amount == 1), 3, "three +1F Revenge tokens on the Yojimbo");
            Eq(e.CardForceForTest(yojimbo.Id), force + 3, "+3F");
            Eq(e.State.Cards[champion.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        }
        var plain = Rig(null, true, out var c2, out var v2, out _);
        Eq(plain.State.Cards[v2.Id].Zone, Zone.Dead, "a plain Personality was destroyed");
        Check(!CanUse(plain, 0, ability, c2.Id), "a destroyed non-Courtier, non-Shugenja: not offered");
        var none = Rig("Courtier", false, out var c3, out _, out _);
        Check(!CanUse(none, 0, ability, c3.Id), "no Yojimbo to perform: not offered");
        // Not before the destruction: the action's window is still the Attack's.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var yo = Kw(Add(s, "training-samurai", 0, Zone.Home), "Yojimbo");
        var c4 = Add(s, def, 0, Zone.Hand);
        var quiet = MatchEngine.FromTestState(s);
        NeverOpened(quiet, 0, ability, c4.Id, Use(quiet, 0, ability, c4.Id, null, yo.Id));
    }

    private static void YakushoImmunity()
    {
        const string ability = "tsuruchi-yakusho-immunity";
        var def = Def(ability);
        MatchEngine Rig(bool yakushoAtBattle, out CardInstance yakusho, out CardInstance attacker, out CardInstance melee, out CardInstance reactor, out CardInstance target)
        {
            var s = Table(Phase.Battle, 0);
            yakusho = Add(s, def, 0, yakushoAtBattle ? Zone.Battlefield : Zone.Home, province: yakushoAtBattle ? 0 : null);
            attacker = Add(s, "training-dual-attacker", 0, Zone.Battlefield, province: 0);
            melee = Add(s, "training-dual-attacker", 0, Zone.Battlefield, province: 0);
            reactor = Add(s, "training-attack-reactor", 1, Zone.Home);
            target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var yakusho, out var attacker, out _, out var reactor, out var target);
        Accept(e, Offer(e, 0, "training-honor-cost-attack", attacker.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "announceReaction", "a Ranged action announced: the announce window");
        Check(CanUse(e, 0, ability, yakusho.Id), "offered to his controller");
        Check(!CanUse(e, 1, ability, yakusho.Id), "not listed for the other player");
        Reject(e, Use(e, 1, ability, yakusho.Id));
        Accept(e, Use(e, 0, ability, yakusho.Id));
        for (var guard = 0; e.State.PendingChoice?.Type == "announceReaction" && guard < 4; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack's own window");
        Reject(e, Use(e, 1, "training-attack-negate", reactor.Id));
        Settle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the Ranged 3 Attack, not reduced, destroys the Force 3 target");
        // Control: without Yakusho's Reaction the same negate lands.
        var c = Rig(true, out _, out var a2, out _, out var r2, out var t2);
        Accept(c, Offer(c, 0, "training-honor-cost-attack", a2.Id, t2.Id));
        for (var guard = 0; c.State.PendingChoice?.Type == "announceReaction" && guard < 4; guard++) Accept(c, new GameCommand("declineReaction", c.State.PriorityPlayerId, c.Version));
        Accept(c, Use(c, 1, "training-attack-negate", r2.Id));
        Settle(c);
        Eq(c.State.Cards[t2.Id].Zone, Zone.Battlefield, "declined, the strength reduction lands and the target survives");
        // A Melee action announces nothing for him.
        var m = Rig(true, out var y3, out _, out var melee3, out _, out var t3);
        Accept(m, Offer(m, 0, "training-weapon-attack", melee3.Id, t3.Id));
        Check(m.State.PendingChoice?.Type != "announceReaction" && !CanUse(m, 0, ability, y3.Id), "a Melee action opens nothing for Yakusho");
        // Yakusho at home, not at the current battlefield: not offered.
        var away = Rig(false, out var y4, out var a4, out _, out _, out var t4);
        Accept(away, Offer(away, 0, "training-honor-cost-attack", a4.Id, t4.Id));
        Check(!CanUse(away, 0, ability, y4.Id), "Yakusho must be at the current battlefield");
        // No action announced: refused.
        var quiet = Rig(true, out var y5, out _, out _, out _, out _);
        NeverOpened(quiet, 0, ability, y5.Id, Use(quiet, 0, ability, y5.Id));
    }

    private static void DaisukeDraw()
    {
        const string ability = "daisuke-exp-draw";
        var def = Def(ability);
        MatchEngine Rig(bool wins, out CardInstance daisuke)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            daisuke = Add(s, def, 1, Zone.Battlefield, province: 0);
            var challenge = Add(s, "training-duel-challenge", 1, Zone.Hand);
            var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (!wins) enemy.Modifiers.Add(new("test", "test", "Chi", 6));
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 1, "training-duel-challenge", challenge.Id, enemy.Id, daisuke.Id));
            Accept(e, new GameCommand("acceptDuel", 0, e.Version));
            Accept(e, new GameCommand("strike", 0, e.Version));
            for (var guard = 0; e.State.PendingDuel is { Stage: "DuelistRefocus" } && guard < 3; guard++) Accept(e, new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        var e = Rig(true, out var daisuke);
        Eq(e.State.PendingChoice?.Type, "duelWonReaction", "the winner's window");
        Eq(e.State.PendingChoice?.CardId, daisuke.Id, "scoped to the card that won");
        var command = InWindow(e, "duelWonReaction", 1, ability, daisuke.Id);
        var hand = e.State.Players[1].Hand.Count; var theirs = e.State.Players[0].Hand.Count;
        Accept(e, command);
        Eq(e.State.Players[1].Hand.Count, hand + 1, "a card was drawn");
        Eq(e.State.Players[0].Hand.Count, theirs, "the opponent draws nothing");
        Reject(e, command with { ExpectedVersion = e.Version });
        var lost = Rig(false, out var d2);
        Check(lost.State.PendingChoice?.Type != "duelWonReaction" && !CanUse(lost, 1, ability, d2.Id), "he lost the duel: no window");
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var idle = Add(s, def, 1, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(s);
        NeverOpened(q, 1, ability, idle.Id, command with { CardId = idle.Id });
    }
    // ---- engage windows ---------------------------------------------------------------------------------------------------------------------------------------------------------
    // Player 0 attacks battlefield 0 (the Defender's province 0); `arrange` lays out the table; the Engage window opens when someone holds a legal option.
    private static MatchEngine Engage(Action<MatchState> arrange)
    {
        var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        return e;
    }

    private static void PassBattle(MatchEngine e, bool untilResolved = false)
    {
        for (var guard = 0; guard < 40 && e.State.Phase == Phase.Battle && !(untilResolved && e.State.Events.Any(x => x.Type == "battleResolved")); guard++)
            Accept(e, e.State.PendingChoice is not null ? new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version) : new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
    }

    private static void JadesRoarRecon()
    {
        const string ability = "jades-roar-recon", terrainDef = "oracle-l5r-2996";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance roar = null!, hand = null!, bower = null!, foe = null!;
            MatchEngine Rig() => Engage(s =>
            {
                Add(s, "training-crane-samurai", o, Zone.Battlefield, province: 0);
                foe = Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
                roar = Add(s, def, o, Zone.Battlefield, province: 0);
                bower = Add(s, "training-battle-bower", o, Zone.Battlefield, province: 0);
                hand = Add(s, terrainDef, o, Zone.Hand);
                Add(s, def, o, Zone.Home);
            });
            var e = Rig();
            var home = e.State.Cards.Values.Single(c => c.DefinitionId == def && c.Zone == Zone.Home);
            var command = InWindow(e, "engageReaction", o, ability, roar.Id);
            Check(!CanUse(e, o, ability, home.Id), "a Roar at home is not at the battlefield");
            Reject(e, command with { CardId = home.Id });
            Accept(e, command); Settle(e);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[o], $"player {o} has Reconnaissance at the battlefield");
            Eq(e.State.FirstActionPlayerId, o, "the Reaction gives its controller the first opportunity");
            Eq(e.State.FirstActionRestriction?.ActionKeyword, "Terrain", "which must be a Terrain action");
            Check(Rejection(e, Use(e, o, "training-bow-at-battle", bower.Id, foe.Id)).Contains("Terrain"), "a non-Terrain action is refused");
            Accept(e, new GameCommand("playStrategy", o, e.Version, hand.Id));
            Eq(e.State.Cards[hand.Id].Zone, Zone.Battlefield, "the Terrain action was accepted");
            Check(e.State.FirstActionRestriction is null, "the limit binds only that first action");
            var control = Rig(); Settle(control);
            Check(!control.State.Players[1].Provinces[0].Reconnaissance[o] && control.State.FirstActionRestriction is null, "declined, no Reconnaissance and no restriction");
        }
        var t = Table(Phase.Battle, 0);
        var lone = Add(t, def, 0, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, lone.Id, Use(q, 0, ability, lone.Id));
    }

    private static void KoshinKeepRecon()
    {
        const string ability = "koshin-keep-recon";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance archer = null!, target = null!;
            MatchEngine Rig() => Engage(s =>
            {
                s.Cards[s.Players[o].StrongholdId].DefinitionId = def;
                archer = Add(s, "training-recon-actor", o, Zone.Battlefield, province: 0);
                target = Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
                Force(target, 2 - CardCatalog.Get("training-lion-samurai").Force);
                target.DestructionShieldTurn = s.TurnNumber;
            });
            var e = Rig();
            var keep = e.State.Cards[e.State.Players[o].StrongholdId];
            var command = InWindow(e, "engageReaction", o, ability, keep.Id);
            Accept(e, command); Settle(e);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[o], $"player {o} has Reconnaissance at the current battlefield");
            for (var guard = 0; e.State.PriorityPlayerId != o && guard < 4; guard++) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Accept(e, Offer(e, o, "training-recon-attack", archer.Id, target.Id)); Settle(e);
            Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the Ranged 2 Attack did not destroy its shielded target");
            Eq(e.State.Cards[target.Id].Modifiers.Single(m => m.Effect == "koshin-keep-recon").Amount, -2, "its Force was reduced by the Attack's strength");
            Check(e.State.AdditionalActions.Any(g => g.PlayerId == o && g.RangedTargetId == target.Id), "an additional Ranged Attack on the same card was granted");
            var control = Rig(); Settle(control);
            Check(!control.State.Players[1].Provinces[0].Reconnaissance[o], "declined, no Reconnaissance");
        }
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        t.Cards[t.Players[0].StrongholdId].DefinitionId = def;
        var h = MatchEngine.FromTestState(t);
        NeverOpened(h, 0, ability, t.Players[0].StrongholdId, Use(h, 0, ability, t.Players[0].StrongholdId));
    }

    private static void KensaisBladeAttach()
    {
        const string ability = "kensais-blade-attach";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance blade = null!, atBattle = null!, atHome = null!;
            var e = Engage(s =>
            {
                atBattle = Add(s, "training-crane-samurai", o, Zone.Battlefield, province: 0);
                atHome = Add(s, "training-crane-veteran", o, Zone.Home);
                Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
                blade = Add(s, def, o, Zone.Hand); Gold(s, o, 3);
            });
            var command = InWindow(e, "engageReaction", o, ability, blade.Id, atBattle.Id);
            Check(Options(e, o, ability, blade.Id, atBattle.Id).First(x => x.Enabled).Gold is { Cost: 3 }, "paying all costs: its Gold cost of 3");
            Check(!CanUse(e, o, ability, blade.Id, atHome.Id), "the performing Personality must be at the current battlefield: not one at home");
            Accept(e, command);
            Check(e.State.Cards[blade.Id].Zone == Zone.Attached && e.State.Cards[blade.Id].AttachedTo == atBattle.Id, "attached to him");
            Check(!e.State.Players[o].Hand.Contains(blade.Id), "and out of the hand");
            Check(command.SourceIds is { Length: > 0 } && command.SourceIds.All(g => e.State.Cards[g].Bowed), "the 3 Gold were paid");
        }
        var t = Table(Phase.Battle, 0);
        var host = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var inHand = Add(t, def, 0, Zone.Hand); Gold(t, 0, 3);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, inHand.Id, Use(q, 0, ability, inHand.Id, null, host.Id));
    }

    private static void NakanuTechnique()
    {
        const string ability = "nakanu-technique-create";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance tech = null!, mine = null!, bowed = null!;
            MatchEngine Rig() => Engage(s =>
            {
                mine = Add(s, "training-crane-samurai", o, Zone.Battlefield, province: 0);
                bowed = Add(s, "training-crane-veteran", o, Zone.Battlefield, bowed: true, province: 0);
                Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
                tech = Add(s, def, o, Zone.Hand);
            });
            var e = Rig();
            var before = e.State.Cards.Keys.ToHashSet();
            var command = InWindow(e, "engageReaction", o, ability, tech.Id, mine.Id);
            Check(!CanUse(e, o, ability, tech.Id, bowed.Id), "a bowed Personality may not perform");
            var honor = Honor(e, o);
            Accept(e, command);
            var made = e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();
            Eq(made.Length, 2, "two Followers created");
            Check(made.All(c => c.OwnerId == o && c.Zone == Zone.Attached && c.AttachedTo == mine.Id), "attached to the performer");
            Check(made.All(c => CardCatalog.Get(c.DefinitionId) is { Type: CardType.Follower, Force: 1 } d && (d.Keywords ?? []).Contains("Shadowlands") && (d.Keywords ?? []).Contains("Undead")), "each a 1F Shadowlands Undead Follower");
            Eq(Honor(e, o), honor - 1, "Lose 1 Honor");
            Eq(e.State.Cards[tech.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        }
        var t = Table(Phase.Battle, 0);
        var card = Add(t, def, 0, Zone.Hand);
        var p = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, card.Id, Use(q, 0, ability, card.Id, null, p.Id));
    }

    private static void RecruitmentOfficer()
    {
        const string ability = "pcc-recruitment-officer";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance officer = null!;
            MatchEngine Rig(bool bowedOfficer = false) => Engage(s =>
            {
                Add(s, "training-crane-samurai", o, Zone.Battlefield, province: 0);
                Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
                officer = Add(s, def, o, Zone.Home, bowed: bowedOfficer);
                if (bowedOfficer) Add(s, def, o, Zone.Home);
            });
            var e = Rig();
            var before = e.State.Cards.Keys.ToHashSet();
            var command = InWindow(e, "engageReaction", o, ability, officer.Id);
            Accept(e, command);
            Check(e.State.Cards[officer.Id].Bowed, "bowing is the cost");
            var recruit = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
            var d = CardCatalog.Get(recruit.DefinitionId);
            Check(recruit.Zone == Zone.Battlefield && recruit.ProvinceIndex == 0 && recruit.OwnerId == o, "created at the battlefield on its player's side");
            Check(d.Type == CardType.Personality && d.Force == 2 && d.Chi == 2 && d.PersonalHonor == 2 && (d.Keywords ?? []).Contains("Ronin") && (d.Keywords ?? []).Contains("Samurai"), "a 2F/2C/2PH Ronin Samurai Personality");
            Check(e.State.MechD.TimedRemovals.Any(r => r.CardId == recruit.Id && r.When == TimedRemovalWhen.TurnEnds), "removed from the game when this turn ends");
            Check(!CanUse(e, o, ability, officer.Id), "a bowed Officer offers nothing more");
            var bowedOne = Rig(true);
            Check(!CanUse(bowedOne, o, ability, officer.Id), "a bowed Officer is not offered");
        }
        // No unit of the Defender's at the battlefield: the Defender's Officer is not offered while the Attacker's is.
        CardInstance mineOfficer = null!, theirOfficer = null!;
        var n = Engage(s =>
        {
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            mineOfficer = Add(s, def, 0, Zone.Home); theirOfficer = Add(s, def, 1, Zone.Home);
        });
        Accept(n, new GameCommand("declineReaction", 0, n.Version));
        Check(n.State.PendingChoice?.Type == "engageReaction" || n.State.PendingChoice is null, "the window state");
        Check(!CanUse(n, 1, ability, theirOfficer.Id), "no unit of yours there: not offered");
        Check(mineOfficer.Id.Length > 0);
        var t = Table(Phase.Battle, 0);
        var lone = Add(t, def, 0, Zone.Home);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, lone.Id, Use(q, 0, ability, lone.Id));
    }

    private static void RokkuMove()
    {
        const string ability = "rokku-move";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance rokku = null!, follower = null!;
            var e = Engage(s =>
            {
                Add(s, "training-crane-samurai", 1 - o, Zone.Battlefield, province: 0);
                if (o == 1) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                rokku = Add(s, def, o, Zone.Home, bowed: true);
                follower = Add(s, "training-follower", o, Zone.Attached, bowed: true, parent: rokku.Id);
                if (o == 0) Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
                else Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            });
            var command = InWindow(e, "engageReaction", o, ability, rokku.Id);
            Accept(e, command);
            Check(e.State.Cards[rokku.Id].Zone == Zone.Battlefield && e.State.Cards[rokku.Id].ProvinceIndex == 0, "moved to the engaged battlefield");
            Check(!e.State.Cards[rokku.Id].Bowed && !e.State.Cards[follower.Id].Bowed, "his unit straightened since he moved");
            Settle(e);
        }
        // No enemy unit at the battlefield: not offered, and nothing happens.
        CardInstance lonely = null!;
        var n = Engage(s => { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); lonely = Add(s, def, 0, Zone.Home, bowed: true); });
        Check(n.State.PendingChoice?.Type != "engageReaction" && !CanUse(n, 0, ability, lonely.Id), "no enemy unit there: no window and not offered: " + n.State.PendingChoice?.Type + " " + string.Join("; ", Options(n, 0, ability, lonely.Id).Select(x => x.Label + x.Enabled + x.Reason)));
        Eq(n.State.Cards[lonely.Id].Zone, Zone.Home, "he stays home");
        var t = Table(Phase.Battle, 0);
        var idle = Add(t, def, 0, Zone.Home, bowed: true);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, idle.Id, Use(q, 0, ability, idle.Id));
    }

    private static void RyokoPenalty()
    {
        const string ability = "ryoko-penalty";
        var def = Def(ability);
        var honor = CardCatalog.Get(def).PersonalHonor;
        Check(honor > 0, "Ryoko has Personal Honor");
        foreach (var o in new[] { 0, 1 })
            foreach (var viaFollower in new[] { false, true })
            {
                CardInstance ryoko = null!, ally = null!, enemy = null!, follower = null!;
                var e = Engage(s =>
                {
                    ryoko = Add(s, def, o, Zone.Battlefield, province: 0);
                    ally = Add(s, "training-crane-samurai", o, Zone.Battlefield, province: 0);
                    enemy = Add(s, "training-lion-veteran", 1 - o, Zone.Battlefield, province: 0);
                    follower = Add(s, "training-follower", 1 - o, Zone.Attached, province: 0, parent: enemy.Id);
                });
                var victim = viaFollower ? follower : enemy;
                var command = InWindow(e, "engageReaction", o, ability, ryoko.Id, null, victim.Id);
                Check(Options(e, o, ability, ryoko.Id).First(x => x.Enabled).TargetIds is { } ids && ids.Contains(enemy.Id) && ids.Contains(follower.Id) && !ids.Contains(ally.Id), "an opposing Personality or Follower, not her own side");
                var force = e.CardForceForTest(victim.Id);
                Accept(e, command);
                Eq(e.CardForceForTest(victim.Id), Math.Max(0, force - honor), $"a Force penalty equal to her Personal Honor ({honor})");
            }
        var t = Table(Phase.BattleSelection, 0); t.CurrentBattleProvince = null; t.UnresolvedBattlefields = [0];
        var home = Add(t, def, 0, Zone.Home);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var h = MatchEngine.FromTestState(t);
        Accept(h, h.QueryActions(0).First(a => a.Type == "selectBattle" && a.Enabled).Command!);
        Check(!CanUse(h, 0, ability, home.Id), "Ryoko at home does not react to an engagement at a battlefield she is not at");
    }

    private static void WavePersistsMove()
    {
        const string ability = "wave-persists-move";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance a = null!, shugenja = null!, spell = null!, foe = null!;
            MatchEngine Rig() => Engage(s =>
            {
                a = Add(s, "training-scout-samurai", o, Zone.Battlefield, province: 0);
                shugenja = Add(s, "training-water-shugenja", o, Zone.Home);
                spell = Add(s, def, o, Zone.Attached, parent: shugenja.Id);
                foe = Add(s, "training-crane-samurai", 1 - o, Zone.Battlefield, province: 0);
            });
            var e = Rig();
            var command = InWindow(e, "engageReaction", o, ability, spell.Id, null, a.Id);
            Accept(e, command);
            Eq(e.State.Cards[a.Id].Zone, Zone.Battlefield, "the move waits until the battle resolves");
            Eq(e.State.DelayedBattleEffects.Count, 1, "registered, not applied");
            Settle(e); PassBattle(e, true);
            var log = e.State.Events.Select(x => x.Type).ToList();
            Check(log.IndexOf("unitMovedHome") >= 0 && log.IndexOf("unitMovedToBattlefield") > log.IndexOf("unitMovedHome"), "the target goes home first, then the Shugenja moves");
            Eq(e.State.Cards[a.Id].Zone, Zone.Home, "the target is home");
            Check(e.State.Events.Single(x => x.Type == "unitMovedToBattlefield").Message.Contains("Water Shugenja"), "the Shugenja moved to the battlefield");
            Check(e.State.Events.Single(x => x.Type == "battleResolved").Message.Contains(o == 0 ? "attacking Force 4, defending Force 3" : "attacking Force 3, defending Force 4"), "he fought in the moved unit's place: Force 2 + 2F Water against 3");
            Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "and destroyed the opposing army");
            var control = Rig(); Settle(control); PassBattle(control, true);
            Eq(control.State.Cards[a.Id].Zone, Zone.Dead, "declined, the Force 2 Personality loses to Force 3");
        }
        // The Shugenja already at the battlefield: no Reaction (and nothing to open).
        var s2 = Table(Phase.BattleSelection, 0); s2.CurrentBattleProvince = null; s2.UnresolvedBattlefields = [0];
        var ally = Add(s2, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var sh2 = Add(s2, "training-water-shugenja", 0, Zone.Battlefield, province: 0);
        var sp2 = Add(s2, def, 0, Zone.Attached, province: 0, parent: sh2.Id);
        Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var w = MatchEngine.FromTestState(s2);
        Accept(w, w.QueryActions(0).First(x => x.Type == "selectBattle" && x.Enabled).Command!);
        Check(w.State.PendingChoice is null && !CanUse(w, 0, ability, sp2.Id), "his Shugenja is at the battlefield: no Reaction");
        Reject(w, Use(w, 0, ability, sp2.Id, ally.Id));
    }

    private static void TsumaroIllusion()
    {
        const string ability = "w821-tsumaro-illusion";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance tsumaro = null!, air = null!, earth = null!;
            MatchEngine Rig() => Engage(s =>
            {
                tsumaro = Add(s, def, o, Zone.Home);
                air = Kw(Add(s, "training-shugenja", o, Zone.Battlefield, province: 0), "Air");
                earth = Add(s, "training-shugenja", o, Zone.Battlefield, province: 0);
                Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
            });
            var e = Rig();
            var before = e.State.Cards.Keys.ToHashSet();
            var command = InWindow(e, "engageReaction", o, ability, tsumaro.Id, null, air.Id);
            Reject(e, command with { TargetId = earth.Id });
            Check(!e.State.Cards.Values.Any(c => !before.Contains(c.Id)), "the refused non-Air target created nothing");
            Accept(e, command);
            Eq(e.State.Cards[air.Id].Zone, Zone.Home, "the Air Shugenja was moved home");
            var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
            var d = CardCatalog.Get(made.DefinitionId);
            Check(made.Zone == Zone.Battlefield && made.ProvinceIndex == 0 && made.OwnerId == o, "the Illusion stands at the current battlefield on its creator's side");
            Check(d.Force == 0 && d.Chi == 3 && d.PersonalHonor == 0 && new[] { "Air", "Illusion", "Shugenja" }.All(k => (d.Keywords ?? []).Contains(k)), "a 0F/3C/0PH Air Illusion Shugenja");
            Settle(e); PassBattle(e);
            Check(e.State.Cards[made.Id].Zone != Zone.Battlefield && e.State.Cards[made.Id].Zone != Zone.Home, "removed from the game after the battle ends: " + e.State.Cards[made.Id].Zone);
        }
        var t = Table(Phase.Battle, 0);
        var idle = Add(t, def, 0, Zone.Home);
        var sh = Kw(Add(t, "training-shugenja", 0, Zone.Battlefield, province: 0), "Air");
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, idle.Id, Use(q, 0, ability, idle.Id, sh.Id));
    }

    private static void EngageClaimPolitical()
    {
        const string ability = "training-engage-claim-political", def = "training-engage-political-claim-strategy";
        foreach (var o in new[] { 0, 1 })
        {
            CardInstance card = null!, mine = null!, foe = null!;
            MatchEngine Rig() => Engage(s =>
            {
                mine = Add(s, "training-crane-samurai", o, Zone.Battlefield, province: 0);
                foe = Add(s, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
                card = Add(s, def, o, Zone.Hand);
            });
            var e = Rig();
            var command = InWindow(e, "engageReaction", o, ability, card.Id, null, foe.Id);
            Reject(e, command with { TargetId = mine.Id });
            Accept(e, command);
            Eq(e.State.PendingEngage!.Claims.Count, 1, "the claim was made");
            Settle(e);
            Eq(e.State.FirstActionPlayerId, 0, "the Attacker has the first opportunity to take a Battle action or pass");
            var control = Rig(); Settle(control);
            Eq(control.State.FirstActionPlayerId, 1, "declined, the Defender keeps the default first opportunity");
        }
        var t = Table(Phase.Battle, 0);
        var idle = Add(t, def, 0, Zone.Hand);
        var foe2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, idle.Id, Use(q, 0, ability, idle.Id, foe2.Id));
    }
    // ---- after-action, announce, payment and other windows ----------------------------------------------------------------------------------------------------------------------
    private static void ToAfterAction(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { } p && p.Type != "afterActionResolvedReaction" && guard < 8; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void SihungBoost()
    {
        const string ability = "wlpt-sihung-boost", taigo = "oracle-l5r-10100";
        var def = Def(ability);
        MatchEngine Rig(int actor, int holder, string performerDef, string action, out CardInstance sihung, out CardInstance performer, bool act = true)
        {
            var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor;
            sihung = Add(s, def, holder, Zone.Battlefield, province: 0);
            performer = Add(s, performerDef, actor, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1 - actor, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, action == "taigo-tactical-bow" ? Use(e, actor, action, performer.Id, selections: Pick("target", enemy.Id)) : Offer(e, actor, action, performer.Id, enemy.Id));
            ToAfterAction(e);
            return e;
        }
        foreach (var o in new[] { 0, 1 })
        {
            var e = Rig(o, o, taigo, "taigo-tactical-bow", out var sihung, out var performer);
            var (sihungForce, force) = (e.CardForceForTest(sihung.Id), e.CardForceForTest(performer.Id));
            var command = InWindow(e, "afterActionResolvedReaction", o, ability, sihung.Id);
            Accept(e, command);
            Eq(e.CardForceForTest(performer.Id), force + 2, "the performer has +2F");
            Eq(e.CardForceForTest(sihung.Id), sihungForce, "Sihung himself is not the performer");
            var plain = Rig(o, o, "training-battle-bower", "training-bow-at-battle", out var s2, out _);
            Check(!CanUse(plain, o, ability, s2.Id), "a non-Tactical action does not offer it");
            var theirs = Rig(1 - o, o, taigo, "taigo-tactical-bow", out var s3, out var tp);
            Check(!CanUse(theirs, o, ability, s3.Id), "another player's Tactical action is not yours");
            Eq(theirs.CardForceForTest(tp.Id), CardCatalog.Get(taigo).Force, "no bonus for the other side's performer");
            var quiet = Rig(o, o, taigo, "taigo-tactical-bow", out var s4, out _, act: false);
            NeverOpened(quiet, o, ability, s4.Id, command with { CardId = s4.Id });
        }
    }

    private static void StipendAnnounce()
    {
        const string ability = "w737-stipend-announce";
        var def = Def(ability);
        MatchEngine Rig(int o, bool magistrate, string action, bool battle, out CardInstance stipend, out CardInstance actor)
        {
            var s = Table(battle ? Phase.Battle : Phase.Action, battle ? o : 0); s.PriorityPlayerId = o;
            stipend = Add(s, def, o, Zone.Home);
            actor = Add(s, battle ? "training-crane-samurai" : "training-crane-samurai", o, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            Kw(actor, "Samurai"); if (magistrate) Kw(actor, "Magistrate");
            actor.GrantedAbilities.Add(new(action, "test-fixture", GrantDuration.Permanent));
            var foe = Add(s, "training-lion-samurai", 1 - o, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            var e = MatchEngine.FromTestState(s);
            Accept(e, action == "training-plain-open" ? Use(e, o, action, actor.Id) : Offer(e, o, action, actor.Id, foe.Id));
            return e;
        }
        foreach (var (o, battle, action) in new[] { (0, false, "training-plain-open"), (1, false, "training-plain-open"), (0, true, "training-bow-at-battle"), (1, true, "training-bow-at-battle") })
        {
            var e = Rig(o, true, action, battle, out var stipend, out var mag);
            Eq(e.State.PendingChoice?.Type, "announceReaction", "the announce window opens for the Stipend");
            var chi = e.CardChiForTest(mag.Id);
            var command = InWindow(e, "announceReaction", o, ability, stipend.Id, complete: c => c with { PerformerId = mag.Id });
            Accept(e, command);
            Check(e.State.Cards[mag.Id].Modifiers.Any(m => m.Stat == "Chi" && m.Amount == 1), $"+1C while the announced action is unresolved ({action}, player {o})");
            Eq(e.CardChiForTest(mag.Id), chi + 1, "his Chi is one higher");
            Settle(e);
            Check(!e.State.Cards[mag.Id].Modifiers.Any(m => m.Stat == "Chi" && m.Amount == 1), "and gone when the action ended");
            Eq(e.CardChiForTest(mag.Id), chi, "Chi is back where it was");
        }
        var plain = Rig(0, false, "training-plain-open", false, out var s2, out _);
        Check(plain.State.PendingChoice is not { Type: "announceReaction" } && !CanUse(plain, 0, ability, s2.Id), "a performer who is not a Magistrate opens no window");
        var iai = Rig(0, true, "daisuke-duel", true, out var s3, out _);
        Check(!CanUse(iai, 0, ability, s3.Id), "an Iaijutsu action is not a non-Iaijutsu action");
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var idle = Add(t, def, 0, Zone.Home);
        var m = Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Magistrate");
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, idle.Id, Use(q, 0, ability, idle.Id, null, m.Id));
    }

    private static void MempoTakeFavor()
    {
        const string ability = "pe-mempo-take-favor";
        var def = Def(ability);
        MatchState Published(MatchState s) { foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }
        MatchEngine CycleRig(int favorHolder, int mempoOwner, out CardInstance mempo, out CardInstance spare)
        {
            var s = Published(Table(Phase.Action, favorHolder)); s.PriorityPlayerId = favorHolder; s.FavorPlayerId = favorHolder;
            var champion = Kw(Add(s, mempoOwner == 1 ? "training-lion-samurai" : "training-crane-samurai", mempoOwner, Zone.Home), "Amethyst Champion");
            mempo = Add(s, def, mempoOwner, Zone.Attached, parent: champion.Id);
            spare = Add(s, "training-lion-samurai", favorHolder, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        foreach (var o in new[] { 0, 1 })
        {
            var e = CycleRig(o, o, out var mempo, out var spare);
            Accept(e, new GameCommand("useAbility", o, e.Version, AbilityId: "favor-cycle", TargetId: spare.Id));
            Eq(e.State.FavorPlayerId, null, "the Favor was discarded as the cost");
            var command = InWindow(e, "afterActionResolvedReaction", o, ability, mempo.Id);
            Accept(e, command);
            Eq(e.State.FavorPlayerId, o, "he took the Imperial Favor");
            Check(e.State.Events.Any(x => x.Type == "favorTaken"), "recorded as a take");
            var other = CycleRig(1 - o, o, out var m2, out var sp2);
            Accept(other, new GameCommand("useAbility", 1 - o, other.Version, AbilityId: "favor-cycle", TargetId: sp2.Id));
            Check(!CanUse(other, o, ability, m2.Id), "an opponent's discard is not 'you discard'");
            var quiet = CycleRig(o, o, out var m3, out _);
            NeverOpened(quiet, o, ability, m3.Id, command with { CardId = m3.Id });
            var declined = CycleRig(o, o, out _, out var sp4);
            Accept(declined, new GameCommand("useAbility", o, declined.Version, AbilityId: "favor-cycle", TargetId: sp4.Id));
            Settle(declined);
            Eq(declined.State.FavorPlayerId, null, "declined, the Imperial Favor stays discarded");
        }
        // The Battle action that sends a Personality home discards the Favor too.
        var s = Published(Table(Phase.Battle, 1)); s.PriorityPlayerId = 1; s.FavorPlayerId = 1;
        var attacker = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Amethyst Champion");
        var held = Add(s, def, 1, Zone.Attached, province: 0, parent: attacker.Id);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        Accept(b, Offer(b, 1, "favor-battle", null!, defender.Id));
        Eq(b.State.FavorPlayerId, null, "favor-battle discarded the Favor");
        Accept(b, InWindow(b, "afterActionResolvedReaction", 1, ability, held.Id));
        Eq(b.State.FavorPlayerId, 1, "taken back after the Battle action");
    }

    private static void IvoryDiscard()
    {
        const string ability = "pe-ivory-discard";
        var def = Def(ability);
        MatchEngine Rig(bool dishonorable, bool performerIsOwners, bool giveHand, out CardInstance ivory, out CardInstance held)
        {
            var s = Table(Phase.Battle, performerIsOwners ? 0 : 1); s.PriorityPlayerId = s.ActivePlayerId;
            var wearer = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ivory Champion");
            ivory = Add(s, def, 0, Zone.Attached, province: 0, parent: wearer.Id);
            var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", performerIsOwners ? 0 : 1, Zone.Battlefield, province: 0); bower.Dishonorable = dishonorable;
            held = Add(s, "training-lion-samurai", 1, Zone.Hand); if (giveHand) Add(s, "training-crane-samurai", 1, Zone.Hand); else s.Players[1].Hand.Clear();
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, performerIsOwners ? 0 : 1, Bow, bower.Id, performerIsOwners ? target.Id : wearer.Id));
            ToAfterAction(e);
            return e;
        }
        var e = Rig(true, false, true, out var ivory, out var held);
        var hand = e.State.Players[1].Hand.Count;
        var command = InWindow(e, "afterActionResolvedReaction", 0, ability, ivory.Id);
        Accept(e, command);
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "his controller chooses");
        Eq(e.State.PendingChoice?.PlayerId, 1, "player 1, the dishonorable Personality's controller, decides");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseDecider" && a.Enabled && a.Command!.TargetId == held.Id).Command!);
        Eq(e.State.Cards[held.Id].Zone, Zone.FateDiscard, "the card he chose was discarded");
        Eq(e.State.Players[1].Hand.Count, hand - 1, "exactly one card");
        var honorable = Rig(false, false, true, out var i2, out _);
        Check(!CanUse(honorable, 0, ability, i2.Id), "an honorable performer: no discard");
        var empty = Rig(true, false, false, out var i3, out _);
        Check(!CanUse(empty, 0, ability, i3.Id), "an empty hand has nothing to discard");
        var own = Rig(true, true, true, out var i4, out _);
        Check(!CanUse(own, 0, ability, i4.Id), "the Sword owner's own dishonorable Personality is not an opposing one");
        var t = Table(Phase.Battle, 1);
        var w = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var idle = Add(t, def, 0, Zone.Attached, province: 0, parent: w.Id);
        var q = MatchEngine.FromTestState(t);
        NeverOpened(q, 0, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void DeedsAndWords()
    {
        const string ability = "deeds-words-reduce";
        var def = Def(ability);
        var tsujiken = Def("tsujiken-exp-dishonor");
        MatchEngine Rig(int deedsOwner, Phase phase, bool act, out CardInstance deeds, out CardInstance actor, out CardInstance victim)
        {
            var s = Table(phase, 1); s.PriorityPlayerId = 1;
            deeds = Add(s, def, deedsOwner, Zone.Home);
            actor = Add(s, tsujiken, 1, Zone.Home);
            victim = Add(s, "training-crane-samurai", 0, Zone.Home);
            s.Players[0].Honor = 12; s.Players[1].Honor = 12;
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, Offer(e, 1, "tsujiken-exp-dishonor", actor.Id, victim.Id));
            return e;
        }
        foreach (var owner in new[] { 0, 1 })
        {
            var e = Rig(owner, Phase.Action, true, out var deeds, out var actor, out var victim);
            Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the window opens before the Honor loss");
            Check(e.State.PendingHonorChange is { Amount: -1, PlayerId: 0 }, "it names the loss: 1 Honor for player 0");
            var command = InWindow(e, "honorChangeReaction", owner, ability, deeds.Id);
            Accept(e, command); Settle(e);
            Check(e.State.Cards[deeds.Id].Zone is not (Zone.Home or Zone.Stronghold), "Deeds and Words was destroyed as the cost");
            Eq(Honor(e, 0), 12, "the Honor loss was reduced to 0");
            Check(e.State.Cards[victim.Id].Dishonorable && e.State.Cards[actor.Id].Bowed, "the rest of the action resolved");
            var control = Rig(owner, Phase.Action, true, out var d2, out _, out _); Settle(control);
            Eq(Honor(control, 0), 11, "declined, the Honor is lost");
            Eq(control.State.Cards[d2.Id].Zone, Zone.Home, "and Deeds and Words stays in play");
            var quiet = Rig(owner, Phase.Action, false, out var d3, out _, out _);
            NeverOpened(quiet, owner, ability, d3.Id, command with { CardId = d3.Id });
        }
        // Another phase: the Honor change of a Battle-phase action offers nothing.
        var s4 = Table(Phase.Battle, 0);
        s4.Cards[s4.Players[0].StrongholdId].DefinitionId = "oracle-l5r-6691";
        var performer = Add(s4, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        var target = Add(s4, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var deeds4 = Add(s4, def, 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        var honor4 = Honor(e4, 0);
        Accept(e4, Offer(e4, 0, "shamate-move", s4.Players[0].StrongholdId, target.Id, performer.Id));
        Check(e4.State.PendingChoice?.Type != "honorChangeReaction", "no window: it is not the Action Phase");
        Eq(Honor(e4, 0), honor4 + 2, "the gain was applied at once");
        Eq(e4.State.Cards[deeds4.Id].Zone, Zone.Home, "and Deeds and Words is still there");
    }

    private static void CoinPouchGold()
    {
        const string ability = "pay-coin-pouch-gold";
        var def = Def(ability);
        foreach (var o in new[] { 0, 1 })
        {
            MatchEngine Rig(int keeps, bool pouchBowed, out CardInstance host, out CardInstance weapon, out CardInstance pouch, out CardInstance[] holdings)
            {
                var s = Table(Phase.Action, o); s.PriorityPlayerId = o;
                foreach (var c in s.Cards.Values.Where(c => c.OwnerId == o && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
                holdings = Enumerable.Range(0, keeps).Select(_ => Add(s, "training-border-keep", o, Zone.Home)).ToArray();
                host = Add(s, "training-crane-samurai", o, Zone.Home);
                pouch = Add(s, def, o, Zone.Attached, pouchBowed, null, host.Id);
                weapon = Add(s, "training-costly-weapon", o, Zone.Hand);
                return MatchEngine.FromTestState(s);
            }
            GameCommand Equip(MatchEngine m, CardInstance weapon, CardInstance host, params string[] sources) => new("equip", o, m.Version, CardId: weapon.Id, TargetId: host.Id, SourceIds: sources);
            var e = Rig(1, false, out var host, out var weapon, out var pouch, out var keeps);
            var subject = new PaymentSubject(weapon, PaymentKind.Equip, Wielder: host);
            var pouchId = pouch.Id + "#pouch";
            var sources = e.PaymentSourcesForTest(o, subject);
            Check(sources.Any(x => x.Id == pouchId), "the Pouch is listed among the payment sources");
            Eq(sources.Single(x => x.Id == pouchId).Produced, 1, "worth 1 additional Gold");
            var options = e.PaymentOptions(o, 3, subject);
            Check(options.Length > 0 && options.All(x => x.SourceIds.Contains(pouchId) && x.SourceIds.Contains(keeps[0].Id)), "the only way to pay 3 is the Holding and the Pouch");
            Accept(e, Equip(e, weapon, host, keeps[0].Id, pouchId));
            Eq(e.State.Cards[weapon.Id].AttachedTo, host.Id, "the 3 Gold Weapon was bought with 2 + 1");
            Check(e.State.Cards[pouch.Id].Bowed && e.State.Cards[keeps[0].Id].Bowed, "the Holding and the Pouch are bowed");
            Check(e.State.Events.Any(x => x.Type == "goldSourceUsed" && x.CardId == pouch.Id), "the Pouch's use is logged");
            // Alone it cannot pay, and it stays unbowed after the refusal.
            var a = Rig(1, false, out var ah, out var aw, out var ap, out _);
            Reject(a, Equip(a, aw, ah, ap.Id + "#pouch"));
            Check(!a.State.Cards[ap.Id].Bowed, "the refusal left the Pouch unbowed");
            // A bowed Pouch is no source; with no Holding to bow there is none either.
            var bowed = Rig(1, true, out var bh, out var bw, out var bp, out _);
            Check(!bowed.PaymentSourcesForTest(o, new PaymentSubject(bw, PaymentKind.Equip, Wielder: bh)).Any(x => x.Id == bp.Id + "#pouch"), "a bowed Pouch produces nothing");
            var none = Rig(0, false, out var nh, out var nw, out var np, out _);
            Check(!none.PaymentSourcesForTest(o, new PaymentSubject(nw, PaymentKind.Equip, Wielder: nh)).Any(x => x.Id == np.Id + "#pouch"), "no Holding bowed for Gold, no Pouch");
            // Once a turn: after it paid, a straightened Pouch is not a source again this turn.
            var again = Batch336Support.Edit(e, st => { st.Cards[pouch.Id].Bowed = false; st.Cards[keeps[0].Id].Bowed = false; Add(st, "training-costly-weapon", o, Zone.Hand); });
            var second = again.State.Cards.Values.First(c => c.Zone == Zone.Hand && c.OwnerId == o && c.DefinitionId == "training-costly-weapon");
            Check(!again.PaymentSourcesForTest(o, new PaymentSubject(second, PaymentKind.Equip, Wielder: again.State.Cards[host.Id])).Any(x => x.Id == pouchId), "the Pouch's Reaction is spent for the turn");
            Check(!CanUse(a, o, ability, ap.Id), "it is never offered as a Reaction window option: it is a payment source");
        }
    }

    private static void WitnessLook()
    {
        const string ability = "wl-witness-the-untold-look", ringOfVoid = "oracle-l5r-6338";
        var def = Def(ability);
        MatchEngine Rig(int actor, int ringOwner, bool act, out CardInstance witness, out string[] top)
        {
            var s = Table(Phase.Action, ringOwner); s.PriorityPlayerId = ringOwner;
            var host = Add(s, "training-shugenja", actor, Zone.Home);
            witness = Add(s, def, actor, Zone.Attached, province: null, parent: host.Id);
            var ring = Add(s, ringOfVoid, ringOwner, Zone.Hand);
            foreach (var d in new[] { 0, 1, 2 }) { var c = Add(s, "training-focus-strategy", actor, Zone.FateDeck); s.Players[actor].FateDeck.Insert(0, c.Id); }
            top = s.Players[actor].FateDeck.Take(3).ToArray();
            var e = MatchEngine.FromTestState(s);
            if (act) { e = Batch336Support.GrantVoid(e, ringOwner, ring.Id); Accept(e, Use(e, ringOwner, "ring-of-void-recruit", ring.Id)); }
            return e;
        }
        foreach (var o in new[] { 0, 1 })
        {
            var e = Rig(o, o, true, out var witness, out var top);
            var command = InWindow(e, "afterActionResolvedReaction", o, ability, witness.Id);
            var size = e.State.Players[o].FateDeck.Count; var dynasty = e.State.Players[o].DynastyDeck.Count;
            Accept(e, command);
            Check(e.State.Cards[witness.Id].Bowed, "the Spell bowed as the cost");
            Eq(e.State.PendingChoice?.Type, "lookDeck", "the Dynasty or Fate deck is chosen first");
            Accept(e, new GameCommand("chooseLookDeck", o, e.Version, Choice: "Fate", TargetPlayerId: o));
            var view = e.GetView(o).PendingLook!;
            Eq(view.Cards!.Length, 3, "the top three cards");
            Eq(e.GetView(1 - o).PendingLook, null, "the opponent sees nothing of the look");
            Accept(e, new GameCommand("arrangeTopCards", o, e.Version, CardIds: ["2:top", "0:top", "1:top"]));
            Eq(string.Join(",", e.State.Players[o].FateDeck.Take(3)), string.Join(",", new[] { top[2], top[0], top[1] }), "put back in the chosen order");
            Eq(e.State.Players[o].FateDeck.Count, size, "no card left the deck"); Eq(e.State.Players[o].DynastyDeck.Count, dynasty, "the Dynasty deck is untouched");
            var theirs = Rig(o, 1 - o, true, out var w2, out _);
            Check(!CanUse(theirs, o, ability, w2.Id) && !CanUse(theirs, 1 - o, ability, w2.Id), "the other player's Ring is not 'your Ring'");
            var quiet = Rig(o, o, false, out var w3, out _);
            NeverOpened(quiet, o, ability, w3.Id, command with { CardId = w3.Id });
            var bowedSpell = Batch336Support.Edit(Rig(o, o, false, out var w4, out _), st => st.Cards[w4.Id].Bowed = true);
            bowedSpell = Batch336Support.GrantVoid(bowedSpell, o, bowedSpell.State.Cards.Values.First(c => c.DefinitionId == ringOfVoid).Id); Accept(bowedSpell, Use(bowedSpell, o, "ring-of-void-recruit", bowedSpell.State.Cards.Values.First(c => c.DefinitionId == ringOfVoid).Id));
            Check(!CanUse(bowedSpell, o, ability, w4.Id), "a bowed Spell cannot pay its cost");
        }
    }

    private static void InexorableNegate()
    {
        const string ability = "inexorable-negate";
        var def = Def(ability);
        MatchEngine Rig(int o, Phase phase, bool negatorActs, out CardInstance card, out CardInstance a, out CardInstance b, bool withCard = true)
        {
            var s = Table(phase, o); s.PriorityPlayerId = o;
            card = Add(s, withCard ? def : "training-focus-strategy", o, Zone.Hand);
            var battle = phase == Phase.Battle;
            Zone place = battle ? Zone.Battlefield : Zone.Home; int? prov = battle ? 0 : null;
            var actor = Add(s, battle ? "training-slots-source" : "training-bow-any-source", o, place, province: prov);
            a = Add(s, "training-lion-samurai", 1 - o, place, province: prov);
            b = Add(s, "training-crane-samurai", 1 - o, place, province: prov);
            Add(s, "training-action-negator", 1 - o, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, battle ? Use(e, o, "training-slots-two-personalities", actor.Id, selections: [Pick("first", a.Id), Pick("second", b.Id)]) : Offer(e, o, "training-bow-any", actor.Id, a.Id));
            if (negatorActs) Accept(e, e.QueryActions(1 - o).Single(x => x.Type == "useAbility" && x.Enabled && x.Command!.AbilityId == "training-negate-action").Command!);
            return e;
        }
        // Regression (issue #833): an effect-negating Reaction (Caught in the Act) answered by an Inexorable that is declined still negates what it negates.
        foreach (var o in new[] { 0, 1 })
        {
            var s = Table(Phase.Battle, o); s.PriorityPlayerId = o;
            var victim = Add(s, "training-crane-samurai", 1 - o, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", o, Zone.Battlefield, province: 0);
            var caught = Add(s, Def("caught-act-negate-bow"), 1 - o, Zone.Hand);
            Add(s, def, o, Zone.Hand); Gold(s, 1 - o, 2);
            var e = Open(s, o, Bow, bower.Id, victim.Id);
            Accept(e, Enabled(e, 1 - o, "caught-act-negate-bow", caught.Id).Command!);
            Check(CanUse(e, o, ability, e.State.Cards.Values.Single(c => c.DefinitionId == def).Id), "Inexorable is offered against the announced Caught in the Act");
            Settle(e);
            Check(!e.State.Cards[victim.Id].Bowed, $"Inexorable declined: Caught in the Act still negated the bowing (o={o})");
        }
        foreach (var o in new[] { 0, 1 })
        {
            var e = Rig(o, Phase.Battle, true, out var card, out var a, out var b);
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "Inexorable's own window opens for the announced Reaction");
            var command = InWindow(e, "beforeActionReaction", o, ability, card.Id);
            Accept(e, command); Settle(e);
            Check(e.State.Cards[a.Id].Bowed && e.State.Cards[b.Id].Bowed, "the whole Reaction (training-negate-action) was itself negated: the original bow stands");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "Inexorable is spent");
            var baseline = Rig(o, Phase.Battle, true, out _, out var a1, out var b1, withCard: false); Settle(baseline);
            Check(!baseline.State.Cards[a1.Id].Bowed && !baseline.State.Cards[b1.Id].Bowed, "baseline without Inexorable: the Reaction negates the action");
            var control = Rig(o, Phase.Battle, true, out _, out var a2, out var b2); Settle(control);
            Check(!control.State.Cards[a2.Id].Bowed && !control.State.Cards[b2.Id].Bowed, "declined, the Reaction negates the action as printed (o=" + o + ", a=" + control.State.Cards[a2.Id].Bowed + ", b=" + control.State.Cards[b2.Id].Bowed + ", " + control.State.PendingChoice?.Type + ") " + string.Join(" / ", control.State.Events.TakeLast(10).Select(x => x.Type + ":" + x.Message)));
            // The first window (the action itself, not a Reaction): Inexorable is not offered.
            var first = Rig(o, Phase.Battle, false, out var c3, out _, out _);
            Check(!CanUse(first, o, ability, c3.Id) && !CanUse(first, 1 - o, ability, c3.Id), "an action that is not a Reaction gives it nothing to negate");
            // Outside the Combat Segment (the Action phase) the same Reaction is not answered.
            var action = Rig(o, Phase.Action, true, out var c4, out _, out _);
            Check(!CanUse(action, o, ability, c4.Id), "not during the Action phase");
            var qs = Table(Phase.Battle, o); qs.PriorityPlayerId = o;
            var c5 = Add(qs, def, o, Zone.Hand);
            Add(qs, "training-lion-samurai", 1 - o, Zone.Battlefield, province: 0);
            var quiet = MatchEngine.FromTestState(qs);
            NeverOpened(quiet, o, ability, c5.Id, command with { CardId = c5.Id });
        }
    }

    private static void BeforeActionSuppress()
    {
        const string ability = "training-before-action-suppress", def = "training-before-action-suppressor";
        MatchEngine Rig(bool act, out CardInstance suppressor, out CardInstance negator, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            suppressor = Add(s, def, 0, Zone.Battlefield, province: 0);
            negator = Add(s, "training-action-negator", 0, Zone.Battlefield, province: 0);
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, victim.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var suppressor, out var negator, out var victim);
        var command = InWindow(e, "beforeActionReaction", 0, ability, suppressor.Id);
        Check(CanUse(e, 0, "training-negate-action", negator.Id), "before it, the other reaction is usable");
        Accept(e, command);
        Check(e.State.Cards[suppressor.Id].Bowed && e.State.Cards[negator.Id].Bowed, "it bowed itself and the other before-action reactor");
        Check(!CanUse(e, 0, "training-negate-action", negator.Id), "the window re-evaluates: the bowed card's reaction is no longer usable");
        Settle(e);
        Check(e.State.Cards[victim.Id].Bowed, "the action was not negated, so its target is bowed");
        var control = Rig(true, out _, out var n2, out var v2);
        Accept(control, Enabled(control, 0, "training-negate-action", n2.Id).Command!); Settle(control);
        Check(!control.State.Cards[v2.Id].Bowed, "control: the negator alone does negate the action");
        var quiet = Rig(false, out var s3, out _, out _);
        NeverOpened(quiet, 0, ability, s3.Id, command with { CardId = s3.Id });
    }

    private static void DisciplineCounter()
    {
        const string ability = "training-discipline-counter-negate", def = "training-discipline-counter";
        MatchEngine Rig(int holder, Zone zone, bool grant, out CardInstance counter, out CardInstance target)
        {
            var reactor = 1 - holder;
            var s = Table(Phase.Battle, reactor); s.PriorityPlayerId = holder;
            Gold(s, reactor, 3);
            var kensai = Add(s, "training-kensai", holder, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", holder, Zone.Battlefield, province: 0);
            target = Add(s, "training-crane-samurai", reactor, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", reactor, Zone.Battlefield, province: 0);
            counter = Add(s, def, reactor, zone);
            if (grant) s.AdditionalActions.Add(new(holder, null, false));
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, holder, Bow, bower.Id, target.Id));
            return e;
        }
        foreach (var holder in new[] { 0, 1 })
            foreach (var zone in new[] { Zone.Hand, Zone.FateDiscard, Zone.DynastyDiscard })
            {
                var e = Rig(holder, zone, true, out var counter, out var target);
                Eq(e.State.PendingChoice?.Type, "additionalActionReaction", $"a Discipline Reaction in {zone} can be played, so the window opens");
                var command = InWindow(e, "additionalActionReaction", 1 - holder, ability, counter.Id);
                if (zone != Zone.Hand)
                {
                    Eq(Options(e, 1 - holder, ability, counter.Id).First(o => o.Enabled).Gold?.Cost, 2, $"from the {zone} pile it is the paid Discipline play, quoted at the card's 2 Gold");
                    Check(command.SourceIds is { Length: > 0 }, "with a payment the engine suggests");
                }
                else Check(Options(e, 1 - holder, ability, counter.Id).First(o => o.Enabled).Gold is null, "from hand it is the free play");
                Accept(e, command); Settle(e);
                Check(!e.State.Cards[target.Id].Bowed, $"the additional action was negated (from {zone})");
                var control = Rig(holder, zone, true, out _, out var t2); Settle(control);
                Check(control.State.Cards[t2.Id].Bowed, "declined, the additional action resolves");
            }
        // No additional action outstanding: the first action is not an additional one, so no window.
        var first = Rig(0, Zone.Hand, false, out var c, out var tf);
        Check(first.State.PendingChoice?.Type != "additionalActionReaction" && !CanUse(first, 1, ability, c.Id), "the creating action is not an additional action: no window");
        Check(first.State.Cards[tf.Id].Bowed, "it simply resolved");
    }
    //FALLING
    // ---- before-action windows: an action targets one of this player's cards -----------------------------------------------------------------------------------------------
    private const string Bow = "training-bow-at-battle", MoveHome = "training-move-both-home";

    private static MatchEngine Open(MatchState s, int player, string ability, string cardId, string targetId, string? performerId = null)
    {
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, player, ability, cardId, targetId, performerId));
        return e;
    }

    private static void CaughtInTheAct()
    {
        const string ability = "caught-act-negate-bow";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool bows, out CardInstance caught, out CardInstance victim)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            Gold(s, 0, 2);
            caught = Add(s, def, 0, Zone.Hand);
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var src = Add(s, bows ? "training-battle-bower" : "training-move-source", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, bows ? Bow : MoveHome, src.Id, victim.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var caught, out var victim);
        var command = InWindow(e, "beforeActionReaction", 0, ability, caught.Id);
        Accept(e, command); Settle(e);
        Check(!e.State.Cards[victim.Id].Bowed, "his bowing from the action was negated");
        Eq(e.State.Cards[caught.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var control = Rig(true, true, out _, out var v2); Settle(control);
        Check(control.State.Cards[v2.Id].Bowed, "declined, the bow lands");
        var moves = Rig(true, false, out var c3, out var v3);
        Check(moves.State.PendingChoice is null && !CanUse(moves, 0, ability, c3.Id), "an action that does not bow opens nothing");
        Eq(moves.State.Cards[v3.Id].Zone, Zone.Home, "and the move happened");
        var quiet = Rig(false, true, out var c4, out _);
        NeverOpened(quiet, 0, ability, c4.Id, command with { CardId = c4.Id });
        // The Action phase: an Open action of the other player bowing a Personality at home.
        var s2 = Table(Phase.Action, 1); s2.PriorityPlayerId = 1;
        var caught2 = Add(s2, def, 0, Zone.Hand);
        var home = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var bower = Add(s2, "training-bow-any-source", 1, Zone.Home);
        var a = Open(s2, 1, "training-bow-any", bower.Id, home.Id);
        Accept(a, InWindow(a, "beforeActionReaction", 0, ability, caught2.Id)); Settle(a);
        Check(!a.State.Cards[home.Id].Bowed, "the Action-phase bow was negated too");
        // Your own action targeting your own Personality is not "another player's action".
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var caught3 = Add(s3, def, 0, Zone.Hand);
        var mine = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var own = Add(s3, "training-bow-any-source", 0, Zone.Home);
        var o = Open(s3, 0, "training-bow-any", own.Id, mine.Id);
        Check(!CanUse(o, 0, ability, caught3.Id), "not offered against your own action");
        Settle(o);
        Check(o.State.Cards[mine.Id].Bowed, "and the bow landed");
    }

    private static void UruwashiiNegateMove()
    {
        const string ability = "p2-uruwashii-negate-move";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool targetWielder, out CardInstance armor, out CardInstance wielder, out CardInstance mover)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            wielder = Add(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
            armor = Add(s, def, 0, Zone.Attached, province: 0, parent: wielder.Id);
            var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            mover = Add(s, "training-move-source", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, MoveHome, mover.Id, targetWielder ? wielder.Id : other.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var armor, out var wielder, out var mover);
        var command = InWindow(e, "beforeActionReaction", 0, ability, armor.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[wielder.Id].Zone, Zone.Battlefield, "the wielder's movement from the action was negated");
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the rest of the action (its performer's own move) still happened");
        Eq(e.State.Cards[armor.Id].Zone, Zone.Attached, "the Armor stays attached");
        var control = Rig(true, true, out _, out var w2, out _); Settle(control);
        Eq(control.State.Cards[w2.Id].Zone, Zone.Home, "declined, the wielder moves home");
        var elsewhere = Rig(true, false, out var a3, out _, out _);
        Check(elsewhere.State.PendingChoice?.Type != "beforeActionReaction" && !CanUse(elsewhere, 0, ability, a3.Id), "an action that moves another Personality opens nothing for the Armor");
        var quiet = Rig(false, true, out var a4, out _, out _);
        NeverOpened(quiet, 0, ability, a4.Id, command with { CardId = a4.Id });
        var printed = CardCatalog.Get(def);
        Check(printed.AttackDefenseKind == "Melee" && printed.AttackDefenseAmount == -2 && printed.AttackDefenseScope == "Unit", "the Melee -2 passive is the card's own");
    }

    private static void HungerNegateMove()
    {
        const string ability = "hunger-negate-move";
        var def = Def(ability);
        MatchEngine Rig(bool act, out CardInstance hunger, out CardInstance wielder, out CardInstance mover)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            wielder = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            hunger = Add(s, def, 0, Zone.Attached, province: 0, parent: wielder.Id);
            mover = Add(s, "training-move-source", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, MoveHome, mover.Id, wielder.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var hunger, out var wielder, out var mover);
        var command = InWindow(e, "beforeActionReaction", 0, ability, hunger.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[wielder.Id].Zone, Zone.Battlefield, "his movement from the action was negated");
        Eq(e.State.Cards[mover.Id].Zone, Zone.Home, "the action's other effect still happened");
        var control = Rig(true, out _, out var w2, out _); Settle(control);
        Eq(control.State.Cards[w2.Id].Zone, Zone.Home, "declined, the wielder moves home");
        var quiet = Rig(false, out var h3, out _, out _);
        NeverOpened(quiet, 0, ability, h3.Id, command with { CardId = h3.Id });
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var bearer = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var carried = Add(s, def, 0, Zone.Attached, province: 0, parent: bearer.Id);
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var src = Add(s, "training-move-source", 1, Zone.Battlefield, province: 0);
        var o = Open(s, 1, MoveHome, src.Id, other.Id);
        Check(!CanUse(o, 0, ability, carried.Id), "not offered when the action targets another Personality");
    }

    private static void SawakiSpawn()
    {
        const string ability = "sawaki-spawn";
        var def = Def(ability);
        MatchEngine Rig(bool act, out CardInstance sawaki)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            sawaki = Add(s, def, 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, sawaki.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var sawaki);
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "beforeActionReaction", 0, ability, sawaki.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.Cards[sawaki.Id].Zone, Zone.Dead, "Sawaki was destroyed as the cost");
        var spawn = e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();
        Eq(spawn.Length, 2, "two Personalities created");
        foreach (var c in spawn)
        {
            var d = CardCatalog.Get(c.DefinitionId);
            Check(c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.ProvinceIndex == 0, "at the current battlefield, controlled by Sawaki's player");
            Check(d.Type == CardType.Personality && d.Force == 1 && d.Chi == 1 && d.PersonalHonor == 0, $"1F/1C/0PH, got {d.Force}/{d.Chi}/{d.PersonalHonor}");
            foreach (var kw in new[] { "Spider Clan", "Shadowlands", "Ninja" }) Check((d.Keywords ?? []).Contains(kw) || d.Clan == kw.Replace(" Clan", ""), "has " + kw + " (" + string.Join(",", d.Keywords ?? []) + ")");
        }
        var control = Rig(true, out var s2); Settle(control);
        Check(control.State.Cards[s2.Id].Bowed && control.State.Cards[s2.Id].Zone == Zone.Battlefield, "declined, he is bowed and stays");
        var quiet = Rig(false, out var s3);
        NeverOpened(quiet, 0, ability, s3.Id, command with { CardId = s3.Id });
        var a = Table(Phase.Action, 1); a.PriorityPlayerId = 1;
        var home = Add(a, def, 0, Zone.Home);
        var bower2 = Add(a, "training-bow-any-source", 1, Zone.Home);
        var o = Open(a, 1, "training-bow-any", bower2.Id, home.Id);
        Check(!CanUse(o, 0, ability, home.Id), "an Open action is not a Battle action");
        Check(o.State.Cards[home.Id].Bowed, "and the bow landed");
    }

    private static void HanteiNegate()
    {
        const string ability = "w740-hantei-negate";
        var def = Def(ability);
        var kuroko = "oracle-l5r-10489";
        MatchEngine Rig(bool act, bool political, out CardInstance celestial, out CardInstance actor)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1;
            actor = Add(s, political ? kuroko : "training-bow-any-source", 1, Zone.Home);
            var victim = Add(s, "training-crane-samurai", 0, Zone.Home);
            celestial = Add(s, def, 0, Zone.Celestial);
            var e = MatchEngine.FromTestState(s);
            if (act) Accept(e, political ? Use(e, 1, "w737-kuroko-favor", actor.Id) : Offer(e, 1, "training-bow-any", actor.Id, victim.Id));
            return e;
        }
        var e = Rig(true, true, out var celestial, out _);
        var command = InWindow(e, "beforeActionReaction", 0, ability, celestial.Id);
        Accept(e, command); Settle(e);
        Eq(e.State.FavorPlayerId, null, "the Political action was negated: no Imperial Favor was taken");
        Eq(e.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded from play as the cost");
        var control = Rig(true, true, out _, out _); Settle(control);
        Eq(control.State.FavorPlayerId, 1, "declined, the Political action took the Imperial Favor");
        var plain = Rig(true, false, out var c3, out _);
        Check(!CanUse(plain, 0, ability, c3.Id), "a non-Political action is not offered the Interrupt");
        var quiet = Rig(false, true, out var c4, out _);
        NeverOpened(quiet, 0, ability, c4.Id, command with { CardId = c4.Id });
    }

    private static void SnakeTattooMove()
    {
        const string ability = "snake-tattoo-move";
        var def = Def(ability);
        MatchEngine Rig(bool act, bool adjacent, bool tattooed, out CardInstance tattoo, out CardInstance target)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            if (adjacent) s.UnresolvedBattlefields = [0, 1];
            tattoo = Add(s, def, 0, Zone.Hand);
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (tattooed) Kw(target, "Tattooed");
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, target.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, true, out var tattoo, out var target);
        var command = InWindow(e, "beforeActionReaction", 0, ability, tattoo.Id);
        Accept(e, command); Settle(e);
        var moved = e.State.Cards[target.Id];
        Check(moved.Zone == Zone.Battlefield && moved.ProvinceIndex == 1, "he moved to the adjacent battlefield");
        Check(!moved.Bowed, "and, no longer a legal target, the action's bowing was negated");
        Eq(e.State.Cards[tattoo.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var control = Rig(true, true, true, out _, out var t2); Settle(control);
        Check(control.State.Cards[t2.Id].Bowed && control.State.Cards[t2.Id].ProvinceIndex == 0, "declined, he is bowed where he stood");
        var alone = Rig(true, false, true, out var a3, out _);
        Check(alone.State.PendingChoice is null && !CanUse(alone, 0, ability, a3.Id), "no adjacent unresolved battlefield: nothing offered");
        var plain = Rig(true, true, false, out var p4, out _);
        Check(plain.State.PendingChoice is null && !CanUse(plain, 0, ability, p4.Id), "only a Tattooed Personality");
        var quiet = Rig(false, true, true, out var q5, out _);
        NeverOpened(quiet, 0, ability, q5.Id, command with { CardId = q5.Id });
    }

    private static void ShiftingTerrainRedirect()
    {
        const string ability = "shifting-terrain-redirect";
        var def = Def(ability);
        MatchEngine Rig(bool act, Zone terrainZone, out CardInstance terrain, out CardInstance a, out CardInstance b)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            terrain = Add(s, def, 0, terrainZone, province: terrainZone == Zone.Battlefield ? 0 : null);
            a = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            b = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-veteran", 0, Zone.Home);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, a.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, Zone.Battlefield, out var terrain, out var a, out var b);
        var offers = Options(e, 0, ability, terrain.Id).Where(o => o.Enabled).Select(o => o.Command!.TargetId).ToArray();
        Check(offers.SequenceEqual([b.Id]), "one option: the other Personality at the battlefield: " + string.Join(",", offers));
        var command = InWindow(e, "beforeActionReaction", 0, ability, terrain.Id, null, b.Id);
        Accept(e, command); Settle(e);
        Check(e.State.Cards[b.Id].Bowed && !e.State.Cards[a.Id].Bowed, "the action landed on the chosen Personality instead");
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain stays in play");
        var control = Rig(true, Zone.Battlefield, out _, out var a2, out var b2); Settle(control);
        Check(control.State.Cards[a2.Id].Bowed && !control.State.Cards[b2.Id].Bowed, "declined, the original target is bowed");
        var inHand = Rig(true, Zone.Hand, out var t3, out _, out _);
        Check(inHand.State.PendingChoice is null && !CanUse(inHand, 0, ability, t3.Id), "the Reaction is only for a Terrain in play");
        var quiet = Rig(false, Zone.Battlefield, out var t4, out _, out _);
        NeverOpened(quiet, 0, ability, t4.Id, command with { CardId = t4.Id });
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var terr = Add(s2, def, 0, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var foe = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bower2 = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var o = Open(s2, 0, Bow, bower2.Id, foe.Id);
        Check(!CanUse(o, 0, ability, terr.Id), "no redirect for your own action");
    }

    private static void MerchantGuardFree()
    {
        const string ability = "training-negate-merchant-free", def = "training-merchant-free-guard";
        MatchEngine Rig(bool act, bool merchantTarget, out CardInstance guard, out CardInstance target)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            guard = Add(s, def, 0, Zone.Battlefield, province: 0);
            target = Add(s, merchantTarget ? "training-merchant" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return act ? Open(s, 1, Bow, bower.Id, target.Id) : MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, out var guard, out var merchant);
        var command = InWindow(e, "beforeActionReaction", 0, ability, guard.Id);
        Check(Options(e, 0, ability, guard.Id).First(o => o.Enabled).Gold?.Cost is null or 0, "0 Gold");
        Accept(e, command); Settle(e);
        Check(!e.State.Cards[merchant.Id].Bowed, "the whole action was negated");
        var control = Rig(true, true, out _, out var m2); Settle(control);
        Check(control.State.Cards[m2.Id].Bowed, "declined, the Merchant is bowed");
        var plain = Rig(true, false, out var g3, out _);
        Check(plain.State.PendingChoice is null && !CanUse(plain, 0, ability, g3.Id), "a non-Merchant is not protected");
        var quiet = Rig(false, true, out var g4, out _);
        NeverOpened(quiet, 0, ability, g4.Id, command with { CardId = g4.Id });
    }
}
