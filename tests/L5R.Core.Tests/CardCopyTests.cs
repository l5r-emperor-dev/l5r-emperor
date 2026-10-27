using static RegressionAssertions;
using L5R.Core;

// Copying keywords and abilities (issue #338): Fukuzo (oracle-l5r-2728) and the two independent primitives beneath him -- copy one keyword,
// copy one ability -- plus the general GrantedAbility model a copied ability lives in. Every reading is in
// docs/decisions/2026-09-19-copy-and-either-or-primitives.md. Each test builds an explicit position and drives it only through QueryActions and the
// commands the engine offers; the table is the Action Phase with player 0 active, holding Fukuzo (or the training receiver) at home, and the
// donors in a discard pile.
internal static class CardCopyTests
{
    private const string Fukuzo = "oracle-l5r-2728", Donor = "training-skin-donor", Receiver = "training-skin-receiver", Wear = "fukuzo-wear-skin";
    private const string BowAny = "training-bow-any", CreateToken = "training-create-token", EntryReaction = "tsudoken-exp2-entry";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Fukuzo is the printed card (Force 5, Chi 2, no Honor Requirement, Cost 7, Personal Honor 0, Spider Clan Unique Magistrate ...), whole -- one Open ability that copies abilities, its target category registered -- with the scan's art shipped; his keyword line's 'Experienced Chuda Fukuzo' is not an Experienced level", PublishedShape),
            ("Fukuzo's action, end to end: the removal is the cost, then TWO decisions in printed order -- a keyword (not one he has), then an ability (never an entering-play Reaction) -- private to the chooser, offered as one action per candidate, both copies live and recorded with their source, and priority then passes as after any action", FukuzoWizard),
            ("A copied ability is live: Fukuzo may take it as an action like a printed one (offered with its source in the label, validated by the ordinary pipeline), it is limited once a turn on Fukuzo under its own usage key, and the donor's own stamps play no part", CopiedAbilityIsUsable),
            ("Using the ability again replaces the old copy -- whether or not the new donor offers anything -- and the once-per-turn stamp of a copied ability stays on Fukuzo, so re-copying the same ability does not reset it", NextUseReplacesTheCopy),
            ("The two copy steps are independent primitives: a keyword-only ability never opens the ability decision, an ability-only one never opens the keyword decision, each command is refused in the other's step, and a copy made for the turn (Duration Turn) ends at turn end while a Copy one persists", StepsAreIndependent),
            ("A step with nothing to offer is skipped and one with a single candidate is taken automatically (the printed 'copies one' is not optional): no decision opens, both are announced in the log, and the action finishes immediately", NothingOrOneCandidate),
            ("An ability that itself copies abilities is never offered (a second Fukuzo as donor: his own ability is excluded, his permanent grants are not), and neither is one that triggers on its own card entering play", CopyingAbilitiesAreNotCopyable),
            ("The target is 'a Personality in any player's discard pile' -- merely discarded or dead, either player's -- and nothing else: not in play, not in a hand or province, not already removed from the game, not a Strategy", TargetCategory),
            ("Permanent changes survive death and are copyable: a Personality with a permanent keyword, a permanent granted ability and a removed printed keyword dies by Seppuku, keeps all of it in the discard pile, and Fukuzo is offered exactly those (its turn-duration and attachment grants are not)", PermanentChangesSurviveDeath),
            ("The offered keywords and abilities are a snapshot from the moment the action resolves: the source is already out of the game by then, and nothing that happens while the player decides changes the list", SnapshotAtRemoval),
            ("Fukuzo's own copies persist through his death (a Personality keeps what it has), are ended by his next use, and are cleared when he returns to a deck as a fresh card", CopiesPersistAndReset),
            ("Granted abilities have the same lifecycle as granted keywords: turn-duration ones clear at turn end, permanent ones do not, and an Experienced overlay carries them over", GrantedAbilityLifecycle),
            ("A copied Reaction is live in its window like a printed one: Fukuzo copies Tamori Tamoko's before-action reaction, and when another player's straighten targets a bowed Personality it is offered to his controller, usable, and negates the straightening", CopiedReactionIsLive),
            ("The copy showcase (the UI issue's scripted position) is an ordinary replayable match: Fukuzo offered five Personalities in two discard zones, both decisions answerable through the offered actions, and the replay reproduces the state", CopyShowcase),
            ("Every offered command of every open copy decision validates on a fresh engine, an illegal answer is refused with the state unchanged, and only the chooser can answer", OffersValidateAndRejections),
        ];
        runner.Run("card-copy", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static void Same(IEnumerable<string> actual, IEnumerable<string> expected, string message = "Sequences differ") =>
        Check(actual.SequenceEqual(expected), $"{message}: expected [{string.Join(", ", expected)}]; got [{string.Join(", ", actual)}]");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"cc-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
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
        return s;
    }

    private sealed record Rig(MatchState S, CardInstance Wearer, CardInstance Donor);

    // Fukuzo (player 0) at home; a Training skin donor (four keywords, two Open actions, an entering-play Reaction) in player 1's discard pile.
    private static Rig Skin(string wearer = Fukuzo, string donor = Donor, Zone donorZone = Zone.DynastyDiscard)
    {
        var s = Table();
        return new(s, Add(s, wearer, 0, Zone.Home), Add(s, donor, 1, donorZone));
    }

    private static ActionOption OfferOption(MatchEngine e, int player, string ability, string cardId, string targetId)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability && (a.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} on {cardId} against {targetId} was not offered to player {player}: "
            + string.Join("; ", e.QueryActions(player).Where(a => a.Type == "useAbility" && a.CardId == cardId).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return option!;
    }

    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => OfferOption(e, player, ability, cardId, targetId).Command! with { TargetId = targetId };

    // The option a card lists for an ability, enabled or not (a disabled one carries no command, so it is found by its label).
    private static ActionOption Listed(MatchEngine e, int player, string cardId, string ability) =>
        e.QueryActions(player).Single(a => a.Type == "useAbility" && a.CardId == cardId && a.Label.StartsWith(AbilityCatalog.All[ability].Label, StringComparison.Ordinal));

    private static ActionOption[] Answers(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type is "chooseCopyKeyword" or "chooseCopyAbility").ToArray();

    // Answers the open copy decision with `value`, through the offered option.
    private static void Choose(MatchEngine e, int player, string value)
    {
        var option = Answers(e, player).SingleOrDefault(a => a.Command?.Choice == value);
        Check(option is { Enabled: true, Command: not null }, $"choosing {value} was not offered");
        Accept(e, option!.Command!);
    }

    private static string[] Values(MatchEngine e, int player = 0) => e.GetView(player).PendingCopy!.Choices.Select(c => c.Value).ToArray();

    private static CardInstance Now(MatchEngine e, string id) => e.State.Cards[id];

    // ---- published shape -----------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.PersonalityWave33.Cards.Length, 2, "Fukuzo and Yoritomo Sakuma");
        var f = CardCatalog.Get(Fukuzo);
        Check(!f.ScenarioOnly && f.Type == CardType.Personality && f.Name == "Fukuzo" && f.Clan == "Spider");
        // Verified against the scan (Force 5 / Chi 2 / no Honor Requirement / Cost 7 / Personal Honor 0).
        Eq(f.Force, 5); Eq(f.Chi, 2); Eq(f.GoldCost, 7); Eq(f.PersonalHonor, 0); Check(f.HonorRequirement is null, "no Honor Requirement");
        foreach (var k in new[] { "Spider Clan", "Shadowlands", "Nonhuman", "Bog Hag", "Samurai", "Magistrate", "Experienced Chuda Fukuzo", "Unique", "Advisor to the Emerald Champion", "Infiltrator" }) Check(f.Keywords!.Contains(k), k);
        Eq(f.Text, "Open: Remove a target Personality in any player's discard pile from the game: Fukuzo wears his skin. Until the next time this game this ability is used, Fukuzo copies one keyword, and one ability that does not itself copy abilities, from the Personality.");
        Same(f.Abilities!, [Wear]); Eq(f.Effect, "none");
        var ability = AbilityCatalog.All[Wear];
        Eq(ability.Timing, "Open"); Eq(ability.Target, "personality-in-any-discard"); Check(ability.CopiesAbilities, "it copies abilities, so it is never itself copyable");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", Fukuzo + ".jpg")), "Fukuzo's art is shipped");
        Eq(f.ArtworkKey, $"res://Content/Published/{Fukuzo}.jpg");
        // 'Experienced Chuda Fukuzo' is a trait naming the version he stands in for: it is not an Experienced level.
        var e = MatchEngine.FromTestState(Table());
        Eq(Experienced.Level(f.Keywords), 0, "no Experienced level");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }

    // ---- the whole action ------------------------------------------------------------------------------------------
    private static void FukuzoWizard()
    {
        var r = Skin();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, r.Donor.Id));
        // The removal is the cost: the donor is out of the game before anything is chosen.
        Eq(Now(e, r.Donor.Id).Zone, Zone.RemovedFromGame, "the donor was removed from the game");
        Eq(e.State.PendingChoice?.Type, "copyKeyword"); Eq(e.State.PendingChoice?.PlayerId, 0); Eq(e.State.PriorityPlayerId, 0);
        // The keyword decision: the donor's four keywords minus the two Fukuzo already has (Samurai, Unique).
        var keyword = e.GetView(0).PendingCopy!;
        Eq(keyword.Step, "keyword"); Same(keyword.RemainingSteps, ["ability"]); Eq(keyword.ReceiverId, r.Wearer.Id); Eq(keyword.ReceiverName, "Fukuzo");
        Eq(keyword.SourceName, "Training skin donor"); Eq(keyword.SourceDefinitionId, Donor); Eq(keyword.Duration, "Copy");
        Same(keyword.Choices.Select(c => c.Value), ["Berserker", "Tactician"]);
        Check(keyword.Choices.All(c => c.Label == c.Value && c.Detail is null), "a keyword is its own label");
        // Private to the chooser; the other player sees only that a decision is pending, and is told to wait.
        Check(e.GetView(1).PendingCopy is null, "the opponent has no copy view");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the opponent holds no decision");
        var offers = Answers(e, 0);
        Check(offers.Select(a => a.Command!.Choice).ToHashSet().SetEquals(["Berserker", "Tactician"]) && offers.All(a => a.Type == "chooseCopyKeyword" && a.Enabled), "one enabled action per candidate keyword");
        Check(Answers(e, 1).Length == 0, "the opponent is offered no answer");
        Choose(e, 0, "Berserker");
        // The keyword is copied at once; the ability decision follows, again a real choice: the donor's two actions, never its entering-play Reaction.
        Check(e.State.Cards[r.Wearer.Id].GrantedKeywords.Any(g => g.Keyword == "Berserker"), "the keyword was copied");
        Eq(e.State.PendingChoice?.Type, "copyAbility");
        var ability = e.GetView(0).PendingCopy!;
        Eq(ability.Step, "ability"); Eq(ability.RemainingSteps.Length, 0); Same(ability.Choices.Select(c => c.Value), [BowAny, CreateToken]);
        Check(!ability.Choices.Any(c => c.Value == EntryReaction), "an entering-play Reaction is not offered");
        Check(ability.Choices.All(c => c.Label == AbilityCatalog.All[c.Value].Label && c.Detail == "Open"), "each ability is labeled and timed by the engine");
        Choose(e, 0, CreateToken);
        // Finished: the action resolved and priority passed to the other player as after any action.
        Check(e.State.PendingChoice is null && e.State.PendingCopy is null, "the decision is over");
        Eq(e.State.PriorityPlayerId, 1, "priority passed to the opponent");
        var wearer = Now(e, r.Wearer.Id);
        var kw = wearer.GrantedKeywords.Single(g => g.Keyword == "Berserker");
        Eq(kw.Duration, "Copy"); Eq(kw.Source, Wear); Eq(kw.CopiedFrom, Donor); Eq(kw.CopiedFromName, "Training skin donor");
        var ab = wearer.GrantedAbilities.Single();
        Eq(ab.AbilityId, CreateToken); Eq(ab.Duration, "Copy"); Eq(ab.Source, Wear); Eq(ab.CopiedFrom, Donor);
        Check(e.HasKeyword(r.Wearer.Id, "Berserker") && !e.HasKeyword(r.Wearer.Id, "Tactician"), "exactly the chosen keyword");
        // Both copies are public: in the log, and on the card as everyone sees it.
        Check(e.State.Events.Any(x => x.Type == "keywordCopied" && x.Message.Contains("Berserker")) && e.State.Events.Any(x => x.Type == "abilityCopied"), "the copies are announced");
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = e.GetView(viewer).Players[0].Home.Single(c => c.Id == r.Wearer.Id);
            Check(view.GrantedKeywords!.Any(g => g.Keyword == "Berserker" && g.CopiedFromName == "Training skin donor"), $"player {viewer} sees the copied keyword and its source");
            var granted = view.GrantedAbilities!.Single();
            Eq(granted.AbilityId, CreateToken); Eq(granted.Label, AbilityCatalog.All[CreateToken].Label); Eq(granted.Timing, "Open"); Eq(granted.CopiedFromName, "Training skin donor");
        }
        // The donor's own ability is untouched (it left the game with the card, and the wearer took a copy, not the original).
        Check(!Now(e, r.Donor.Id).GrantedAbilities.Any(), "nothing was added to the donor");
    }

    private static void CopiedAbilityIsUsable()
    {
        var r = Skin();
        var enemy = Add(r.S, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, r.Donor.Id));
        Choose(e, 0, "Berserker"); Choose(e, 0, BowAny);
        Accept(e, new GameCommand("pass", 1, e.Version));
        // Offered to Fukuzo like a printed ability, labeled with where it came from.
        var option = Listed(e, 0, r.Wearer.Id, BowAny);
        Check(option.Enabled && option.Label.EndsWith(" · copied from Training skin donor"), option.Label);
        Check(option.TargetIds!.Contains(enemy.Id), "it targets like the printed ability does");
        Accept(e, option.Command! with { TargetId = enemy.Id });
        Check(Now(e, enemy.Id).Bowed, "the copied ability resolved: the target bowed");
        Eq(Now(e, r.Wearer.Id).AbilityUses[BowAny], e.State.TurnNumber, "the use is stamped on Fukuzo under the ability's own key");
        Check(!Now(e, r.Donor.Id).AbilityUses.ContainsKey(BowAny), "and not on the donor");
        // Limited to once a turn like any ability: the option is still listed, disabled with the reason.
        Accept(e, new GameCommand("pass", 1, e.Version));
        var again = Listed(e, 0, r.Wearer.Id, BowAny);
        Check(!again.Enabled && again.Reason!.Contains("already been used"), again.Reason ?? "");
        // The other player never gets it: another player's card does not provide the ability.
        Reject(e, new GameCommand("useAbility", 1, e.Version, CardId: r.Wearer.Id, TargetId: enemy.Id, AbilityId: BowAny));
    }

    private static void NextUseReplacesTheCopy()
    {
        var r = Skin();
        // A second donor: its printed Berserker/Tactician removed and Kensai granted for good (so one keyword to offer), the same two actions; and a
        // third with nothing to copy at all.
        var second = Add(r.S, Donor, 1, Zone.DynastyDiscard);
        second.RemovedKeywords = ["Berserker", "Tactician"]; second.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
        var third = Add(r.S, "training-lion-samurai", 1, Zone.DynastyDiscard);
        var enemy = Add(r.S, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, r.Donor.Id));
        Choose(e, 0, "Berserker"); Choose(e, 0, BowAny);
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, Offer(e, 0, BowAny, r.Wearer.Id, enemy.Id)); // the copied ability, used this turn
        Eq(Now(e, r.Wearer.Id).AbilityUses[BowAny], e.State.TurnNumber);
        // A new turn: Fukuzo's ability is used again. The old copies end at once (before anything is chosen), so the ability the last copy provided is
        // offered again, and the keyword step -- Kensai is the only keyword left to offer -- resolves by itself.
        e = Edit(e, st => { st.TurnNumber += 2; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.Phase = Phase.Action; });
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, second.Id));
        var wearer = Now(e, r.Wearer.Id);
        Check(!wearer.GrantedKeywords.Any(g => g.Keyword == "Berserker") && wearer.GrantedKeywords.Any(g => g.Keyword == "Kensai" && g.Duration == "Copy"), "Berserker was replaced by Kensai");
        Check(!wearer.GrantedAbilities.Any(), "the old ability copy has ended while the new one is being chosen");
        Check(e.State.Events.Any(x => x.Type == "copyEnded"), "the end of the old copy is announced");
        Eq(e.State.PendingChoice?.Type, "copyAbility"); Same(Values(e), [BowAny, CreateToken]);
        Choose(e, 0, CreateToken);
        Same(Now(e, r.Wearer.Id).GrantedAbilities.Select(g => g.AbilityId), [CreateToken]);
        // Copying the SAME ability again does not reset its usage: the stamp lives on Fukuzo under the ability's own key. Within one turn Fukuzo's own
        // ability is spent, so the position is built by clearing that stamp (the once-per-turn limit is not what is being pinned here).
        string again = "";
        e = Edit(e, st =>
        {
            st.Cards[r.Wearer.Id].AbilityUses.Remove(Wear); st.Cards[r.Wearer.Id].AbilityUses[BowAny] = st.TurnNumber; st.Cards[r.Wearer.Id].GrantedAbilities.Clear();
            st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.Phase = Phase.Action;
            var d = Add(st, Donor, 1, Zone.DynastyDiscard); d.RemovedKeywords = ["Berserker", "Tactician"]; d.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
            again = d.Id;
        });
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, again));
        Choose(e, 0, BowAny);
        Accept(e, new GameCommand("pass", 1, e.Version));
        var copied = Listed(e, 0, r.Wearer.Id, BowAny);
        Check(!copied.Enabled, "the re-copied ability is still spent this turn: " + copied.Label);
        // Using it once more with nothing on offer still ends the copy.
        e = Edit(e, st => { st.TurnNumber += 2; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.Phase = Phase.Action; });
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, third.Id));
        Check(e.State.PendingChoice is null, "nothing to decide");
        wearer = Now(e, r.Wearer.Id);
        Check(!wearer.GrantedKeywords.Any() && !wearer.GrantedAbilities.Any(), "using the ability again ends the old copies even when the new donor offers nothing");
    }

    private static MatchEngine NextTurn(MatchEngine e) =>
        Edit(e, st => { st.TurnNumber += 2; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.Phase = Phase.Action; st.PendingChoice = null; });

    private static void StepsAreIndependent()
    {
        var s = Table();
        var recv = Add(s, Receiver, 0, Zone.Home); // printed Samurai
        var d1 = Add(s, Donor, 1, Zone.DynastyDiscard);
        var d2 = Add(s, Donor, 1, Zone.DynastyDiscard);
        var d3 = Add(s, Donor, 1, Zone.DynastyDiscard);
        var e = MatchEngine.FromTestState(s);
        // Keyword only: one decision, then the action is over. The ability command is refused in the keyword step.
        Accept(e, Offer(e, 0, "training-copy-keyword", recv.Id, d1.Id));
        Eq(e.State.PendingChoice?.Type, "copyKeyword"); Same(Values(e), ["Berserker", "Tactician", "Unique"]);
        Eq(e.GetView(0).PendingCopy!.RemainingSteps.Length, 0, "no later step");
        Reject(e, new GameCommand("chooseCopyAbility", 0, e.Version, Choice: BowAny));
        Choose(e, 0, "Unique");
        Check(e.State.PendingChoice is null && Now(e, recv.Id).GrantedAbilities.Count == 0, "the ability decision never opened");
        Eq(e.State.PriorityPlayerId, 1);
        // Ability only, a turn later: one decision, and the keyword command is refused in the ability step.
        e = NextTurn(e);
        Accept(e, Offer(e, 0, "training-copy-ability", recv.Id, d2.Id));
        Eq(e.State.PendingChoice?.Type, "copyAbility"); Same(Values(e), [BowAny, CreateToken]);
        Reject(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: "Berserker"));
        Choose(e, 0, BowAny);
        Check(e.State.PendingChoice is null, "the keyword decision never opened");
        // Each copying ability's copies are its own: the earlier keyword copy is still there, alongside the new ability.
        var granted = Now(e, recv.Id);
        Check(granted.GrantedKeywords.Single().Keyword == "Unique" && granted.GrantedKeywords.Single().Source == "training-copy-keyword", "the keyword copy of the other ability persists");
        Check(granted.GrantedAbilities.Single().AbilityId == BowAny && granted.GrantedAbilities.Single().Source == "training-copy-ability");
        // A copy made "this turn": Duration Turn, never replaced, and gone when the turn ends -- while the Copy-duration ones stay.
        e = NextTurn(e);
        Accept(e, Offer(e, 0, "training-copy-ability-turn", recv.Id, d3.Id)); // the receiver already has training-bow-any: one candidate left, taken by itself
        Check(e.State.PendingChoice is null, "a single candidate is taken automatically");
        Eq(Now(e, recv.Id).GrantedAbilities.Single(g => g.AbilityId == CreateToken).Duration, "Turn");
        Check(Now(e, recv.Id).GrantedAbilities.Count == 2, "the Turn copy is in addition to the Copy one");
        e = Edit(e, st => { st.Phase = Phase.Dynasty; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null; });
        Accept(e, new GameCommand("endDynasty", 0, e.Version));
        var after = Now(e, recv.Id);
        Same(after.GrantedAbilities.Select(g => g.AbilityId), [BowAny]);
        Check(after.GrantedKeywords.Any(g => g.Keyword == "Unique"), "the Copy-duration keyword survived the turn");
    }

    private static void NothingOrOneCandidate()
    {
        // Nothing at all to copy: no decision, both steps announced as skipped, the action finishes and passes priority.
        var s = Table();
        var wearer = Add(s, Fukuzo, 0, Zone.Home);
        var bare = Add(s, "training-lion-samurai", 1, Zone.DynastyDiscard);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Wear, wearer.Id, bare.Id));
        Check(e.State.PendingChoice is null && e.State.PendingCopy is null, "no decision opens");
        Eq(Now(e, bare.Id).Zone, Zone.RemovedFromGame, "the cost was still paid");
        Eq(e.State.PriorityPlayerId, 1);
        Eq(e.State.Events.Count(x => x.Type == "copyNothing"), 2, "both steps are announced as skipped");
        Check(Now(e, wearer.Id).GrantedKeywords.Count == 0 && Now(e, wearer.Id).GrantedAbilities.Count == 0);
        // Exactly one keyword and one ability to copy: each is taken automatically (the printed "copies one" is not optional).
        s = Table();
        wearer = Add(s, Fukuzo, 0, Zone.Home);
        var one = Add(s, "training-battle-bower", 1, Zone.DynastyDiscard);
        one.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Wear, wearer.Id, one.Id));
        Check(e.State.PendingChoice is null && e.State.PendingCopy is null, "a single candidate needs no decision");
        Check(Now(e, wearer.Id).GrantedKeywords.Single().Keyword == "Kensai" && Now(e, wearer.Id).GrantedAbilities.Single().AbilityId == "training-bow-at-battle", "both were taken");
        Eq(e.State.Events.Count(x => x.Type == "keywordCopied"), 1); Eq(e.State.Events.Count(x => x.Type == "abilityCopied"), 1);
        Eq(e.State.PriorityPlayerId, 1);
        // One keyword to copy but two abilities: only the ability is a decision, and it is the FIRST decision.
        s = Table();
        wearer = Add(s, Fukuzo, 0, Zone.Home);
        var mixed = Add(s, Donor, 1, Zone.DynastyDiscard);
        mixed.RemovedKeywords = ["Berserker", "Tactician"]; mixed.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Wear, wearer.Id, mixed.Id));
        Eq(e.State.PendingChoice?.Type, "copyAbility"); Check(Now(e, wearer.Id).GrantedKeywords.Any(g => g.Keyword == "Kensai"), "the lone keyword was already taken");
    }

    private static void CopyingAbilitiesAreNotCopyable()
    {
        // The wearer does not print Fukuzo's ability, so only the copies-abilities exclusion keeps it off the list: a second Fukuzo as donor.
        var s = Table();
        var wearer = Add(s, Receiver, 0, Zone.Home);
        var donor = Add(s, Fukuzo, 1, Zone.DynastyDiscard);
        donor.GrantedAbilities = [new(BowAny, "test-fixture", "Permanent")];
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-copy-ability", wearer.Id, donor.Id));
        Check(e.State.PendingChoice is null, "one candidate: taken automatically");
        Same(Now(e, wearer.Id).GrantedAbilities.Select(g => g.AbilityId), [BowAny]);
        Check(!Now(e, wearer.Id).GrantedAbilities.Any(g => g.AbilityId == Wear), "an ability that copies abilities is never copied");
        // ...and the abilities of the training receiver: of its three, only the keyword-only one does not itself copy abilities.
        s = Table();
        wearer = Add(s, Fukuzo, 0, Zone.Home);
        donor = Add(s, Receiver, 1, Zone.DynastyDiscard);
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Wear, wearer.Id, donor.Id));
        Same(Now(e, wearer.Id).GrantedAbilities.Select(g => g.AbilityId), ["training-copy-keyword"]);
        Check(AbilityCatalog.All["training-copy-ability"].CopiesAbilities && AbilityCatalog.All["training-copy-ability-turn"].CopiesAbilities && !AbilityCatalog.All["training-copy-keyword"].CopiesAbilities);
        // A permanently granted ability that copies abilities is excluded just the same (the flag is on the ability, not on where it came from).
        s = Table();
        wearer = Add(s, Fukuzo, 0, Zone.Home);
        donor = Add(s, "training-battle-bower", 1, Zone.DynastyDiscard);
        donor.GrantedAbilities = [new("training-copy-ability", "test-fixture", "Permanent")];
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Wear, wearer.Id, donor.Id));
        Same(Now(e, wearer.Id).GrantedAbilities.Select(g => g.AbilityId), ["training-bow-at-battle"]);
    }

    private static void TargetCategory()
    {
        var s = Table();
        var wearer = Add(s, Fukuzo, 0, Zone.Home);
        var discarded = Add(s, Donor, 1, Zone.DynastyDiscard);
        var dead = Add(s, Donor, 0, Zone.Dead);
        var inPlay = Add(s, Donor, 1, Zone.Home);
        var inHand = Add(s, Donor, 1, Zone.Hand);
        var inProvince = Add(s, Donor, 1, Zone.Province, province: 3);
        var removed = Add(s, Donor, 1, Zone.RemovedFromGame);
        var strategy = Add(s, "training-bow-strategy", 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == wearer.Id && a.Label.StartsWith("Remove a target Personality"));
        Check(option.Enabled && option.TargetIds!.ToHashSet().SetEquals([discarded.Id, dead.Id]), "exactly the discarded and the dead Personality, either player's: " + string.Join(",", option.TargetIds ?? []));
        foreach (var bad in new[] { inPlay, inHand, inProvince, removed, strategy })
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: wearer.Id, TargetId: bad.Id, AbilityId: Wear));
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: wearer.Id, AbilityId: Wear));
        Accept(e, Offer(e, 0, Wear, wearer.Id, dead.Id)); // a dead Personality of the wearer's OWN controller
        Eq(Now(e, dead.Id).Zone, Zone.RemovedFromGame);
        // No candidate at all: listed disabled with the reason.
        s = Table(); Add(s, Fukuzo, 0, Zone.Home);
        e = MatchEngine.FromTestState(s);
        var none = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Label.StartsWith("Remove a target Personality"));
        Check(!none.Enabled && none.Reason == "No legal target or performer.", none.Reason ?? "");
    }

    private static void PermanentChangesSurviveDeath()
    {
        var s = Table();
        var wearer = Add(s, Fukuzo, 0, Zone.Home);
        var donor = Add(s, Donor, 0, Zone.Home);
        donor.Dishonorable = true;
        donor.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent"), new("Naval", "test-fixture", "Turn"), new("Ninja", "some-attachment", "Attachment")];
        donor.GrantedAbilities = [new("training-straighten-at-battle", "test-fixture", "Permanent"), new("training-honor-strike", "test-fixture", "Turn")];
        donor.RemovedKeywords = ["Berserker"];
        var e = MatchEngine.FromTestState(s);
        // Death by Seppuku, through the offered action.
        var seppuku = e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == donor.Id);
        Accept(e, seppuku.Command!);
        Eq(Now(e, donor.Id).Zone, Zone.Dead, "the Personality died");
        var dead = Now(e, donor.Id);
        Check(dead.GrantedKeywords.Any(g => g.Keyword == "Kensai") && dead.GrantedAbilities.Any(g => g.AbilityId == "training-straighten-at-battle"), "the permanent changes survived death");
        Check(dead.RemovedKeywords.Contains("Berserker"), "a removed keyword stays removed");
        // A whole turn later: turn-duration grants are gone, permanent ones and the removal remain on the dead card.
        e = Edit(e, st => { st.Phase = Phase.Dynasty; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null; });
        Accept(e, new GameCommand("endDynasty", 0, e.Version));
        dead = Now(e, donor.Id);
        Same(dead.GrantedKeywords.Select(g => g.Keyword).OrderBy(x => x, StringComparer.Ordinal), ["Kensai", "Ninja"]);
        Same(dead.GrantedAbilities.Select(g => g.AbilityId), ["training-straighten-at-battle"]);
        // Fukuzo is offered exactly the changes that survive: printed and permanent, less the removed one; never a turn or attachment grant.
        e = NextTurn(e);
        Accept(e, Offer(e, 0, Wear, wearer.Id, donor.Id));
        Eq(e.State.PendingChoice?.Type, "copyKeyword"); Same(Values(e), ["Tactician", "Kensai"]);
        Choose(e, 0, "Kensai");
        Same(Values(e), [BowAny, CreateToken, "training-straighten-at-battle"]);
        Choose(e, 0, "training-straighten-at-battle");
        Check(e.HasKeyword(wearer.Id, "Kensai") && Now(e, wearer.Id).GrantedAbilities.Single().AbilityId == "training-straighten-at-battle");
        Check(!e.HasKeyword(wearer.Id, "Berserker") && !e.HasKeyword(wearer.Id, "Naval") && !e.HasKeyword(wearer.Id, "Ninja"), "removed, turn and attachment keywords were not copied");
    }

    private static void SnapshotAtRemoval()
    {
        var r = Skin();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, r.Donor.Id));
        Eq(Now(e, r.Donor.Id).Zone, Zone.RemovedFromGame, "the donor is out of the game while the player decides");
        Same(Values(e), ["Berserker", "Tactician"]);
        // Whatever happens to the removed donor now does not change what was on offer, and the answer is checked against the snapshot.
        e = Edit(e, st => { var d = st.Cards[r.Donor.Id]; d.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")]; d.RemovedKeywords = ["Tactician"]; });
        Same(Values(e), ["Berserker", "Tactician"], "the list is a snapshot");
        Reject(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: "Kensai"));
        Choose(e, 0, "Tactician");
        Check(e.HasKeyword(r.Wearer.Id, "Tactician"), "a snapshot keyword is copied even though the source has since lost it");
    }

    private static void CopiesPersistAndReset()
    {
        // Persist through death: Fukuzo (a Dishonorable Samurai) copies, then dies by Seppuku, and keeps what he copied.
        var r = Skin();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, r.Donor.Id));
        Choose(e, 0, "Berserker"); Choose(e, 0, BowAny);
        e = Edit(e, st => { st.Cards[r.Wearer.Id].Dishonorable = true; st.PriorityPlayerId = 0; st.ConsecutivePasses = 0; });
        Accept(e, e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == r.Wearer.Id).Command!);
        var dead = Now(e, r.Wearer.Id);
        Eq(dead.Zone, Zone.Dead);
        Check(dead.GrantedKeywords.Any(g => g.Keyword == "Berserker" && g.Duration == "Copy") && dead.GrantedAbilities.Any(g => g.AbilityId == BowAny), "a Personality keeps what it copied through death");
        // Reset on return to a deck: a bare enemy Personality carrying copies goes to the bottom of its Dynasty deck (Goju House Guard) as a fresh card.
        var s = Table(Phase.Battle, 0); s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var goju = Add(s, "oracle-l5r-2851", 0, Zone.Attached, province: 0, parent: host.Id);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        enemy.GrantedKeywords = [new("Berserker", Wear, "Copy", Donor, "Training skin donor")]; enemy.GrantedAbilities = [new(BowAny, Wear, "Copy", Donor, "Training skin donor")];
        e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: goju.Id, TargetId: enemy.Id, AbilityId: "goju-house-guard-return", DeckPosition: "Bottom"));
        var back = Now(e, enemy.Id);
        Eq(back.Zone, Zone.DynastyDeck); Check(back.GrantedKeywords.Count == 0 && back.GrantedAbilities.Count == 0, "a card returned to a deck starts fresh");
    }

    private static void GrantedAbilityLifecycle()
    {
        // Turn-duration clears at turn end; permanent does not.
        var s = Table(Phase.Dynasty);
        var p = Add(s, "training-crane-samurai", 0, Zone.Home);
        p.GrantedAbilities = [new(BowAny, "test-fixture", "Turn"), new(CreateToken, "test-fixture", "Permanent")];
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("endDynasty", 0, e.Version));
        Same(Now(e, p.Id).GrantedAbilities.Select(g => g.AbilityId), [CreateToken]);
        // An Experienced overlay carries granted abilities over to the replacement (and does not leave a second copy on the removed card).
        s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Dynasty; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].Clan = "Scorpion"; s.Players[0].Honor = 0;
        var higherId = s.Players[0].Provinces[0].CardId!; var higher = s.Cards[higherId];
        higher.DefinitionId = "training-paneki-exp3"; higher.FaceUp = true;
        var lower = Add(s, "training-paneki-base", 0, Zone.Home);
        lower.GrantedAbilities = [new(CreateToken, "test-fixture", "Permanent")];
        e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("overlay", 0, e.Version, CardId: higher.Id, TargetId: lower.Id, SourceIds: []));
        Same(Now(e, higher.Id).GrantedAbilities.Select(g => g.AbilityId), [CreateToken]);
        Check(Now(e, lower.Id).GrantedAbilities.Count == 0, "the removed card keeps no duplicate");
        // A card that has one is offered it as an ordinary action: no printed copy of it required.
        s = Table();
        var holder = Add(s, "training-crane-samurai", 0, Zone.Home);
        holder.GrantedAbilities = [new(CreateToken, "test-fixture", "Permanent")];
        var target = Add(s, "training-lion-samurai", 0, Zone.Home);
        e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, CreateToken, holder.Id, target.Id));
        Check(Now(e, target.Id).Tokens.Count == 1, "a permanently granted ability resolves like a printed one");
    }

    private static void CopiedReactionIsLive()
    {
        var s = Table();
        var wearer = Add(s, Fukuzo, 0, Zone.Home);
        var tamoko = Add(s, "oracle-l5r-10056", 1, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, Wear, wearer.Id, tamoko.Id));
        Choose(e, 0, Values(e)[0]);
        Check(Values(e).Contains("tamoko-negate"), "the Reaction is a copyable ability");
        Choose(e, 0, "tamoko-negate");
        Check(Now(e, wearer.Id).GrantedAbilities.Any(g => g.AbilityId == "tamoko-negate"));
        // The Battle Phase: another player straightens one of his own bowed Personalities -- Tamoko's reaction applies to any Personality.
        string straightener = "", target = "";
        e = Edit(e, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.PriorityPlayerId = 1; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PendingChoice = null;
            st.Cards[wearer.Id].Zone = Zone.Battlefield; st.Cards[wearer.Id].ProvinceIndex = 0; // #1133: a Reaction in a battle needs its unit at the battlefield
            straightener = Add(st, "training-battle-straightener", 1, Zone.Battlefield, province: 0).Id;
            target = Add(st, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0).Id;
        });
        Accept(e, Offer(e, 1, "training-straighten-at-battle", straightener, target));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction"); Eq(e.State.PriorityPlayerId, 0);
        var reaction = e.QueryActions(0).SingleOrDefault(a => a.Type == "useAbility" && a.CardId == wearer.Id && a.Command?.AbilityId == "tamoko-negate");
        Check(reaction is { Enabled: true }, "Fukuzo's copied Reaction is offered in the window: " + string.Join("; ", e.QueryActions(0).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}]")));
        Accept(e, reaction!.Command!);
        Pass(e, 1); Pass(e, 0);
        Check(Now(e, target).Bowed, "the copied reaction negated the straightening");
    }

    private static void CopyShowcase()
    {
        var e = MatchEngine.CreateCopyShowcase();
        Eq(e.State.Phase, Phase.Action); Eq(e.State.PriorityPlayerId, 0);
        var fuk = e.State.Cards.Values.Single(c => c.DefinitionId == Fukuzo);
        var option = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == fuk.Id && a.Label.StartsWith("Remove a target Personality"));
        Check(option.Enabled && option.TargetIds!.Length == 5, "four discarded and one dead Personality: " + option.TargetIds?.Length);
        var tamoko = option.TargetIds!.Select(id => e.State.Cards[id]).First(c => c.DefinitionId == "oracle-l5r-10056" && c.Zone == Zone.Dead);
        Accept(e, option.Command! with { TargetId = tamoko.Id });
        var steps = 0;
        while (e.State.PendingCopy is not null) { Accept(e, Answers(e, 0)[0].Command!); Check(++steps <= 2, "at most the two steps"); }
        Check(steps >= 1 && Now(e, fuk.Id).GrantedKeywords.Count + Now(e, fuk.Id).GrantedAbilities.Count >= 1, "Fukuzo copied something");
        var replay = MatchEngine.Replay(e.ExportReplay());
        Eq(System.Text.Json.JsonSerializer.Serialize(replay.State), System.Text.Json.JsonSerializer.Serialize(e.State), "the replay reproduces the state");
        // The single-step fixtures are on the table too.
        Check(e.State.Cards.Values.Any(c => c.DefinitionId == Receiver && c.OwnerId == 0) && e.State.Cards.Values.Any(c => c.DefinitionId == Donor && c.Zone == Zone.DynastyDiscard));
    }

    private static void Pass(MatchEngine e, int player) => Accept(e, new GameCommand("pass", player, e.Version));

    private static void OffersValidateAndRejections()
    {
        var r = Skin();
        var sacrificial = Add(r.S, Donor, 0, Zone.Home); sacrificial.Dishonorable = true;
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, Wear, r.Wearer.Id, r.Donor.Id));
        // Keyword decision: every offered command validates on a fresh engine that reached the same position.
        MatchEngine Fresh(params string[] answers)
        {
            var f = MatchEngine.FromTestState(r.S);
            Accept(f, Offer(f, 0, Wear, r.Wearer.Id, r.Donor.Id));
            foreach (var a in answers) Choose(f, 0, a);
            return f;
        }
        foreach (var option in Answers(e, 0)) Accept(Fresh(), option.Command!);
        Choose(e, 0, "Berserker");
        foreach (var option in Answers(e, 0)) Accept(Fresh("Berserker"), option.Command!);
        // Rejections leave the state untouched: the wrong player, the wrong step, the wrong value, no value, an unrelated command.
        Reject(e, new GameCommand("chooseCopyAbility", 1, e.Version, Choice: BowAny));
        Reject(e, new GameCommand("chooseCopyAbility", 0, e.Version, Choice: EntryReaction));
        Reject(e, new GameCommand("chooseCopyAbility", 0, e.Version, Choice: "fukuzo-wear-skin"));
        Reject(e, new GameCommand("chooseCopyAbility", 0, e.Version));
        Reject(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: "Tactician"));
        Reject(e, new GameCommand("pass", 0, e.Version));
        Reject(e, e.QueryActions(0).First(a => a.Type == "chooseCopyAbility").Command! with { ExpectedVersion = e.Version - 1 });
        // Seppuku (an Open action taken only at one's own turn to act with nothing pending, issue #368) is not offered, and is refused, while a copy is open: its receiver could be the
        // Personality destroyed.
        Check(!e.QueryActions(0).Any(a => a.Type == "orderSeppuku"), "Seppuku is not offered mid-copy");
        Reject(e, new GameCommand("orderSeppuku", 0, e.Version, CardId: sacrificial.Id));
    }
}
