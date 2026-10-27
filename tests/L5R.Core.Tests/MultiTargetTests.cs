using System.Text.Json;
using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Multi-target actions (issue #336): an ordered list of independent target slots -- each any card category or a player, each with its own
// count -- validated and offered as a whole set in one command. Proven on Doji Yoshitada (a Personality and a player), Bonds of Coin and Gold and
// Steel (one to three attachments with a 0 Gold cost), Fear Me! (a Personality, then a player only if it moved) and on synthetic fixtures for the
// shapes no card needs yet (a Holding, an enemy Personality and a player at once; up to three with none allowed; a slot naming the actor's hand).
// Readings and alternatives: docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md.
internal static class MultiTargetTests
{
    private const string Yoshitada = "oracle-l5r-2179", BondsOfCoin = "oracle-l5r-1092", GoldAndSteel = "oracle-l5r-2865", FearMe = "oracle-l5r-2493", Bofana = "oracle-l5r-1706";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Yoshitada (Force 3, Chi 4, Honor Requirement 6, Cost 7, Personal Honor 3, Crane Duelist Magistrate), Bonds of Coin, Gold and Steel and Fear Me! are the printed cards, whole, with their art", PublishedShape),
            ("Yoshitada: \"Bow him.\" is its own sentence and always happens; the player choice belongs to the second and is asked AT RESOLUTION, after the bow, only if he is dishonorable (Mendon's ruling) -- then one decision offers each player, gain or lose, and each answer is applied; an honorable target asks nothing and changes nobody's Honor", YoshitadaBowsThenAsksOnlyIfDishonorable),
            ("The two sentences are independent: a bow negated by Stalwart does not stop the follow-up for a dishonorable target, and a target who is not dishonorable never asks -- and the follow-up is judged after the bow, on the target as he then is", SentencesAreIndependent),
            ("Yoshitada's rehonor rule through the prompt: a gain for his own controller while HE is dishonorable rehonors him instead of gaining Honor; a loss is a plain loss; a gain for the other player is a plain gain", YoshitadaRehonorRule),
            ("Yoshitada's command carries only the up-front slot: the target is checked on its own (friendly, absent, out of play, two targets, a conflicting TargetId are refused atomically) and the follow-up slot may NOT be answered in the command", YoshitadaValidation),
            ("Yoshitada is offered ONCE, carrying the up-front target slot with its legal candidates and the follow-up slot as an at-resolution entry naming its condition and the answers it will offer; the default command holds only the target", YoshitadaOffered),
            ("The open decision is answered only by the acting player and only with one of the offered options: every option validates and resolves on a fresh engine; another player, a wrong choice, a pass and any other action are refused atomically, the decision is public and nothing hidden is in it", TheDecisionIsAnsweredOnlyByTheActingPlayer),
            ("The order is: before-action reaction windows, then the bow, then the follow-up decision, then the tail -- a reaction that negates the whole action stops both the bow and the decision", ReactionsThenBowThenDecision),
            ("Every reader of \"targets\" sees a multi-target action's target: Bofana's trigger asks the enemy for a discard when Yoshitada's Personality slot names him", BofanaSeesSlotTarget),
            ("Each target of a multi-target action gets its OWN before-action window, in slot order (Mendon's ruling): two targets that each have a possible reactor open two windows; a target with no possible reactor opens none; a reaction played in one window is about that window's target only", OneWindowPerTarget),
            ("Each per-target window is the alternating window of #318/#327: the first opportunity is the active player's when he holds a reaction for that target (otherwise the other player's), passes hand it over, two passes close it; negations from several windows compose and a whole-action negation skips the windows still to come", PerTargetWindowsAlternate),
            ("Per-target windows meet R's \"targeted by an action\" reactions (#335) and the curated before-action ones (#318): a two-target action whose first target is a Merchant with Jinn-Kuen's shape of negation and whose second is Akodo Kakihara opens the Merchant's window, then Kakihara's, each offering only ITS reactor; the negation of the whole action played in the first window skips the second; Sakuma's Political reaction reads the window's target", TargetedReactionsPerTarget),
            ("Ezuno's redirect (#335) on a multi-target action: the window's target is replaced IN ITS SLOT (the selection and the primary TargetId follow), the window is then about Ezuno, the other targets keep their windows, and the action resolves on Ezuno, the untouched second target and not the Shugenja; a redirect onto a card the slot already holds or an illegal one is not offered", RedirectInsideASlot),
            ("Negated actions pay what they declare (#356): Bonds of Coin's Favor half declares \"bow your Stronghold\" as a cost (Costs) and discards the Favor, so a negated one still bows the Stronghold and discards the Favor but draws nothing; every other action of this change declares no cost beyond its Gold and its Strategy, so negated it changes nothing and is still taken", NegatedActionsPayWhatTheyDeclare),
            ("A multi-target action is ONE action to out-of-play windows (#367): Unimpeachable Name is asked once for the action, not once per target, and only while a copy can really be played from where it is; Sakuma's Political reaction and Kakihara's own each get one window per target that has them", OutOfPlayAndPoliticalWindowsPerTarget),
            ("Bonds of Coin, Political Open: one to three attachments with a 0 Gold cost, of either player -- exactly the ones costing 0 now (a printed 0 or one taken there by a Gold cost penalty) are offered, one, two or three are destroyed together, none or four are refused", BondsOfCoinDestroys),
            ("Bonds of Coin, Favor Political Limited: bow your Stronghold and discard the Imperial Favor to draw two cards -- only with the Favor and an unbowed Stronghold, only on your own turn, and it counts as a Favor action", BondsOfCoinDraws),
            ("Gold and Steel, Battle/Open: the same up-to-three destroy, anywhere as an Open action but only at the current battlefield as a Battle one", GoldAndSteelDestroys),
            ("Gold and Steel, Battle: your performing unbowed Personality bows an enemy Personality with an equal or lower Gold cost (current cost: a penalty can bring a target under), and the paired ability also moves him home; the two share one use per turn", GoldAndSteelBows),
            ("Fear Me!: your performing unbowed Duelist moves an enemy Personality with equal or lower Chi home; ONLY if he moved is a player (and gain or lose) asked at resolution -- nothing is asked, and no Honor changes, if the move was negated", FearMeMoves),
            ("\"Equal or lower Chi\" is CURRENT Chi (Mendon's ruling): a Chi modifier or an Item's Chi on either Personality flips who is a legal target of Fear Me!, Chokehold and Ring of Fire alike, while cards whose text says printed or base keep reading printed or base", CurrentChiFlipsTheResult),
            ("A slot may be ANY card category and a player, independently: a Holding, an enemy Personality and a player on one action, each judged on its own, and every card type in the wrong slot refused", MixedSlots),
            ("Up to three, including none: a zero-to-three slot accepts an empty selection and three cards but not four, and its default command is the empty one", UpToThreeIncludingNone),
            ("Hidden information: a slot that names a card in the actor's hand offers only the actor's own hand, never the opponent's -- no opponent hand card id appears in the options, and naming one is refused", HandSlotIsPrivate),
            ("A selection survives JSON (a wire command and an offered option round-trip with their slots), and an ability with slots offers nothing the single-target listing could mistake for a target", SelectionsSurviveJson),
        ];
        runner.Run("multi-target", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // ---- rigs --------------------------------------------------------------------------------------------------------------
    // The Battle Phase at province 0: player 0 (the Defender, holding the opportunity) has Yoshitada there; player 1 has an enemy Personality
    // there (`dishonorable` when asked) and another at home.
    private sealed record YoshitadaRig(MatchState S, CardInstance Yoshitada, CardInstance Enemy, CardInstance AtHome);
    private static YoshitadaRig Yoshi(bool dishonorable = true, string yoshitada = Yoshitada)
    {
        var s = Table(); s.PriorityPlayerId = 0;
        var y = Add(s, yoshitada, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); enemy.Dishonorable = dishonorable;
        var home = Add(s, "training-lion-samurai", 1, Zone.Home);
        return new(s, y, enemy, home);
    }

    private const string YoshiAbility = "yoshitada-bow", FearMeMove = "fear-me-move";

    // Yoshitada's whole action: the command names only the target.
    private static GameCommand Yo(MatchEngine e, YoshitadaRig r, string target, int actor = 0) =>
        Use(e, actor, YoshiAbility, r.Yoshitada.Id, selections: Pick("target", target));

    private static ActionOption[] Answers(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect").ToArray();
    private static bool Asked(MatchEngine e) => e.State.PendingChoice?.Type == "effectChoice";
    private static string[] OptionIds(ActionOption[] answers) => answers.Select(a => a.Command!.Choice!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    private static void Answer(MatchEngine e, int player, string choice)
    {
        var option = Answers(e, player).SingleOrDefault(a => a.Command!.Choice == choice);
        Check(option is { Enabled: true }, $"the answer {choice} was not offered to player {player}: " + string.Join(",", Answers(e, player).Select(a => a.Command!.Choice)));
        Accept(e, option!.Command!);
    }
    private static readonly string[] HonorAnswers = ["opponent-gain", "opponent-lose", "self-gain", "self-lose"];

    // ---- published shape ----------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.StrategyWave21.Cards.Length, 3, "Bonds of Coin, Gold and Steel, Fear Me!");
        var y = CardCatalog.Get(Yoshitada);
        Check(!y.ScenarioOnly && y.Type == CardType.Personality && y.Name == "Doji Yoshitada" && y.Clan == "Crane");
        Eq(y.Force, 3); Eq(y.Chi, 4); Eq(y.HonorRequirement, 6); Eq(y.GoldCost, 7); Eq(y.PersonalHonor, 3);
        foreach (var k in new[] { "Duelist", "Crane Clan", "Magistrate", "Samurai" }) Check(y.Keywords!.Contains(k), k);
        Eq(y.Text, "Political Battle: Target an enemy Personality: Bow him. If he is dishonorable, choose a player who gains or loses 2 Honor.");
        Check(y.Abilities!.SequenceEqual(new[] { YoshiAbility }) && y.Effect == "none");
        {
            var a = AbilityCatalog.All[YoshiAbility];
            Eq(a.Timing, "Battle"); Eq(a.Target, "slots"); Check(a.ActionKeywords!.SequenceEqual(new[] { "Political" }));
            Check(a.Slots!.Select(x => (x.Id, x.Category, x.Min, x.Max, x.AtResolution)).SequenceEqual(new[] { ("target", "enemy-personality", 1, 1, false), ("honor", "player", 0, 0, true) }),
                "an up-front enemy Personality, then a conditional follow-up player choice asked at resolution");
            Eq(a.Slots![1].Resolution!.Condition, BranchCondition.TargetDishonorable); Eq(a.Slots[1].Resolution!.Otherwise.Effects.Length, 0);
            Check(a.Slots[1].Resolution!.Options.Select(o => o.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(HonorAnswers), "each player, gain or lose");
        }
        var b = CardCatalog.Get(BondsOfCoin);
        Check(b.Type == CardType.Strategy && b.Name == "Bonds of Coin" && b.GoldCost == 0 && b.Focus == 4 && b.Keywords!.SequenceEqual(new[] { "Favor", "Political" }));
        Eq(b.Text, "Political Open: Destroy one to three target attachments with a 0 Gold Cost.\nFavor Political Limited: Bow your Stronghold and discard the Imperial Favor to draw two cards.");
        Check(b.Abilities!.SequenceEqual(new[] { "bonds-of-coin-destroy", "bonds-of-coin-draw" }));
        var g = CardCatalog.Get(GoldAndSteel);
        Check(g.Type == CardType.Strategy && g.Name == "Gold and Steel" && g.GoldCost == 0 && g.Focus == 2);
        Eq(g.Text, "Battle/Open: Target one to three attachments with a Gold Cost of 0: Destroy them.\nBattle: Choose your performing unbowed Personality and target an enemy Personality with an equal or lower Gold Cost: Bow him. You may move him home.");
        Check(g.Abilities!.SequenceEqual(new[] { "gold-and-steel-destroy", "gold-and-steel-bow", "gold-and-steel-bow-move" }));
        var f = CardCatalog.Get(FearMe);
        Check(f.Type == CardType.Strategy && f.Name == "Fear Me!" && f.GoldCost == 0 && f.Focus == 4 && f.Keywords!.SequenceEqual(new[] { "Fear", "Iaijutsu" }));
        Eq(f.Text, "Battle: Choose your performing unbowed Duelist and target an enemy Personality with equal or lower Chi: Move him home. If he moved, choose a player, who gains or loses 2 Honor.");
        Check(f.Abilities!.SequenceEqual(new[] { FearMeMove }));
        Check(AbilityCatalog.All[FearMeMove].Slots!.Select(x => (x.Id, x.AtResolution)).SequenceEqual(new[] { ("target", false), ("honor", true) }) && AbilityCatalog.All[FearMeMove].Slots![1].Resolution!.Condition == BranchCondition.TargetMoved, "the follow-up is asked only if he moved");
        foreach (var id in new[] { Yoshitada, BondsOfCoin, GoldAndSteel, FearMe })
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // ---- Yoshitada ----------------------------------------------------------------------------------------------------------
    private static void YoshitadaBowsThenAsksOnlyIfDishonorable()
    {
        foreach (var (choice, player, delta) in new[] { ("self-gain", 0, 2), ("opponent-gain", 1, 2), ("self-lose", 0, -2), ("opponent-lose", 1, -2) })
        {
            var r = Yoshi(dishonorable: true);
            var e = MatchEngine.FromTestState(r.S);
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Yo(e, r, r.Enemy.Id));
            // "Bow him." has already happened; the second sentence waits for the player.
            Check(e.State.Cards[r.Enemy.Id].Bowed, "the bow came first");
            Check(Asked(e) && e.State.PendingChoice!.PlayerId == 0 && e.State.PriorityPlayerId == 0, "the acting player is asked");
            Check(Honor(e, 0) == before[0] && Honor(e, 1) == before[1], "nothing has changed yet");
            Check(OptionIds(Answers(e, 0)).SequenceEqual(HonorAnswers), "one answer per player and direction: " + string.Join(",", OptionIds(Answers(e, 0))));
            Check(Answers(e, 0).All(a => a.Enabled && a.Label.Contains("2 Honor")), "each says what it does");
            Check(!e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled), "the action waits: nothing else is offered");
            Answer(e, 0, choice);
            Eq(Honor(e, player), before[player] + delta, $"{choice}"); Eq(Honor(e, 1 - player), before[1 - player], "the other player's Honor is untouched");
            Check(e.State.PendingChoice is null && e.State.PendingEffectChoice is null, "the decision is closed");
            Check(e.State.Cards[r.Yoshitada.Id].AbilityUses.ContainsKey(YoshiAbility), "the action is spent");
            Eq(e.State.PriorityPlayerId, 1, "priority passed on, as after any action");
            Check(e.State.Events.Any(x => x.Type == "effectChosen"), "the answer is in the public log");
        }
        {
            // An honorable target: the bow, then nothing -- no prompt, no Honor change, and the action finishes at once.
            var r = Yoshi(dishonorable: false);
            var e = MatchEngine.FromTestState(r.S);
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Yo(e, r, r.Enemy.Id));
            Check(e.State.Cards[r.Enemy.Id].Bowed, "bowed");
            Check(!Asked(e) && e.State.PendingChoice is null, "nothing is asked");
            Check(Honor(e, 0) == before[0] && Honor(e, 1) == before[1], "an honorable target changes nobody's Honor");
            Eq(e.State.PriorityPlayerId, 1, "and the action finished");
        }
        {
            // Another, dishonorable Personality of his does not make this target dishonorable.
            var r = Yoshi(dishonorable: false);
            Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Dishonorable = true;
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Yo(e, r, r.Enemy.Id));
            Check(!Asked(e), "only the target's own status counts");
        }
    }

    private static void SentencesAreIndependent()
    {
        {
            // The bow is negated by Stalwart; the second sentence does not depend on it.
            var r = Yoshi(dishonorable: true);
            r.Enemy.GrantedKeywords.Add(new("Stalwart", "test", "Turn"));
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Yo(e, r, r.Enemy.Id));
            Check(!e.State.Cards[r.Enemy.Id].Bowed, "Stalwart negated the bow");
            Check(Asked(e), "the follow-up is asked all the same: the sentences are independent");
            Answer(e, 0, "opponent-lose");
            Eq(Honor(e, 1), 4, "and applies (6 - 2)");
        }
        {
            // Judged on the target as he is when the action resolves, after the bow.
            var r = Yoshi(dishonorable: true);
            var e = MatchEngine.FromTestState(r.S);
            Check(Enabled(e, 0, YoshiAbility, r.Yoshitada.Id).Slots![1].Condition == BranchCondition.TargetDishonorable, "the offer names the condition");
            Accept(e, Yo(e, r, r.Enemy.Id));
            Check(Asked(e), "dishonorable at resolution: asked");
        }
    }

    private static void YoshitadaRehonorRule()
    {
        foreach (var (choice, honorDelta, rehonored) in new[] { ("self-gain", 0, true), ("self-lose", -2, false), ("opponent-gain", 0, false) })
        {
            var r = Yoshi(); r.Yoshitada.Dishonorable = true;
            var e = MatchEngine.FromTestState(r.S);
            var mine = Honor(e, 0); var theirs = Honor(e, 1);
            Accept(e, Yo(e, r, r.Enemy.Id)); Answer(e, 0, choice);
            Eq(Honor(e, 0), mine + honorDelta, $"{choice}: his controller's Honor");
            Eq(Honor(e, 1), choice == "opponent-gain" ? theirs + 2 : theirs, $"{choice}: the other player's Honor");
            Eq(!e.State.Cards[r.Yoshitada.Id].Dishonorable, rehonored, $"{choice}: whether he was rehonored instead");
        }
    }

    private static void YoshitadaValidation()
    {
        var r = Yoshi();
        var own = Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var holding = Add(r.S, "training-farm", 1, Zone.Home);
        var follower = Add(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var second = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        GameCommand Base(params SlotSelection[] sel) => Use(e, 0, YoshiAbility, r.Yoshitada.Id, selections: sel);
        var target = Pick("target", r.Enemy.Id);
        Reject(e, Base()); Reject(e, Base(PickPlayer("target", 1)));
        Reject(e, Base(target, target));
        Reject(e, Base(target, PickPlayer("honor", 1))); Reject(e, Base(target, Pick("honor", r.Enemy.Id))); Reject(e, Base(target, new SlotSelection("honor")));
        Reject(e, Base(target, PickPlayer("player", 1)));
        Reject(e, Base(new SlotSelection("target", [r.Enemy.Id], 1)));
        Reject(e, Base(Pick("target", own.Id))); Reject(e, Base(Pick("target", holding.Id))); Reject(e, Base(Pick("target", follower.Id)));
        Reject(e, Base(Pick("target", r.AtHome.Id)));
        Reject(e, Base(Pick("target", r.Enemy.Id, second.Id)));
        Reject(e, Base(Pick("target"))); Reject(e, Base(Pick("target", "no-such-card")));
        Reject(e, Base(target) with { CardIds = [r.Enemy.Id] }); Reject(e, Base(target) with { TargetPlayerId = 1 });
        Reject(e, Base(target) with { TargetId = second.Id }); Reject(e, Base(target) with { PerformerId = own.Id });
        Accept(e, Base(target) with { TargetId = r.Enemy.Id });
        Check(e.State.Cards[r.Enemy.Id].Bowed && !e.State.Cards[second.Id].Bowed, "the chosen target, and only that one, is bowed");
    }

    private static void YoshitadaOffered()
    {
        var r = Yoshi();
        Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var second = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        var options = Options(e, 0, YoshiAbility, r.Yoshitada.Id);
        Eq(options.Length, 1, "one option -- no gain and lose variants any more");
        var option = options.Single(); Check(option.Enabled, option.Reason ?? "");
        Check(option.TargetIds is null, "no single TargetIds a viewer could mistake for the whole choice");
        var slots = option.Slots!;
        Eq(slots.Length, 2);
        Eq(slots[0].Slot, "target"); Eq(slots[0].Kind, "card"); Eq(slots[0].Timing, "up-front"); Eq(slots[0].Min, 1); Eq(slots[0].Max, 1);
        Check(slots[0].CardIds.ToHashSet().SetEquals([r.Enemy.Id, second.Id]), "the enemy Personalities AT the battlefield, no friendly one, none at home: " + string.Join(",", slots[0].CardIds));
        Eq(slots[1].Slot, "honor"); Eq(slots[1].Kind, "follow-up"); Eq(slots[1].Timing, "at-resolution"); Eq(slots[1].Condition, "target-dishonorable");
        Check(slots[1].OptionLabels!.Length == 4 && slots[1].CardIds.Length == 0 && slots[1].PlayerIds.Length == 0 && slots[1].Min == 0 && slots[1].Max == 0, "the follow-up is described, and asks for nothing now");
        Check(slots.All(s => s.Label.Length > 0), "each slot says what it is for");
        var command = option.Command!;
        Check(command.Selections is [{ Slot: "target" }], "the default command holds only the up-front target");
        Eq(command.TargetId, command.Selections![0].CardIds![0], "and carries it as TargetId too");
        var fresh = MatchEngine.FromTestState(r.S); Accept(fresh, command with { ExpectedVersion = fresh.Version });
        // Nothing to target: the option is there, disabled with a reason.
        var none = Yoshi(); none.S.Cards.Remove(none.Enemy.Id);
        var listed = Options(MatchEngine.FromTestState(none.S), 0, YoshiAbility, none.Yoshitada.Id).Single();
        Check(!listed.Enabled && listed.Reason is not null && listed.Slots![0].CardIds.Length == 0, "disabled, with its reason, when no enemy is at the battlefield: " + listed.Reason);
    }

    private static void TheDecisionIsAnsweredOnlyByTheActingPlayer()
    {
        var r = Yoshi();
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Yo(e, r, r.Enemy.Id));
        Check(Asked(e), "asked");
        foreach (var answer in Answers(e, 0)) { var fresh = MatchEngine.FromTestState(r.S); Accept(fresh, Yo(fresh, r, r.Enemy.Id)); Accept(fresh, answer.Command! with { ExpectedVersion = fresh.Version }); Check(fresh.State.PendingChoice is null, $"{answer.Command!.Choice} closes it"); }
        Check(Answers(e, 1).Length == 0, "the other player is offered nothing to answer");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede" or "orderSeppuku"), "the other player only waits: " + string.Join(",", e.QueryActions(1).Select(a => a.Type)));
        Reject(e, new GameCommand("chooseEffect", 1, e.Version, Choice: "self-gain"));
        Reject(e, new GameCommand("chooseEffect", 0, e.Version, Choice: "no-such-choice")); Reject(e, new GameCommand("chooseEffect", 0, e.Version));
        Reject(e, new GameCommand("chooseEffect", 0, e.Version, Choice: "both"));
        Reject(e, new GameCommand("pass", 0, e.Version)); Reject(e, new GameCommand("declineReaction", 0, e.Version));
        Reject(e, Use(e, 0, "training-bow-at-battle", r.Yoshitada.Id, r.Enemy.Id));
        // Public: the prompt reveals no hidden card, and both views carry the same decision.
        var json = System.Text.Json.JsonSerializer.Serialize(e.GetView(1));
        Check(e.GetView(1).PendingChoice?.Type == "effectChoice" && e.GetView(0).PendingChoice?.Type == "effectChoice", "both players see the decision is open");
        Answer(e, 0, "self-lose");
    }

    private static void ReactionsThenBowThenDecision()
    {
        // The target's controller (player 1) holds a whole-action negator: its window opens BEFORE the bow.
        var r = Yoshi(dishonorable: true);
        var negator = Add(r.S, "training-action-negator", 1, Zone.Home);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Yo(e, r, r.Enemy.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction"); Check(!e.State.Cards[r.Enemy.Id].Bowed, "not bowed yet: the window comes first");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "declineReaction" && a.Enabled).Command!);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "declineReaction" && a.Enabled).Command!);
        Check(e.State.Cards[r.Enemy.Id].Bowed && Asked(e), "both passed: the bow, then the decision");
        Answer(e, 0, "self-lose");
        // Negated whole: neither the bow nor the decision.
        var r2 = Yoshi(dishonorable: true); Add(r2.S, "training-action-negator", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(r2.S);
        var honor = Honor(e2, 0);
        Accept(e2, Yo(e2, r2, r2.Enemy.Id));
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled).Command!);
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "declineReaction" && a.Enabled).Command!);
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "declineReaction" && a.Enabled).Command!);
        Check(!e2.State.Cards[r2.Enemy.Id].Bowed, "negated: not bowed"); Check(!Asked(e2) && e2.State.PendingChoice is null, "negated: nothing asked"); Eq(Honor(e2, 0), honor);
        Check(e2.State.Cards[r2.Yoshitada.Id].AbilityUses.ContainsKey(YoshiAbility), "the action was still taken");
    }

    private static void BofanaSeesSlotTarget()
    {
        var r = Yoshi(dishonorable: true);
        var bofana = Add(r.S, Bofana, 1, Zone.Battlefield, province: 0);
        Add(r.S, "training-farm", 0, Zone.Hand); Add(r.S, "training-farm", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Yo(e, r, bofana.Id));
        Eq(e.State.PendingChoice?.Type, "forcedDiscard"); Eq(e.State.PendingChoice?.PlayerId, 0, "Yoshitada's controller must discard: his Force 3 is below Bofana's 7");
        Check(!e.State.Cards[bofana.Id].Bowed, "the action waits for the discard, as for any action that targets him");
        Accept(e, e.QueryActions(0).First(a => a.Type == "forcedDiscard" && a.Enabled).Command!);
        Check(e.State.Cards[bofana.Id].Bowed, "and then resolves");
        // The same action against a different card asks nothing.
        var r2 = Yoshi(); Add(r2.S, Bofana, 1, Zone.Battlefield, province: 0); Add(r2.S, "training-farm", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Yo(e2, r2, r2.Enemy.Id));
        Check(e2.State.PendingChoice?.Type != "forcedDiscard", "an action that does not target Bofana does not trigger him");
        // Not only the FIRST target: Bofana named by a LATER card slot (a Holding first, an enemy Personality second) is targeted just the same.
        var s3 = Table(Phase.Action, actor: 0); s3.PriorityPlayerId = 0;
        var source3 = Add(s3, "training-slots-source", 0, Zone.Home); var holding3 = Add(s3, "training-farm", 1, Zone.Home); var bofana3 = Add(s3, Bofana, 1, Zone.Home);
        Add(s3, "training-farm", 0, Zone.Hand); Add(s3, "training-farm", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "training-slots-mixed", source3.Id, selections: [Pick("holding", holding3.Id), Pick("personality", bofana3.Id), PickPlayer("player", 0)]));
        Eq(e3.State.PendingChoice?.Type, "forcedDiscard", "Bofana was named by the second card slot");
        var s4 = Table(Phase.Action, actor: 0); s4.PriorityPlayerId = 0;
        var source4 = Add(s4, "training-slots-source", 0, Zone.Home); var holding4 = Add(s4, "training-farm", 1, Zone.Home); var other4 = Add(s4, "training-lion-samurai", 1, Zone.Home); Add(s4, Bofana, 1, Zone.Home);
        Add(s4, "training-farm", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 0, "training-slots-mixed", source4.Id, selections: [Pick("holding", holding4.Id), Pick("personality", other4.Id), PickPlayer("player", 0)]));
        Check(e4.State.PendingChoice is null, "and not named by an action that names other cards");
    }

    // ---- one window per target (Mendon's ruling) ---------------------------------------------------------------------------------------
    // Two Personalities of player 1 (the Defender, holding the opportunity is player 0 the actor): A is Akodo Kakihara (reacts only to an action that
    // targets HIM), B has a Champion of Thunder in his unit (reacts to an action that targets a card in that unit), C has no possible reactor.
    private const string Kakihara = "oracle-l5r-223", Champion = "oracle-l5r-1292";
    private sealed record TwoTargets(MatchState S, CardInstance Actor, CardInstance A, CardInstance B, CardInstance C, CardInstance ChampionCard);
    private static TwoTargets Windows()
    {
        var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0; // player 0 attacks and acts; player 1 defends
        var actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
        var a = Add(s, Kakihara, 1, Zone.Battlefield, province: 0);
        var b = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var champion = Add(s, Champion, 1, Zone.Attached, province: 0, parent: b.Id);
        var c = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return new(s, actor, a, b, c, champion);
    }
    private static GameCommand Two(MatchEngine e, TwoTargets t, string first, string second) =>
        Use(e, 0, "training-slots-two-personalities", t.Actor.Id, selections: [Pick("first", first), Pick("second", second)]);
    private static bool WindowFor(MatchEngine e, string target) => e.State.PendingChoice is { Type: "beforeActionReaction" } w && w.CardId == target;
    private static void Decline(MatchEngine e) => Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "declineReaction" && a.Enabled).Command!);

    private static void OneWindowPerTarget()
    {
        var t = Windows();
        var e = MatchEngine.FromTestState(t.S);
        var option = Enabled(e, 0, "training-slots-two-personalities", t.Actor.Id);
        Check(option.Slots!.Length == 2 && option.Slots.All(x => x.Timing == "up-front"), "two up-front slots");
        // A then B: two windows in slot order, the first about Kakihara, the second about the unit with the Champion.
        Accept(e, Two(e, t, t.A.Id, t.B.Id));
        Check(WindowFor(e, t.A.Id), "the first window is for the first target: " + e.State.PendingChoice?.Prompt);
        Check(!e.State.Cards[t.A.Id].Bowed && !e.State.Cards[t.B.Id].Bowed, "nothing has resolved");
        var offered = e.QueryActions(1).Where(a => a.Type == "useAbility" && a.Enabled).ToArray();
        Check(offered.Length == 1 && offered[0].Command!.AbilityId == "kakihara-force-bonus", "only Kakihara's reaction, for HIS window: " + string.Join(",", offered.Select(o => o.Command!.AbilityId)));
        Check(e.QueryActions(0).All(a => a.Type != "useAbility"), "the actor holds no reaction");
        Decline(e); Decline(e);
        Check(WindowFor(e, t.B.Id), "closing it opens the second target's window: " + e.State.PendingChoice?.Type + " " + e.State.PendingChoice?.CardId);
        var second = e.QueryActions(1).Where(a => a.Type == "useAbility" && a.Enabled).ToArray();
        Check(second.Length > 0 && second.All(a => a.Command!.AbilityId == "champion-of-thunder-negate"), "only the Champion's reaction, and none of Kakihara's (it is about B, not A): " + string.Join(",", second.Select(o => o.Command!.AbilityId)));
        Check(!e.State.Cards[t.A.Id].Bowed, "still nothing resolved");
        Decline(e); Decline(e);
        Check(e.State.PendingChoice is null && e.State.Cards[t.A.Id].Bowed && e.State.Cards[t.B.Id].Bowed, "when the last window closes the action resolves once, on both targets");
        Check(e.State.ReactionWindowTargetId is null && e.State.ReactionWindowRemaining.Count == 0 && e.State.PendingNegatableAbility is null, "and the window state is cleared: no stale target is left for the next action to read");
        Check(e.State.Events.Count(x => x.Type == "reactionWindow" && x.Message.StartsWith("Before-action")) == 2, "exactly two windows opened");
        // Order follows the slots, not the cards: B first, then A.
        var t2 = Windows(); var e2 = MatchEngine.FromTestState(t2.S);
        Accept(e2, Two(e2, t2, t2.B.Id, t2.A.Id));
        Check(WindowFor(e2, t2.B.Id), "slot order: B's window first"); Decline(e2); Decline(e2);
        Check(WindowFor(e2, t2.A.Id), "then A's"); Decline(e2); Decline(e2);
        Check(e2.State.Cards[t2.A.Id].Bowed && e2.State.Cards[t2.B.Id].Bowed, "resolved");
        // A target with no possible reactor opens no window.
        var t3 = Windows(); var e3 = MatchEngine.FromTestState(t3.S);
        Accept(e3, Two(e3, t3, t3.C.Id, t3.B.Id));
        Check(WindowFor(e3, t3.B.Id), "C has no reactor: the first (and only) window is B's"); Decline(e3); Decline(e3);
        Check(e3.State.PendingChoice is null && e3.State.Cards[t3.C.Id].Bowed && e3.State.Cards[t3.B.Id].Bowed, "one window, then resolved");
        Check(e3.State.Events.Count(x => x.Type == "reactionWindow" && x.Message.StartsWith("Before-action")) == 1, "exactly one window opened");
        var t4 = Windows(); var c2 = Add(t4.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var e4 = MatchEngine.FromTestState(t4.S);
        Accept(e4, Two(e4, t4, t4.C.Id, c2.Id));
        Check(e4.State.PendingChoice is null && e4.State.Cards[t4.C.Id].Bowed && e4.State.Cards[c2.Id].Bowed, "no target has a reactor: no window at all, the action resolves at once");
        // A single-target action still opens its one window with the old prompt.
        var t5 = Windows(); Add(t5.S, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(t5.S);
        Accept(e5, Offer(e5, 0, "training-bow-at-battle", t5.S.Cards.Values.First(c => c.DefinitionId == "training-battle-bower").Id, t5.A.Id));
        Eq(e5.State.PendingChoice?.Prompt, "Before this action resolves: either player may use a reaction, or pass."); Eq(e5.State.PendingChoice?.CardId, null);
    }

    private static void PerTargetWindowsAlternate()
    {
        // The active player (0) holds a reaction for B's window only: the first opportunity there is HIS; in A's window player 1 goes first.
        var t = Windows(); t.S.Cards[t.Actor.Id].DefinitionId = "training-slots-source";
        var suppressor = Add(t.S, "training-action-negator", 0, Zone.Home); // reacts to an action targeting a card of ITS controller's: never here (the actor's own)
        var e = MatchEngine.FromTestState(t.S);
        Accept(e, Two(e, t, t.A.Id, t.B.Id));
        Eq(e.State.PriorityPlayerId, 1, "A's window: the active player holds no reaction for A, so the other player has the first opportunity");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled).Command!); // Kakihara reacts
        Eq(e.State.PriorityPlayerId, 0, "a reaction hands the opportunity to the other player"); Check(e.State.PendingChoice?.Type == "beforeActionReaction", "same window");
        Decline(e);
        Eq(e.State.PriorityPlayerId, 1, "and back again"); Decline(e);
        Check(WindowFor(e, t.B.Id), "both passed in succession: B's window");
        Check(e.State.Cards[t.A.Id].Modifiers.Any(m => m.Effect == "kakihara-force-bonus"), "what A's reaction did stands");
        Check(e.QueryActions(1).All(a => a.Command?.AbilityId != "kakihara-force-bonus"), "and is not offered again for B (spent, and not B's)");
        // Negations from two windows compose: a Champion's "all" on B in the second window and Tamoko's-shape ones elsewhere; here the Champion negates the bow on B.
        var champ = e.QueryActions(1).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "champion-of-thunder-negate" && a.Command.TargetId == t.B.Id);
        Accept(e, champ.Command!); Decline(e); Decline(e);
        Check(e.State.PendingChoice is null, "resolved"); Check(e.State.Cards[t.A.Id].Bowed && !e.State.Cards[t.B.Id].Bowed, "A bowed; B's effects negated by the Champion");
        // A whole-action negation skips the windows still to come.
        var t2 = Windows(); Add(t2.S, "training-action-negator", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(t2.S);
        Accept(e2, Two(e2, t2, t2.A.Id, t2.B.Id));
        Check(WindowFor(e2, t2.A.Id), "the first window");
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-action").Command!);
        Decline(e2); Decline(e2);
        Check(e2.State.PendingChoice is null, "negated whole: B's window never opened");
        Check(!e2.State.Cards[t2.A.Id].Bowed && !e2.State.Cards[t2.B.Id].Bowed, "and neither is bowed");
    }

    // ---- per-target windows beside R's "targeted by an action" reactions (#335) ------------------------------------------------------------------
    // Player 0 acts (Battle, province 0) with `training-slots-two-personalities` on two Personalities of player 1's.
    private static string[] OfferedTo(MatchEngine e, int player) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled).Select(a => a.Command!.AbilityId!).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static void TargetedReactionsPerTarget()
    {
        MatchEngine Fresh(out TwoTargets t, out CardInstance guard)
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
            guard = Add(s, "training-merchant-free-guard", 1, Zone.Battlefield, province: 0);
            var kakihara = Add(s, Kakihara, 1, Zone.Battlefield, province: 0);
            t = new(s, actor, guard, kakihara, kakihara, kakihara);
            return MatchEngine.FromTestState(s);
        }
        // (1) Both windows open, each with only its own reactor.
        var e = Fresh(out var t, out var guard);
        Accept(e, Two(e, t, guard.Id, t.B.Id));
        Check(WindowFor(e, guard.Id), "the Merchant's window first (slot order): " + e.State.PendingChoice?.Prompt);
        Check(OfferedTo(e, 1).SequenceEqual(new[] { "training-negate-merchant-free" }), "only the Merchant's negation, none of Kakihara's: " + string.Join(",", OfferedTo(e, 1)));
        Decline(e); Decline(e);
        Check(WindowFor(e, t.B.Id), "Kakihara's window second");
        Check(OfferedTo(e, 1).SequenceEqual(new[] { "kakihara-force-bonus" }), "only Kakihara's reaction now: " + string.Join(",", OfferedTo(e, 1)));
        Decline(e); Decline(e);
        Check(e.State.PendingChoice is null && e.State.Cards[guard.Id].Bowed && e.State.Cards[t.B.Id].Bowed, "resolved on both");
        Check(e.State.ReactionWindowTargetId is null && e.State.ReactionWindowRemaining.Count == 0, "window state cleared");
        // (2) The Merchant's negation of the whole action skips Kakihara's window.
        var e2 = Fresh(out var t2, out var guard2);
        Accept(e2, Two(e2, t2, guard2.Id, t2.B.Id));
        Accept(e2, e2.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-negate-merchant-free").Command!);
        Decline(e2); Decline(e2);
        Check(e2.State.PendingChoice is null, "negated as a whole: Kakihara's window never opened");
        Check(!e2.State.Cards[guard2.Id].Bowed && !e2.State.Cards[t2.B.Id].Bowed, "and nobody was bowed");
        Check(e2.State.Cards[t2.Actor.Id].AbilityUses.ContainsKey("training-slots-two-personalities"), "the action was still taken");
        // (3) A target that is a Merchant only as the SECOND slot still gets its window (the first has no reactor): the window is not the primary target's.
        var e3 = Fresh(out var t3, out var guard3);
        Accept(e3, Two(e3, t3, t3.B.Id, guard3.Id));
        Check(WindowFor(e3, t3.B.Id), "slot order: Kakihara first"); Decline(e3); Decline(e3);
        Check(WindowFor(e3, guard3.Id), "then the Merchant, whose reactor reads the window's target and not the command's first");
        Check(OfferedTo(e3, 1).SequenceEqual(new[] { "training-negate-merchant-free" }), string.Join(",", OfferedTo(e3, 1)));
        Decline(e3); Decline(e3);
        Check(e3.State.PendingChoice is null && e3.State.Cards[guard3.Id].Bowed, "resolved");
        // (4) Sakuma's Political reaction (#338) reads the window's target too: Yoshitada's Political action names an enemy Personality, and Sakuma (in play, his
        // controller's) is offered his reaction for it -- and for nobody else's target.
        var r = Yoshi(dishonorable: false);
        var sakuma = Add(r.S, "oracle-l5r-9624", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(r.S);
        Accept(e4, Yo(e4, r, r.Enemy.Id));
        Check(WindowFor(e4, r.Enemy.Id) || e4.State.PendingChoice?.Type == "beforeActionReaction", "a window opens for the Political action's target: " + e4.State.PendingChoice?.Type);
        Check(OfferedTo(e4, 1).Contains("sakuma-political-negate"), "Sakuma is offered his reaction for the Political action's target: " + string.Join(",", OfferedTo(e4, 1)));
    }

    private static void RedirectInsideASlot()
    {
        MatchEngine Fresh(out CardInstance actor, out CardInstance shugenja, out CardInstance ezuno, out CardInstance other)
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
            shugenja = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
            ezuno = Add(s, "oracle-l5r-5086", 1, Zone.Battlefield, province: 0);
            other = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Fresh(out var actor, out var shugenja, out var ezuno, out var other);
        GameCommand Cmd(string first, string second) => Use(e, 0, "training-slots-two-personalities", actor.Id, selections: [Pick("first", first), Pick("second", second)]);
        Accept(e, Cmd(shugenja.Id, other.Id));
        Check(WindowFor(e, shugenja.Id), "the Shugenja's window: " + e.State.PendingChoice?.Prompt);
        var redirect = e.QueryActions(1).Where(a => a.Type == "useAbility" && a.Command!.AbilityId == "ezuno-redirect").ToArray();
        Check(redirect.Length == 1 && redirect[0].Enabled, "Ezuno's redirect is offered, once: " + string.Join("; ", redirect.Select(a => a.Reason)));
        Accept(e, redirect[0].Command!);
        var held = e.State.PendingNegatableAbility!;
        Check(held.Selections!.Single(x => x.Slot == "first").CardIds!.SequenceEqual(new[] { ezuno.Id }) && held.Selections!.Single(x => x.Slot == "second").CardIds!.SequenceEqual(new[] { other.Id }), "the first slot now holds Ezuno; the second is untouched");
        Eq(held.TargetId, ezuno.Id, "and the primary target follows");
        Eq(e.State.ReactionWindowTargetId, ezuno.Id, "the window is now about Ezuno"); Eq(e.State.PendingChoice?.CardId, ezuno.Id);
        Check(e.State.Cards[ezuno.Id].AbilityUses.ContainsKey("ezuno-redirect"), "Ezuno is spent");
        Decline(e); Decline(e);
        Check(e.State.PendingChoice is null, "no reactor for the second target: the action resolves");
        Check(e.State.Cards[ezuno.Id].Bowed && e.State.Cards[other.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed, "it resolved on Ezuno and the second target, and left the Shugenja alone");
        // Another slot already holds Ezuno: slots are independent, so he may still take the Shugenja's place in his own slot (only a card the SAME slot holds is refused).
        var e2 = Fresh(out var actor2, out var shugenja2, out var ezuno2, out var other2);
        Accept(e2, Use(e2, 0, "training-slots-two-personalities", actor2.Id, selections: [Pick("first", shugenja2.Id), Pick("second", ezuno2.Id)]));
        Check(WindowFor(e2, shugenja2.Id), "the Shugenja's window");
        Check(e2.QueryActions(1).Any(a => a.Type == "useAbility" && a.Command!.AbilityId == "ezuno-redirect" && a.Enabled), "slots are independent: Ezuno may take the Shugenja's place in his own slot");
        // An action at Ezuno himself (the window is Ezuno's own): never offered.
        var e3 = Fresh(out var actor3, out _, out var ezuno3, out var other3);
        Accept(e3, Use(e3, 0, "training-slots-two-personalities", actor3.Id, selections: [Pick("first", ezuno3.Id), Pick("second", other3.Id)]));
        Check(e3.State.PendingChoice is null || !e3.QueryActions(1).Any(a => a.Command?.AbilityId == "ezuno-redirect" && a.Enabled), "an action that targets Ezuno himself is not redirected onto him");
    }

    // ---- negated actions and the costs they declare (#356), out-of-play windows (#367) -------------------------------------------------------------
    private static void NegatedActionsPayWhatTheyDeclare()
    {
        // (1) Bonds of Coin's Favor half: a declared cost (bow the Stronghold) and the Favor discard are paid negated or not; only the draws are its effect.
        foreach (var negated in new[] { false, true })
        {
            var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            var card = Add(s, BondsOfCoin, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s); e.NegateEveryAction = negated;
            var hand = e.State.Players[0].Hand.Count; var deck = e.State.Players[0].FateDeck.Count;
            Accept(e, Enabled(e, 0, "bonds-of-coin-draw", card.Id).Command!);
            Check(e.State.Cards[e.State.Players[0].StrongholdId].Bowed, $"negated={negated}: the Stronghold is bowed either way (a cost)");
            Check(e.State.FavorPlayerId is null, $"negated={negated}: the Imperial Favor is discarded either way");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");
            Eq(e.State.Players[0].Hand.Count, negated ? hand - 1 : hand + 1, "the draws are the effect: not when negated"); Eq(e.State.Players[0].FateDeck.Count, negated ? deck : deck - 2);
        }
        // (2) Atomic: a blocked cost negates the whole action and pays NOTHING -- not the Stronghold bow, not the Favor.
        {
            var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            var card = Add(s, BondsOfCoin, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            e.CostBlockHook = (cost, _) => cost.Verb == CostVerb.Bow;
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, Enabled(e, 0, "bonds-of-coin-draw", card.Id).Command!);
            Check(!e.State.Cards[e.State.Players[0].StrongholdId].Bowed && e.State.FavorPlayerId == 0, "blocked: nothing was paid");
            Eq(e.State.Players[0].Hand.Count, hand - 1, "and nothing was drawn; the Strategy was still played");
        }
        // (3) Yoshitada negated: not bowed, nothing asked, no Honor changed, the action taken.
        {
            var r = Yoshi(dishonorable: true);
            var e = MatchEngine.FromTestState(r.S); e.NegateEveryAction = true;
            var honor = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Yo(e, r, r.Enemy.Id));
            Check(!e.State.Cards[r.Enemy.Id].Bowed && e.State.PendingChoice is null && Honor(e, 0) == honor[0] && Honor(e, 1) == honor[1], "Yoshitada negated: no bow, nothing asked, no Honor");
            Check(e.State.Cards[r.Yoshitada.Id].AbilityUses.ContainsKey(YoshiAbility) && !e.State.Cards[r.Yoshitada.Id].Bowed, "the action was taken and cost him nothing");
        }
        // (4) Fear Me! negated: the Strategy is spent, the target is not moved, nothing asked.
        {
            var s = Table(); s.PriorityPlayerId = 0;
            var yoshi = Add(s, Yoshitada, 0, Zone.Battlefield, province: 0); var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var strategy = Add(s, FearMe, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s); e.NegateEveryAction = true;
            Accept(e, Use(e, 0, FearMeMove, strategy.Id, performerId: yoshi.Id, selections: Pick("target", enemy.Id)));
            Check(e.State.Cards[enemy.Id].Zone == Zone.Battlefield && e.State.PendingChoice is null, "Fear Me! negated: not moved, nothing asked");
            Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was still played"); Check(!e.State.Cards[yoshi.Id].Bowed, "and the performer paid nothing");
        }
        // (5) Bonds of Coin's destroy half negated: nothing is destroyed, the Strategy is played.
        {
            var r = Attachments(BondsOfCoin);
            var e = MatchEngine.FromTestState(r.S); e.NegateEveryAction = true;
            Accept(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", r.Free.Take(2).Select(c => c.Id).ToArray())));
            Check(r.Free.Take(2).All(c => e.State.Cards[c.Id].Zone == Zone.Attached), "negated: nothing destroyed"); Eq(e.State.Cards[r.Strategy.Id].Zone, Zone.FateDiscard);
        }
    }

    private static void OutOfPlayAndPoliticalWindowsPerTarget()
    {
        // (a) Unimpeachable Name (an out-of-play Reaction in hand) is asked ONCE for the multi-target action -- "before the resolution of another player's action" --
        // not once per target; declining it lets the action resolve on both.
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
            var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); a.Dishonorable = true; var b = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "oracle-l5r-8989", 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 0, "training-slots-two-personalities", actor.Id, selections: [Pick("first", a.Id), Pick("second", b.Id)]));
            Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "asked once, for the action");
            for (var i = 0; i < 4 && e.State.PendingChoice?.Type == "unimpeachableNameReaction"; i++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); // the out-of-play window: both pass
            Check(e.State.PendingChoice is null && e.State.Cards[a.Id].Bowed && e.State.Cards[b.Id].Bowed, "declined: the action resolved on both targets, without a second window: " + e.State.PendingChoice?.Type + " " + e.State.Cards[a.Id].Bowed + e.State.Cards[b.Id].Bowed);
            Eq(e.State.Events.Count(x => x.Type == "reactionWindow" && x.Message.StartsWith("Out-of-play")), 1, "one out-of-play window in all");
        }
        // (b) Sakuma (a Political reaction) and Kakihara (his own): Political action on Kakihara (first) and a plain Personality (second). Kakihara's window offers both
        // reactions, the second target's only Sakuma's -- one window per target that has a reactor, each with its own.
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
            var kakihara = Add(s, Kakihara, 1, Zone.Battlefield, province: 0); var plain = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "oracle-l5r-9624", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 0, "training-slots-two-political", actor.Id, selections: [Pick("first", kakihara.Id), Pick("second", plain.Id)]));
            Check(WindowFor(e, kakihara.Id), "Kakihara's window first: " + e.State.PendingChoice?.Prompt);
            Check(OfferedTo(e, 1).SequenceEqual(new[] { "kakihara-force-bonus", "sakuma-political-negate" }), "both reactions for him: " + string.Join(",", OfferedTo(e, 1)));
            Decline(e); Decline(e);
            Check(WindowFor(e, plain.Id), "then the second target's");
            Check(OfferedTo(e, 1).SequenceEqual(new[] { "sakuma-political-negate" }), "only Sakuma's for a plain Personality: " + string.Join(",", OfferedTo(e, 1)));
            Decline(e); Decline(e);
            Check(e.State.PendingChoice is null && e.State.Cards[kakihara.Id].Bowed && e.State.Cards[plain.Id].Bowed, "resolved on both");
            Eq(e.State.Events.Count(x => x.Type == "reactionWindow" && x.Message.StartsWith("Before-action")), 2, "exactly two windows, one per target");
        }
        // (c) The non-Political twin: Sakuma is offered nothing, so only Kakihara's window opens.
        {
            var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
            var kakihara = Add(s, Kakihara, 1, Zone.Battlefield, province: 0); var plain = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "oracle-l5r-9624", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Two(e, new(s, actor, kakihara, plain, plain, plain), kakihara.Id, plain.Id));
            Check(WindowFor(e, kakihara.Id) && OfferedTo(e, 1).SequenceEqual(new[] { "kakihara-force-bonus" }), "not Political: Kakihara's own reaction only");
            Decline(e); Decline(e);
            Check(e.State.PendingChoice is null, "and no window for the second target");
        }
    }

    // ---- attachments ----------------------------------------------------------------------------------------------------------
    private sealed record AttachmentRig(MatchState S, CardInstance Strategy, CardInstance[] Free, CardInstance Priced, CardInstance Cut, CardInstance HostOwn, CardInstance HostEnemy);
    // Action Phase, player 0 to act with the Strategy in hand. Attachments costing 0 now: three printed (own Item on his Personality, enemy Follower,
    // enemy Spell) and one taken to 0 by a Gold cost penalty; one that really costs 2; the Personalities themselves and a Holding (cost 0? no: 2).
    private static AttachmentRig Attachments(string strategy, Phase phase = Phase.Action)
    {
        var s = Table(phase, actor: 0); s.PriorityPlayerId = 0;
        var hostOwn = Add(s, "training-crane-samurai", 0, Zone.Home); var hostEnemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var a = Add(s, "training-zero-cost-item", 0, Zone.Attached, parent: hostOwn.Id);
        var b = Add(s, "training-zero-cost-follower", 1, Zone.Attached, parent: hostEnemy.Id);
        var c = Add(s, "training-zero-cost-spell", 1, Zone.Attached, parent: hostEnemy.Id);
        var priced = Add(s, "training-item", 1, Zone.Attached, parent: hostEnemy.Id);
        var cut = Add(s, "training-bow-item", 0, Zone.Attached, parent: hostOwn.Id); // printed 1
        cut.Modifiers.Add(new("test", "gold-cost-penalty", "GoldCost", -1));
        Add(s, "training-farm", 1, Zone.Home);
        var card = Add(s, strategy, 0, Zone.Hand);
        return new(s, card, [a, b, c, cut], priced, cut, hostOwn, hostEnemy);
    }

    private static void BondsOfCoinDestroys()
    {
        var r = Attachments(BondsOfCoin);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, "bonds-of-coin-destroy", r.Strategy.Id);
        var slot = option.Slots!.Single();
        Eq(slot.Slot, "attachments"); Eq(slot.Min, 1); Eq(slot.Max, 3);
        Check(slot.CardIds.ToHashSet().SetEquals(r.Free.Select(c => c.Id)), "exactly the attachments that cost 0 now, both sides', the penalized one included: " + string.Join(",", slot.CardIds));
        Check(!slot.CardIds.Contains(r.Priced.Id) && !slot.CardIds.Contains(r.HostOwn.Id) && !slot.CardIds.Contains(r.HostEnemy.Id), "not a priced attachment, not a Personality");
        Eq(option.Command!.Selections!.Single().CardIds!.Length, 1, "the default is the one card the slot requires");
        foreach (var take in new[] { 1, 2, 3 })
        {
            var fresh = MatchEngine.FromTestState(r.S);
            var chosen = r.Free.Take(take).Select(c => c.Id).ToArray();
            Accept(fresh, Use(fresh, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", chosen)));
            foreach (var c in r.Free) Eq(fresh.State.Cards[c.Id].Zone, chosen.Contains(c.Id) ? Zone.FateDiscard : Zone.Attached, $"destroying {take}: {c.Id}");
            Eq(fresh.State.Cards[r.Priced.Id].Zone, Zone.Attached, "the priced attachment is untouched");
            Eq(fresh.State.Cards[r.Strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            Eq(fresh.State.PriorityPlayerId, 1);
        }
        var all4 = r.Free.Select(c => c.Id).ToArray();
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", all4))); // four
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments"))); // none
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id)); // nothing selected at all
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", all4[0], all4[0]))); // the same card twice
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", all4[0], r.Priced.Id))); // one costs 2
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", r.HostOwn.Id))); // not an attachment
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: PickPlayer("attachments", 1)));
        // The Gold cost is the CURRENT one: without the penalty the same item is not a target, and a target's cost raised above 0 stops being one.
        var s2 = Attachments(BondsOfCoin); s2.Cut.Modifiers.Clear(); s2.Free[0].Modifiers.Add(new("test", "cost-bonus", "GoldCost", 1));
        var e2 = MatchEngine.FromTestState(s2.S);
        var offered = Enabled(e2, 0, "bonds-of-coin-destroy", s2.Strategy.Id).Slots!.Single().CardIds;
        Check(!offered.Contains(s2.Cut.Id) && !offered.Contains(s2.Free[0].Id) && offered.Length == 2, "printed 1 with no penalty, and printed 0 raised to 1, are not zero-cost: " + string.Join(",", offered));
        // One of the destroyed attachments' hosts is not destroyed by it (an attachment leaving is not a Personality's death unless it was his only Chi).
        var fresh2 = MatchEngine.FromTestState(r.S);
        Accept(fresh2, Use(fresh2, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", r.Free[1].Id, r.Free[2].Id)));
        Eq(fresh2.State.Cards[r.HostEnemy.Id].Zone, Zone.Home, "the host of two destroyed attachments is untouched");
        // Political Open: playable by either player in the Action Phase (not only Limited), so the other player holding the opportunity may use it.
        var other = Attachments(BondsOfCoin); other.S.Cards[other.Strategy.Id].OwnerId = 1; other.S.Players[0].Hand.Remove(other.Strategy.Id); other.S.Players[1].Hand.Add(other.Strategy.Id); other.S.PriorityPlayerId = 1;
        Check(Enabled(MatchEngine.FromTestState(other.S), 1, "bonds-of-coin-destroy", other.Strategy.Id) is not null, "the non-active player may use an Open action when he holds the opportunity");
    }

    private static void BondsOfCoinDraws()
    {
        MatchEngine Ready(Action<MatchState>? edit = null)
        {
            var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            var card = Add(s, BondsOfCoin, 0, Zone.Hand); edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        string StrategyId(MatchEngine e) => e.State.Cards.Values.Single(c => c.DefinitionId == BondsOfCoin).Id;
        var e = Ready();
        var handBefore = e.State.Players[0].Hand.Count; var deckBefore = e.State.Players[0].FateDeck.Count;
        var option = Enabled(e, 0, "bonds-of-coin-draw", StrategyId(e));
        Check(option.Gold is null or { Cost: 0 } && option.Slots is null && option.TargetIds!.Length == 0, "a free action with no target: " + option.Gold?.Cost);
        Accept(e, option.Command!);
        Check(e.State.Cards[e.State.Players[0].StrongholdId].Bowed, "the Stronghold is bowed");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor is discarded");
        Eq(e.State.Players[0].Hand.Count, handBefore - 1 + 2, "the Strategy left the hand and two cards were drawn"); Eq(e.State.Players[0].FateDeck.Count, deckBefore - 2);
        Eq(e.State.Players[0].FavorActionsThisTurn, 1, "discarding the Imperial Favor as its cost makes it a Favor action");
        // Refused without the Favor, with a bowed Stronghold, on the opponent's turn -- listed disabled with the reason.
        foreach (var (name, edit) in new (string, Action<MatchState>)[]
        {
            ("without the Imperial Favor", s => s.FavorPlayerId = null),
            ("the other player holds the Imperial Favor", s => s.FavorPlayerId = 1),
            ("with a bowed Stronghold", s => s.Cards[s.Players[0].StrongholdId].Bowed = true),
            ("as Limited: not on the opponent's turn", s => s.ActivePlayerId = 1),
        })
        {
            var refused = Ready(edit);
            var listed = Options(refused, 0, "bonds-of-coin-draw", StrategyId(refused));
            Check(listed.All(o => !o.Enabled), $"{name}: not offered enabled ({string.Join(";", listed.Select(o => o.Reason))})");
            Rejection(refused, Use(refused, 0, "bonds-of-coin-draw", StrategyId(refused)));
        }
    }

    private static void GoldAndSteelDestroys()
    {
        // Open, in the Action Phase: any zero-cost attachment anywhere in play.
        var r = Attachments(GoldAndSteel);
        var e = MatchEngine.FromTestState(r.S);
        var open = Enabled(e, 0, "gold-and-steel-destroy", r.Strategy.Id);
        Check(open.Slots!.Single().CardIds.ToHashSet().SetEquals(r.Free.Select(c => c.Id)), "as an Open action: every zero-cost attachment in play");
        Accept(e, Use(e, 0, "gold-and-steel-destroy", r.Strategy.Id, selections: Pick("attachments", r.Free.Take(3).Select(c => c.Id).ToArray())));
        Check(r.Free.Take(3).All(c => e.State.Cards[c.Id].Zone == Zone.FateDiscard) && e.State.Cards[r.Free[3].Id].Zone == Zone.Attached, "three destroyed");
        // Battle, at the current battlefield: only attachments of units there.
        var s = Table(); s.PriorityPlayerId = 0;
        var here = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-lion-samurai", 1, Zone.Home);
        var near = Add(s, "training-zero-cost-follower", 1, Zone.Attached, province: 0, parent: here.Id);
        var far = Add(s, "training-zero-cost-item", 1, Zone.Attached, parent: elsewhere.Id);
        var strategy = Add(s, GoldAndSteel, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s);
        var battle = Enabled(e2, 0, "gold-and-steel-destroy", strategy.Id);
        Check(battle.Slots!.Single().CardIds.SequenceEqual(new[] { near.Id }), "as a Battle action: only the attachment at the current battlefield: " + string.Join(",", battle.Slots!.Single().CardIds));
        Reject(e2, Use(e2, 0, "gold-and-steel-destroy", strategy.Id, selections: Pick("attachments", far.Id)));
        Accept(e2, Use(e2, 0, "gold-and-steel-destroy", strategy.Id, selections: Pick("attachments", near.Id)));
        Eq(e2.State.Cards[near.Id].Zone, Zone.FateDiscard); Eq(e2.State.Cards[far.Id].Zone, Zone.Attached);
    }

    private static void GoldAndSteelBows()
    {
        // Costs: the Force 7 bower 6, the Lion samurai 5, the Crane samurai 5, the Battle bower 3.
        var s = Table(); s.PriorityPlayerId = 0;
        var big = Add(s, "training-force-seven-bower", 0, Zone.Battlefield, province: 0); var small = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var five = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var six = Add(s, "training-force-seven-bower", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, GoldAndSteel, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var byBig = Enabled(e, 0, "gold-and-steel-bow", strategy.Id, big.Id).TargetIds!; var bySmall = Options(e, 0, "gold-and-steel-bow", strategy.Id, small.Id).Single();
        Check(byBig.ToHashSet().SetEquals([five.Id, six.Id]), "a cost-6 performer may bow costs 5 and 6 (equal or lower): " + string.Join(",", byBig));
        Check(!bySmall.Enabled, "a cost-3 performer has nothing at or under 3: " + bySmall.Reason);
        Reject(e, Use(e, 0, "gold-and-steel-bow", strategy.Id, five.Id, small.Id));
        // A Gold cost penalty on the target brings it under the performer's: the CURRENT cost counts.
        var s2 = Table(); s2.PriorityPlayerId = 0;
        var small2 = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 0); var five2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        five2.Modifiers.Add(new("test", "penalty", "GoldCost", -2));
        var strategy2 = Add(s2, GoldAndSteel, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "gold-and-steel-bow", strategy2.Id, five2.Id, small2.Id));
        Check(e2.State.Cards[five2.Id].Bowed && e2.State.Cards[five2.Id].Zone == Zone.Battlefield, "bowed, not moved (the plain ability)");
        // The performer must be unbowed and at the battlefield.
        var s3 = Table(); s3.PriorityPlayerId = 0;
        var bowedPerformer = Add(s3, "training-force-seven-bower", 0, Zone.Battlefield, province: 0, bowed: true); Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy3 = Add(s3, GoldAndSteel, 0, Zone.Hand);
        Check(Options(MatchEngine.FromTestState(s3), 0, "gold-and-steel-bow", strategy3.Id).All(o => !o.Enabled), "a bowed performer is not offered");
        // "You may move him home": the second ability moves him and shares the use.
        var s4 = Table(); s4.PriorityPlayerId = 0;
        var perf = Add(s4, "training-force-seven-bower", 0, Zone.Battlefield, province: 0); var target = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy4 = Add(s4, GoldAndSteel, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "gold-and-steel-bow-move", strategy4.Id, target.Id, perf.Id));
        Check(e4.State.Cards[target.Id].Bowed && e4.State.Cards[target.Id].Zone == Zone.Home, "bowed and moved home");
        Check(e4.State.Cards[strategy4.Id].AbilityUses.ContainsKey("gold-and-steel-bow"), "one shared use");
        Check(Options(MatchEngine.FromTestState(e4.State), 1, "gold-and-steel-bow", strategy4.Id).All(o => !o.Enabled), "and nothing more of that action");
    }

    private static void FearMeMoves()
    {
        MatchEngine Battle(Action<MatchState>? edit = null, string enemy = "training-lion-samurai")
        {
            var s = Table(); s.PriorityPlayerId = 0;
            Add(s, Yoshitada, 0, Zone.Battlefield, province: 0);
            Add(s, enemy, 1, Zone.Battlefield, province: 0);
            Add(s, FearMe, 0, Zone.Hand);
            edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        string Id(MatchEngine e, string def, int owner) => e.State.Cards.Values.First(c => c.Id.StartsWith("fx-") && c.DefinitionId == def && c.OwnerId == owner).Id;
        GameCommand Play(MatchEngine e) => Use(e, 0, FearMeMove, Id(e, FearMe, 0), performerId: Id(e, Yoshitada, 0), selections: Pick("target", Id(e, "training-lion-samurai", 1)));
        foreach (var (choice, player, delta) in new[] { ("self-gain", 0, 2), ("opponent-gain", 1, 2), ("self-lose", 0, -2), ("opponent-lose", 1, -2) })
        {
            var e = Battle();
            var strategy = Id(e, FearMe, 0); var yoshi = Id(e, Yoshitada, 0); var enemy = Id(e, "training-lion-samurai", 1);
            var option = Enabled(e, 0, FearMeMove, strategy, yoshi);
            Check(option.Slots![0].CardIds.SequenceEqual(new[] { enemy }) && option.Slots[1].Timing == "at-resolution" && option.Slots[1].Condition == "target-moved", "an enemy Personality up front; the player is a follow-up asked only if he moved");
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Play(e));
            Eq(e.State.Cards[enemy].Zone, Zone.Home, "moved home"); Check(!e.State.Cards[enemy].Bowed, "Fear Me! does not bow him");
            Check(Asked(e) && Honor(e, 0) == before[0] && Honor(e, 1) == before[1], "he moved, so the player is asked, and nothing changed yet");
            Check(OptionIds(Answers(e, 0)).SequenceEqual(HonorAnswers), "the same four answers");
            Answer(e, 0, choice);
            Eq(Honor(e, player), before[player] + delta, $"{choice}"); Eq(Honor(e, 1 - player), before[1 - player]);
            Eq(e.State.Cards[strategy].Zone, Zone.FateDiscard, "the Strategy is spent when the action completes"); Eq(e.State.PriorityPlayerId, 1);
        }
        {
            // "If he moved": a negated move (Tamori Tamoko's negation) asks nothing and changes nobody's Honor.
            var e = Battle(s => Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0));
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Play(e));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction");
            Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "tamoko-negate").Command!);
            Decline(e); Decline(e);
            var enemy = Id(e, "training-lion-samurai", 1);
            Eq(e.State.Cards[enemy].Zone, Zone.Battlefield, "his movement was negated");
            Check(!Asked(e) && e.State.PendingChoice is null, "so nothing is asked"); Check(Honor(e, 0) == before[0] && Honor(e, 1) == before[1], "and nobody's Honor changed");
            Eq(e.State.Cards[Id(e, FearMe, 0)].Zone, Zone.FateDiscard, "the action still completed");
        }
        {
            // Equal Chi is allowed and a higher Chi is not (see CurrentChiFlipsTheResult for modifiers).
            var high = Battle(enemy: "training-chi-five-personality");
            Check(Options(high, 0, FearMeMove, Id(high, FearMe, 0)).All(o => !o.Enabled), "a Chi 5 target is out of reach of a Chi 4 Duelist");
            var equal = Battle(enemy: "training-force-seven-bower");
            Check(CanUse(equal, 0, FearMeMove, Id(equal, FearMe, 0)), "Chi 4 against Chi 4 is allowed");
        }
        {
            // The performer must be an unbowed Duelist at the battlefield.
            var notDuelist = Battle(s => { s.Cards.Values.Single(c => c.DefinitionId == Yoshitada).DefinitionId = "training-crane-samurai"; });
            Check(Options(notDuelist, 0, FearMeMove, Id(notDuelist, FearMe, 0)).All(o => !o.Enabled), "no Duelist, no action");
            var bowed = Battle(s => s.Cards.Values.Single(c => c.DefinitionId == Yoshitada).Bowed = true);
            Check(Options(bowed, 0, FearMeMove, Id(bowed, FearMe, 0)).All(o => !o.Enabled), "a bowed Duelist does not perform");
        }
        {
            // A gain for the performing Duelist's own controller while he is dishonorable rehonors HIM (the Duelist is the performer, not the Strategy).
            var e = Battle(s => s.Cards.Values.Single(c => c.DefinitionId == Yoshitada).Dishonorable = true);
            var honor = Honor(e, 0);
            Accept(e, Play(e)); Answer(e, 0, "self-gain");
            Eq(Honor(e, 0), honor); Check(!e.State.Cards[Id(e, Yoshitada, 0)].Dishonorable, "the dishonorable Duelist is rehonored instead");
        }
    }

    // "Equal or lower Chi" reads CURRENT Chi: printed plus modifiers (plus Items, PersonalityChi's own reading, as personality-chi-lte-self already does)
    // -- Mendon's ruling, issue #336. The performer and the target both have printed Chi 3; each modifier below flips whether the target is in reach.
    private static void CurrentChiFlipsTheResult()
    {
        foreach (var shape in new[] { "Fear Me!", "Chokehold", "Ring of Fire" })
        {
            bool InReach(Action<CardInstance, CardInstance> change)
            {
                var s = Table(); s.PriorityPlayerId = 0;
                var performer = Add(s, "training-duelist-personality", 0, Zone.Battlefield, province: 0); // Duelist, Chi 3
                performer.GrantedKeywords.Add(new("Monk", "test", "Turn"));
                var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
                var (ability, card) = shape switch
                {
                    "Fear Me!" => (FearMeMove, Add(s, FearMe, 0, Zone.Hand).Id),
                    "Chokehold" => ("chokehold", Add(s, "oracle-l5r-1336", 0, Zone.Hand).Id),
                    _ => ("ring-of-fire-destroy", Add(s, "oracle-l5r-6335", 0, Zone.Home).Id),
                };
                change(performer, target);
                var e = MatchEngine.FromTestState(s);
                return Options(e, 0, ability, card).Any(o => o.Enabled && (o.Slots?[0].CardIds ?? o.TargetIds)!.Contains(target.Id));
            }
            void Chi(CardInstance c, int amount) => c.Modifiers.Add(new("test", "chi", "Chi", amount));
            Check(InReach((p, t) => { }), $"{shape}: equal printed Chi (3 against 3) is in reach");
            Check(!InReach((p, t) => Chi(t, 1)), $"{shape}: +1 Chi on the target puts it out of reach (printed Chi alone would still allow it)");
            Check(InReach((p, t) => Chi(p, 1)), $"{shape}: +1 Chi on the performer keeps it in reach");
            Check(InReach((p, t) => { Chi(t, 1); Chi(p, 1); }), $"{shape}: +1 on both is equal again");
            Check(!InReach((p, t) => Chi(p, -1)), $"{shape}: -1 Chi on the performer leaves a Chi 3 target out of reach (printed Chi alone would still allow it)");
            Check(InReach((p, t) => Chi(t, -1)), $"{shape}: -1 Chi on the target keeps it in reach");
        }
    }

    // ---- the general shapes -------------------------------------------------------------------------------------------------------
    private static void MixedSlots()
    {
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home);
        var ownHolding = Add(s, "training-farm", 0, Zone.Home); var enemyHolding = Add(s, "training-marketplace", 1, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home); var own = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-slots-mixed", source.Id);
        var slots = option.Slots!;
        Check(slots.Select(x => x.Slot).SequenceEqual(new[] { "holding", "personality", "player" }) && slots.Select(x => x.Kind).SequenceEqual(new[] { "card", "card", "player" }), "three independent slots of three kinds");
        Check(slots[0].CardIds.Contains(ownHolding.Id) && slots[0].CardIds.Contains(enemyHolding.Id) && slots[0].CardIds.All(id => e.State.Cards[id].Zone == Zone.Home && CardCatalog.Get(e.State.Cards[id].DefinitionId).Type == CardType.Holding), "the Holding slot lists Holdings of either side and nothing but Holdings: " + string.Join(",", slots[0].CardIds));
        Check(slots[1].CardIds.SequenceEqual(new[] { enemy.Id }), "the Personality slot lists the enemy Personality only");
        var honor = Honor(e, 0);
        GameCommand Mixed(string holding, string personality, int player) => Use(e, 0, "training-slots-mixed", source.Id, selections: [Pick("holding", holding), Pick("personality", personality), PickPlayer("player", player)]);
        // Each card type in the wrong slot is refused, whatever the other slots hold.
        Reject(e, Mixed(enemy.Id, enemy.Id, 0)); Reject(e, Mixed(ownHolding.Id, ownHolding.Id, 0)); Reject(e, Mixed(ownHolding.Id, own.Id, 0));
        Reject(e, Mixed(follower.Id, enemy.Id, 0)); Reject(e, Mixed(ownHolding.Id, follower.Id, 0)); Reject(e, Mixed(ownHolding.Id, enemy.Id, 3));
        Reject(e, Use(e, 0, "training-slots-mixed", source.Id, selections: [Pick("holding", ownHolding.Id), PickPlayer("player", 0)]));
        Reject(e, Use(e, 0, "training-slots-mixed", source.Id, selections: [Pick("holding", ownHolding.Id), Pick("personality", enemy.Id)])); // an up-front player slot is required too
        Accept(e, Mixed(enemyHolding.Id, enemy.Id, 1));
        Check(e.State.Cards[enemyHolding.Id].Bowed && e.State.Cards[enemy.Id].Bowed && !e.State.Cards[ownHolding.Id].Bowed, "each card was affected in its own role");
        Eq(Honor(e, 1), 7, "the chosen player gained 1 (6 + 1)"); Eq(Honor(e, 0), honor);
    }

    private static void UpToThreeIncludingNone()
    {
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home); var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var items = Enumerable.Range(0, 4).Select(_ => Add(s, "training-item", 0, Zone.Attached, parent: host.Id)).ToArray();
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-slots-up-to-three", source.Id);
        Eq(option.Slots!.Single().Min, 0); Eq(option.Slots!.Single().Max, 3); Eq(option.Slots!.Single().CardIds.Length, 4);
        Check(option.Command!.Selections is { Length: 0 }, "the default takes nothing: a slot that may be empty is left empty");
        Reject(e, Use(e, 0, "training-slots-up-to-three", source.Id, selections: Pick("attachments", items.Select(i => i.Id).ToArray())));
        var three = MatchEngine.FromTestState(s);
        Accept(three, Use(three, 0, "training-slots-up-to-three", source.Id, selections: Pick("attachments", items.Take(3).Select(i => i.Id).ToArray())));
        Eq(three.State.Cards.Values.Count(c => c.Zone == Zone.Attached && c.AttachedTo == host.Id), 1, "three destroyed, one left");
        var none = MatchEngine.FromTestState(s);
        Accept(none, option.Command! with { ExpectedVersion = none.Version });
        Eq(none.State.Cards.Values.Count(c => c.Zone == Zone.Attached && c.AttachedTo == host.Id), 4, "the empty selection resolves and destroys nothing");
    }

    private static void HandSlotIsPrivate()
    {
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home);
        var mine = new[] { Add(s, "training-farm", 0, Zone.Hand), Add(s, "training-item", 0, Zone.Hand) };
        var theirs = new[] { Add(s, "training-marketplace", 1, Zone.Hand), Add(s, "training-follower", 1, Zone.Hand) };
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-slots-discard", source.Id);
        Check(option.Slots!.Single().CardIds.ToHashSet().SetEquals(mine.Select(c => c.Id)), "only the actor's own hand is offered");
        HiddenIdsAbsent(e.GetView(0), theirs.Select(c => c.Id));
        var json = JsonSerializer.Serialize(e.QueryActions(0));
        foreach (var c in theirs) Check(!json.Contains(JsonSerializer.Serialize(c.Id)), $"the opponent's hand card {c.Id} is not in the actor's options");
        Reject(e, Use(e, 0, "training-slots-discard", source.Id, selections: Pick("card", theirs[0].Id)));
        Accept(e, Use(e, 0, "training-slots-discard", source.Id, selections: Pick("card", mine[0].Id)));
        Eq(e.State.Cards[mine[0].Id].Zone, Zone.FateDiscard);
    }

    private static void SelectionsSurviveJson()
    {
        var r = Yoshi();
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, YoshiAbility, r.Yoshitada.Id);
        var back = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(option))!;
        Check(back.Slots!.Length == 2 && back.Slots[0].CardIds.SequenceEqual(option.Slots![0].CardIds), "the option's slots survive");
        Check(back.Slots[1].Timing == "at-resolution" && back.Slots[1].Condition == "target-dishonorable" && back.Slots[1].OptionLabels!.SequenceEqual(option.Slots![1].OptionLabels!), "and the follow-up's timing, condition and answers");
        Check(back.Command!.Selections!.Length == 1 && back.Command.Selections[0].Slot == "target", "and so does its default command");
        var again = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(Yo(e, r, r.Enemy.Id)))!;
        Accept(e, again with { ExpectedVersion = e.Version });
        var answer = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(Answers(e, 0).Single(a => a.Command!.Choice == "opponent-lose")))!;
        Accept(e, answer.Command! with { ExpectedVersion = e.Version });
        Eq(Honor(e, 1), 4, "a command and an answer that went through JSON resolve as sent (6 - 2)");
    }
}
