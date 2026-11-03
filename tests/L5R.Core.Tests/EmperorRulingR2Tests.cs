using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition ruling pass R2 (issue #492): Mendon's ruling on the shared DELAY mechanism (src/L5R.Core/EmperorRulingR2.cs) -- an effect-recording layer (each effect an action or a duel's outcome
// applies to a card is recorded as a deferred effect keyed by the card), a store that survives phases, and appliers at each trigger point (end of battle, end of phase, end of turn, the next Dynasty
// Phase) that apply the effects in the order they were applied and negate what can no longer be applied -- and the cards built on it: Halls of the Forgotten (stronghold-wave-20), The Honor of the
// Lion, The Loyalty of the Scorpion, The Compassion of the Unicorn, The Courage of the Mantis and The Honesty of the Phoenix (strategy-wave-63), plus The Strength of the Spider's Focus Effect
// (strategy-wave-13). Every position is built explicitly and driven only through QueryActions and the commands it offers (the two store-level tests use the engine's test seams).
internal static class EmperorRulingR2Tests
{
    private const string Halls = "oracle-l5r-2950", Compassion = "oracle-l5r-7991", Courage = "oracle-l5r-7995", Honesty = "oracle-l5r-8137", LionHonor = "oracle-l5r-8138", Loyalty = "oracle-l5r-8207",
        SpiderStrength = "oracle-l5r-8362", Ichizo = "oracle-l5r-5104";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardInstance StrongholdOf(MatchState s, int player, string definition) { var card = s.Cards[s.Players[player].StrongholdId]; card.DefinitionId = definition; return card; }
    private static string DefinitionWith(string ability) => CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains(ability) == true).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();

    public static void Run(TestRunner runner) => runner.Run("emperor-ruling-R2", [
        ("Published shape: Halls of the Forgotten (stronghold-wave-20) and the five delay Strategies (strategy-wave-63) print their scans' stats, text and ability ids, ship their art and are in their manifests; The Strength of the Spider carries its Focus Effect", PublishedShape),
        ("Halls of the Forgotten: another player's Battle action that targets your Berserker is delayed -- a destruction is recorded, not applied, and is applied from where he stands once the battle has ended", HallsDelaysDestruction),
        ("Halls of the Forgotten (Mendon's example): a Berserker who attacked and was targeted by Block Supply Lines has his bowing and movement recorded in order; after the battle his bow applies and his move home is negated -- he is home already", HallsAttackerAlreadyHome),
        ("Halls of the Forgotten: a defending Berserker's delayed move home is applied when the battle ends, since he is still at the battlefield", HallsDefenderMovedHome),
        ("Halls of the Forgotten: a delayed Ranged Attack's hit is delayed too; only effects ON him are delayed (the acting player's own costs happen now), and only for the held action", HallsAttackAndLimits),
        ("Halls of the Forgotten: not offered against a non-Berserker, another player's Berserker, your own controller's action, or a non-Battle action", HallsNotOffered),
        ("An action that will not be negated or delayed (Yoritomo Hiromi's Battle action) is not delayed even when Halls' Reaction was used", HiromiNotDelayed),
        ("Journey's End Keep's printed \"this will not be negated or delayed\": its Stronghold action's movement is never delayed by Halls' Reaction (the Strategy that shares its ability id promises nothing)", JourneysEndKeepNotDelayed),
        ("The store applies effects in the order they were applied: dishonor then destroy costs the Honor of a dishonorable death, destroy then dishonor does not (the dishonoring is negated: he is gone); several effects stack on one card", DelayedOrdering),
        ("An effect that cannot be applied any more is negated: no move home from home, no move to a battlefield from home, nothing for a card that left play; destroy, dishonor, bow, straighten and modifiers apply from any location", CannotApplyRule),
        ("Trigger points: end of battle, end of phase, end of turn, the next Dynasty Phase and the End Phase each apply only their own effects, and the store survives the phases between", TriggerPoints),
        ("The delay Focus Effects (The Honor of the Lion, The Loyalty of the Scorpion, The Compassion of the Unicorn, The Courage of the Mantis, The Honesty of the Phoenix, The Strength of the Spider): the player whose Personality lost is asked, before the outcome applies, whether to delay; a yes records the loss's effects until the next Dynasty Phase begins, a no applies them now", DuelLossDelayChoice),
        ("The duel's delay: only the loser's controller is asked, a winner with a delay Focus Effect is not, and the delay covers the effects on the loser's unit only -- the winner's own consequences apply as ever", DuelLossDelayScope),
        ("The duel's delay stacks and orders: Reckless Duel's bow and move home are recorded in order, applied as the next Dynasty Phase begins -- the bow applies, the move home is negated because he is home by then", DuelLossDelayOrdering),
        ("The Honor of the Lion: its unbowed honorable Lion Clan performer permanently gets a Personal Honor bonus up to 4 (none if he has 4 already) and the Battle ability to bow an enemy Personality with equal or lower Personal Honor and gain 2 Honor -- as a Battle or an Open action; not a bowed, dishonorable or non-Lion performer", HonorOfTheLion),
        ("The Loyalty of the Scorpion: after an action targets your Scorpion Clan Personality, destroying your performing Scorpion Clan Personality negates the bowing, the movement or the destruction (one of them) of the target -- only for an action that could inflict it, and only for a Scorpion target", LoyaltyOfTheScorpion),
        ("The Loyalty of the Scorpion negates a bowing that Halls of the Forgotten would have delayed, and a delayed effect still records what the negation left", LoyaltyAndHalls),
        ("The Compassion of the Unicorn: its unbowed Unicorn Clan performer straightens another unit (its Followers too) -- 1 Honor for one of his controller's units, 2 for another player's; Battle at the battlefield or Open anywhere; not himself, not a bowed or non-Unicorn performer", CompassionOfTheUnicorn),
        ("The Honesty of the Phoenix: the acting player names a Strategy or Ring; after the next resolution this turn of his action from another Strategy or Ring with that title he draws a card and chooses a player who discards -- another title does not trigger it", HonestyOfThePhoenix),
        ("The Courage of the Mantis: with enemy units at the battlefield, its unbowed Mantis Clan performer moves there from home (an Absent action) and its player may take an additional Battle action -- not without enemy units, not a bowed or non-Mantis performer", CourageMove),
        ("Existing delays run through the store: Spirit of the Berserker's, In the Heart of Battle's and Deadly Orders' destructions are delayed effects (end of turn / end of battle), applied in order", ExistingDelaysUseTheStore),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Strategy(string id, string name, int focus, string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Strategy && c.Name == name && !c.ScenarioOnly && c.GoldCost == 0 && c.Focus == focus, $"{name}: type={c.Type} focus={c.Focus} cost={c.GoldCost}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{name}'s abilities");
            Check(c.Text.StartsWith("As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins."), $"{name}'s Focus Effect text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Strategy(Compassion, "The Compassion of the Unicorn", 3, ["compassion-unicorn-focus", "compassion-unicorn-straighten"]);
        Strategy(Courage, "The Courage of the Mantis", 1, ["courage-mantis-focus", "courage-mantis-move"]);
        Strategy(Honesty, "The Honesty of the Phoenix", 3, ["honesty-phoenix-focus", "honesty-phoenix-name"]);
        Strategy(LionHonor, "The Honor of the Lion", 3, ["honor-lion-focus", "honor-lion-boost"]);
        Strategy(Loyalty, "The Loyalty of the Scorpion", 2, ["loyalty-scorpion-focus", "loyalty-scorpion-bow", "loyalty-scorpion-move", "loyalty-scorpion-destroy"]);
        Strategy(SpiderStrength, "The Strength of the Spider", 1, ["strength-of-the-spider", "strength-spider-focus"]);
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-63.txt"));
        foreach (var id in new[] { Compassion, Courage, Honesty, LionHonor, Loyalty }) Check(listed.Contains(id), $"{id} is in the strategy-wave-63 manifest");
        Eq(PublishedContent.StrategyWave63.Cards.Length, 5, "strategy-wave-63 holds exactly the five wired Strategies");
        Eq(CardCatalog.Get(Honesty).Text, "As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins.\nBattle/Open: If you are a Phoenix Clan player, choose your performing unbowed Phoenix Clan Personality: Name a card. After the next resolution this turn of your action from another Strategy or Ring with that title, draw a card, and choose a player, who discards a card. Remove this card from the game.", "The Honesty of the Phoenix prints the corrected scan's text (issue #494)");
        Check(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-63-sources.json")).Contains("image_sha256"), "the source receipts record the scans' sha256");

        var halls = CardCatalog.Get(Halls);
        Check(halls.Type == CardType.Stronghold && halls.Name == "Halls of the Forgotten" && halls.Clan == "Crab" && halls.GoldProduction == 4 && halls.ProvinceStrength == 8 && halls.StartingHonor == 3 && halls.GoldCost == 0,
            $"Halls of the Forgotten: production={halls.GoldProduction} strength={halls.ProvinceStrength} honor={halls.StartingHonor}");
        Eq(string.Join(",", halls.Abilities ?? []), "halls-forgotten-delay");
        Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/stronghold-wave-20.txt")).Contains(Halls), "Halls is in the stronghold-wave-20 manifest");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Halls + ".jpg")), "Halls' art is shipped");
        Eq(PublishedContent.StrongholdWave20.Cards.Length, 1, "stronghold-wave-20 holds exactly Halls of the Forgotten");
        Eq(AbilityCatalog.All["halls-forgotten-delay"].Timing, "Reaction"); Eq(AbilityCatalog.All["halls-forgotten-delay"].Trigger, ReactionTriggers.TargetedByAction);
    }

    // ---- Halls of the Forgotten -----------------------------------------------------------------------------------------------------------------------------

    // Player 1 attacks province 0 and acts; player 0 (the Defender) owns Halls of the Forgotten and a Berserker at the battlefield. Player 1's Personality has the Battle action given by `actorDef`.
    private static (MatchState S, CardInstance Halls, CardInstance Berserker, CardInstance Actor) HallsRig(string actorDef, bool berserkerDefends = true)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var halls = StrongholdOf(s, 0, Halls);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0);
        var actor = Add(s, actorDef, 1, Zone.Battlefield, province: 0);
        return (s, halls, berserker, actor);
    }

    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }

    private static bool ReactionOffered(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "beforeActionReaction" or "attackReaction" }, "the window closed");
    }

    // Both players pass in the Combat Segment until the battle at province 0 has been resolved (and its aftermath applied).
    private static void FinishBattle(MatchEngine e) => RunUntil(e, () => e.State.Events.Any(x => x.Type == "battleResolved"));

    private static void HallsDelaysDestruction()
    {
        var (s, halls, berserker, destroyer) = HallsRig("training-no-attachments-destroyer");
        Force(berserker, 10); // the Berserker's army wins the battle: it is the delayed destruction that kills him, not the resolution
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, berserker.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the Berserker's controller");
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Battlefield, "he is not destroyed when the action resolves");
        Eq(e.State.DelayedEffects.Count, 1, "one effect is recorded");
        var recorded = e.State.DelayedEffects.Single();
        Check(recorded.Kind == DelayedKinds.Destroy && recorded.CardId == berserker.Id && recorded.Until == DelayUntil.EndOfBattle, $"a destruction of the Berserker waiting for the end of the battle: {recorded.Kind} {recorded.Until}");
        Check(e.State.Events.Any(x => x.Type == "destructionDelayed"), "the delay is logged");
        Check(e.State.Cards[halls.Id].AbilityUses.Count > 0 || !CanUse(e, 0, "halls-forgotten-delay", halls.Id), "Halls' Reaction was used");
        // The battle goes on and is resolved: the Berserker's army wins. Then, and only then, he is destroyed -- from where he stands.
        FinishBattle(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Dead, "destroyed once the battle has ended");
        Check(e.State.DelayedEffects.Count == 0, "the delay is spent");
        var log = e.State.Events.Select(x => x.Type).ToList();
        Check(log.IndexOf("battleResolved") < log.IndexOf("delayedDestruction"), "after the battle's resolution, not before");
        Check(e.State.Cards[destroyer.Id].Zone == Zone.Dead, "the attacker's own army was destroyed by the battle, as ever");
    }

    private static void HallsAttackerAlreadyHome()
    {
        // Player 0 owns Halls and ATTACKS with his Berserker; player 1 (the Defender) plays Block Supply Lines on him: "Move an attacking unit home bowed".
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1;
        var halls = StrongholdOf(s, 0, Halls);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0); Force(berserker, 10);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var blockSupply = Add(s, DefinitionWith("block-supply"), 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "block-supply", blockSupply.Id, berserker.Id));
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Check(e.State.Cards[berserker.Id].Zone == Zone.Battlefield && !e.State.Cards[berserker.Id].Bowed, "neither the bowing nor the movement has happened");
        Eq(string.Join(",", e.State.DelayedEffects.OrderBy(x => x.Seq).Select(x => x.Kind)), DelayedKinds.MoveHome, "the move home is recorded (Block Supply Lines bows his unit \"as he moves\": the bow is part of the move, ruling R5, so only the move is recorded)");
        Check(e.State.DelayedEffects.All(x => x.Until == DelayUntil.EndOfBattle && x.CardId == berserker.Id), "all wait for the end of the battle");
        // The battle: his army wins; the attacking units bow and return home as the battle ends -- the delayed move home then has nowhere to send him from.
        FinishBattle(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Home, "he went home with the battle's own aftermath");
        Check(e.State.Cards[berserker.Id].Bowed, "and is bowed (the attackers' own bow at the battle's end; the action's bow belonged to the move that never happened)");
        Check(e.State.Events.Any(x => x.Type == "delayedEffectNegated" && x.Message.Contains("moving home") && x.Message.Contains("already home")), "the move home is negated: he is home already");
        Check(e.State.DelayedEffects.Count == 0, "the store is empty");
    }

    private static void HallsDefenderMovedHome()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var halls = StrongholdOf(s, 0, Halls);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0); Force(berserker, 10);
        var actor = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var incapacitated = Add(s, DefinitionWith("incapacitated"), 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "incapacitated", incapacitated.Id, berserker.Id));
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Battlefield, "he is not moved home by the action");
        Eq(e.State.DelayedEffects.Single().Kind, DelayedKinds.MoveHome, "the move home is recorded");
        FinishBattle(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Home, "the defender was still at the battlefield when the battle ended: the delayed move home applied");
        Check(e.State.Events.Any(x => x.Type == "delayedEffectApplied" && x.Message.Contains("moved home, as delayed")), "logged as applied");
        Check(e.State.Cards[actor.Id].Zone != Zone.Battlefield, "the attacker is unaffected");
    }

    private static void HallsAttackAndLimits()
    {
        // A Ranged Attack: the hit's destruction is delayed too. The Berserker (Force 2) is a legal target of the Ranged 4 Attack and would be destroyed.
        var (s, halls, berserker, _) = HallsRig("training-battle-bower");
        var holder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        holder.GrantedAbilities.Add(new("spirit-scout-attack", "test-spirit", GrantDuration.Permanent));
        GiveKeyword(holder, "Scout");
        Force(berserker, 10); Force(berserker, -10); // Force 2
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "spirit-scout-attack", holder.Id).Command! with { TargetId = berserker.Id });
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack's window opens");
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Battlefield, "the Attack did not destroy him yet");
        Check(e.State.DelayedEffects.Any(x => x.Kind == DelayedKinds.Destroy && x.CardId == berserker.Id && x.Until == DelayUntil.EndOfBattle), "the hit is recorded as a delayed destruction");
        // A later action of the same player at the same target is not delayed (the Reaction was for the held action only): the bower's bow applies at once.
        var bower = s.Cards.Values.First(c => c.OwnerId == 1 && c.DefinitionId == "training-battle-bower");
        RunUntil(e, () => e.State.PriorityPlayerId == 1 && e.State.PendingChoice is null && e.State.PendingAttack is null);
        if (CanUse(e, 1, "training-bow-at-battle", bower.Id))
        {
            Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, berserker.Id));
            ClosePassing(e);
            Check(e.State.Cards[berserker.Id].Bowed, "a second action at him is not delayed: the delay was for the held action only");
        }
    }

    private static void HallsNotOffered()
    {
        // Not a Berserker.
        var (s, halls, _, destroyer) = HallsRig("training-no-attachments-destroyer");
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, plain.Id));
        Check(!ReactionOffered(e, 0, "halls-forgotten-delay"), "an action that targets a Personality who is not a Berserker gives Halls nothing to delay");
        ClosePassing(e);
        // A Berserker of the other player's: Halls' controller's own Reaction is for his own Berserker.
        var (s2, halls2, _, _) = HallsRig("training-no-attachments-destroyer");
        var own = Add(s2, "training-berserker", 1, Zone.Battlefield, province: 0);
        var actor2 = Add(s2, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        s2.ActivePlayerId = 0; s2.PriorityPlayerId = 0;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "training-destroy-no-attachments", actor2.Id, own.Id));
        Check(!ReactionOffered(e2, 0, "halls-forgotten-delay") && !ReactionOffered(e2, 1, "halls-forgotten-delay"), "another player's Berserker, or an action of Halls' controller, is not answered by it");
        ClosePassing(e2);
        // Halls' controller's own action at his own Berserker (the same player): not "another player's" action.
        var (s3, halls3, berserker3, _) = HallsRig("training-no-attachments-destroyer");
        var mine = Add(s3, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        s3.ActivePlayerId = 0; s3.PriorityPlayerId = 0;
        var e3 = MatchEngine.FromTestState(s3);
        var own3 = e3.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "training-destroy-no-attachments" && a.Enabled && (a.TargetIds?.Contains(berserker3.Id) ?? false)).ToArray();
        Check(own3.Length == 0 || own3.All(a => !(a.TargetIds?.Contains(berserker3.Id) ?? false)), "an enemy-only destroy cannot even target his own Berserker (control)");
        // A non-Battle action: an Open action targeting the Berserker is not a Battle action. (The Action Phase, the same position at home.)
        var s4 = Table(Phase.Action, 1); s4.PriorityPlayerId = 1;
        StrongholdOf(s4, 0, Halls);
        var b4 = Add(s4, "training-berserker", 0, Zone.Home);
        var opener = Add(s4, DefinitionWith("training-bow-enemy-card-open"), 1, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        var open = Options(e4, 1, "training-bow-enemy-card-open", opener.Id).FirstOrDefault(a => a.Enabled && (a.TargetIds?.Contains(b4.Id) ?? false));
        if (open is not null)
        {
            Accept(e4, open.Command! with { TargetId = b4.Id });
            Check(!ReactionOffered(e4, 0, "halls-forgotten-delay"), "an Open action is not a Battle action: Halls is not offered");
        }
    }

    private static void HiromiNotDelayed()
    {
        var hiromi = CardCatalog.Cards.Values.First(c => c.Abilities?.Contains("hiromi-thunder") == true);
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var halls = StrongholdOf(s, 0, Halls);
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0);
        var actor = Add(s, hiromi.Id, 1, Zone.Battlefield, province: 0);
        Force(actor, 10);
        var e = MatchEngine.FromTestState(s);
        var offer = Options(e, 1, "hiromi-thunder", actor.Id).FirstOrDefault(a => a.Enabled && (a.TargetIds?.Contains(berserker.Id) ?? false));
        Check(offer is not null, "Hiromi's Thunder action can target the Berserker: " + string.Join("; ", Options(e, 1, "hiromi-thunder", actor.Id).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        Accept(e, offer!.Command! with { TargetId = berserker.Id });
        Check(ReactionOffered(e, 0, "halls-forgotten-delay"), "Halls' Reaction is offered");
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Dead, "Hiromi's destruction \"will not be negated or delayed\": the Berserker is destroyed at once");
        Check(e.State.DelayedEffects.Count == 0, "nothing was recorded");
    }

    private static void JourneysEndKeepNotDelayed()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var halls = StrongholdOf(s, 0, Halls);
        var keep = StrongholdOf(s, 1, "oracle-l5r-4059");
        var berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0);
        var performer = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "journeys-end-move", keep.Id, performer.Id).Command! with { TargetId = berserker.Id });
        Check(ReactionOffered(e, 0, "halls-forgotten-delay"), "Halls' Reaction is offered against the Keep's Battle action");
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Eq(e.State.Cards[berserker.Id].Zone, Zone.Home, "the Keep's movement will not be delayed: he is home at once");
        Check(e.State.DelayedEffects.Count == 0, "nothing was recorded");
    }

    // ---- the store ---------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine Rig(Action<MatchState>? edit = null, Phase phase = Phase.Action, int actor = 0)
    {
        var s = Table(phase, actor); s.PriorityPlayerId = actor;
        edit?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void DelayedOrdering()
    {
        // Dishonor then destroy: he dies dishonorable, and his controller loses his Personal Honor. Destroy then dishonor: nothing is left to dishonor.
        int HonorLostWhen(params string[] order)
        {
            var s = Table(Phase.Action, 0);
            var victim = Add(s, "training-crane-samurai", 1, Zone.Home); // Personal Honor 2
            var e = MatchEngine.FromTestState(s);
            var before = e.State.Players[1].Honor;
            foreach (var kind in order) e.DelayEffectForTest(victim.Id, kind, DelayUntil.EndOfTurn, 0);
            Check(e.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn), "effects were due");
            Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, $"{string.Join(" then ", order)}: he is dead");
            return before - e.State.Players[1].Honor;
        }
        Eq(HonorLostWhen(DelayedKinds.Dishonor, DelayedKinds.Destroy), 2, "dishonored first, destroyed after: the dishonorable death costs his Personal Honor");
        Eq(HonorLostWhen(DelayedKinds.Destroy, DelayedKinds.Dishonor), 0, "destroyed first: the dishonoring is negated (he is out of play), so the death is an honorable one");

        // Several effects stack on one card, applied in the order recorded, and effects with the same trigger never reorder across cards.
        var s2 = Table(Phase.Action, 0);
        var first = Add(s2, "training-crane-samurai", 1, Zone.Home); var second = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        e2.DelayEffectForTest(second.Id, DelayedKinds.Modifier, DelayUntil.EndOfTurn, 0, stat: "Force", amount: 2);
        e2.DelayEffectForTest(first.Id, DelayedKinds.Bow, DelayUntil.EndOfTurn, 0);
        e2.DelayEffectForTest(first.Id, DelayedKinds.Modifier, DelayUntil.EndOfTurn, 0, stat: "Force", amount: -1);
        e2.DelayEffectForTest(first.Id, DelayedKinds.Dishonor, DelayUntil.EndOfTurn, 0);
        Eq(string.Join(",", e2.State.DelayedEffects.Select(x => x.Seq)), "1,2,3,4", "sequence numbers follow the order of application");
        e2.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn);
        var applied = e2.State.Events.Where(x => x.Type is "delayedEffectApplied").Select(x => x.Message).ToList();
        Eq(applied.Count, 4, "all four applied: " + string.Join(" | ", applied));
        Check(applied[0].Contains("Lion") && applied[1].Contains("bowed") && applied[2].Contains("-1") && applied[3].Contains("dishonored"), "in the order applied: " + string.Join(" | ", applied));
        Check(e2.State.Cards[first.Id].Bowed && e2.State.Cards[first.Id].Dishonorable, "the first card is bowed and dishonored");
        Check(e2.State.DelayedEffects.Count == 0, "the store is empty");
    }

    private static void CannotApplyRule()
    {
        var st = Table(Phase.Action, 0);
        var home = Add(st, "training-crane-samurai", 1, Zone.Home);      // at home
        var field = Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0);  // at the battlefield
        var bowedHome = Add(st, "training-crane-veteran", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(st);
        // Cannot be applied: moving a Personality who is home home, moving a home Personality to a battlefield.
        e.DelayEffectForTest(home.Id, DelayedKinds.MoveHome, DelayUntil.EndOfBattle, 0);
        e.DelayEffectForTest(home.Id, DelayedKinds.MoveToBattlefield, DelayUntil.EndOfBattle, 0, arg: "0");
        // Can be applied from anywhere: bow, dishonor, modifier, straighten.
        e.DelayEffectForTest(home.Id, DelayedKinds.Bow, DelayUntil.EndOfBattle, 0);
        e.DelayEffectForTest(home.Id, DelayedKinds.Dishonor, DelayUntil.EndOfBattle, 0);
        e.DelayEffectForTest(home.Id, DelayedKinds.Modifier, DelayUntil.EndOfBattle, 0, stat: "Force", amount: 3);
        e.DelayEffectForTest(bowedHome.Id, DelayedKinds.Straighten, DelayUntil.EndOfBattle, 0);
        // At a battlefield: a move home applies; a move to the battlefield he is at is negated (he is there already).
        e.DelayEffectForTest(field.Id, DelayedKinds.MoveToBattlefield, DelayUntil.EndOfBattle, 0, arg: "0");
        e.DelayEffectForTest(field.Id, DelayedKinds.MoveHome, DelayUntil.EndOfBattle, 0);
        e.ApplyDelayedEffectsForTest(DelayUntil.EndOfBattle);
        var negated = e.State.Events.Where(x => x.Type == "delayedEffectNegated").Select(x => x.Message).ToList();
        Eq(negated.Count, 3, "exactly the three that cannot be applied are negated: " + string.Join(" | ", negated));
        Check(negated.Any(m => m.Contains("Crane training samurai") && m.Contains("moving home") && m.Contains("already home")), "no move home from home");
        Check(negated.Any(m => m.Contains("Crane training samurai") && m.Contains("moving to a battlefield") && m.Contains("not at a battlefield")), "no move to a battlefield from home");
        Check(negated.Any(m => m.Contains("Lion training samurai") && m.Contains("moving to a battlefield") && m.Contains("there already")), "no move to the battlefield he is at");
        Check(e.State.Cards[home.Id].Bowed && e.State.Cards[home.Id].Dishonorable, "bow and dishonor applied from home");
        Eq(e.GetView(0).Players[1].Home.Single(c => c.Id == home.Id).Force, 6, "the Force modifier applied (3 + 3)");
        Check(!e.State.Cards[bowedHome.Id].Bowed, "straighten applied from home");
        Eq(e.State.Cards[field.Id].Zone, Zone.Home, "the move home applied to the Personality still at a battlefield");

        // A card that left play meanwhile has nothing left to be affected: everything recorded for it is negated, quietly.
        var st2 = Table(Phase.Action, 0);
        var gone = Add(st2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(st2);
        e2.DelayEffectForTest(gone.Id, DelayedKinds.Destroy, DelayUntil.EndOfTurn, 0);
        e2.DelayEffectForTest(gone.Id, DelayedKinds.Bow, DelayUntil.EndOfTurn, 0);
        var e3 = Edit(e2, st => { st.Cards[gone.Id].Zone = Zone.Dead; });
        e3.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn);
        Eq(e3.State.Events.Count(x => x.Type == "delayedEffectNegated"), 2, "both negated: he left play");
        Check(!e3.State.Events.Any(x => x.Type is "delayedDestruction" or "delayedEffectApplied"), "nothing was applied");
        Eq(e3.State.Cards[gone.Id].Zone, Zone.Dead, "left where he is");
        Check(e3.State.DelayedEffects.Count == 0, "and the store is empty");
    }

    private static void TriggerPoints()
    {
        var st = Table(Phase.Action, 0); st.PriorityPlayerId = 0;
        var card = Add(st, "training-crane-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(st);
        var startTurn = e.State.TurnNumber;
        e.DelayEffectForTest(card.Id, DelayedKinds.Modifier, DelayUntil.EndOfBattle, 0, stat: "Force", amount: 1);
        e.DelayEffectForTest(card.Id, DelayedKinds.Modifier, DelayUntil.EndOfPhase, 0, stat: "Force", amount: 10);
        e.DelayEffectForTest(card.Id, DelayedKinds.Modifier, DelayUntil.NextDynastyPhase, 0, stat: "Force", amount: 100);
        e.DelayEffectForTest(card.Id, DelayedKinds.Modifier, DelayUntil.EndPhaseBegins, 0, stat: "Force", amount: 1000);
        e.DelayEffectForTest(card.Id, DelayedKinds.Modifier, DelayUntil.EndOfTurn, 0, stat: "Force", amount: 10000);
        int Bonus() => e.State.Cards[card.Id].Modifiers.Where(m => m.Stat == "Force" && m.Source == "delayed-effect" || m.Effect == "delayed-effect").Sum(m => m.Amount);
        Eq(Bonus(), 0, "nothing applied while recording");
        Check(e.ApplyDelayedEffectsForTest(DelayUntil.EndOfBattle) && Bonus() == 1, "the end of the battle applies only its own");
        Eq(e.State.DelayedEffects.Count, 4, "four remain");
        // The store survives the phases: play the turn out and read the log. The Action Phase's end applies its own (+10) as the Action Phase ends, the next Dynasty Phase's (+100) as it begins, the End Phase's
        // (+1000) as that begins, and the end of the turn's (+10000) at the top of EndTurn, the engine's \"before this turn ends\" point, which comes before the End Phase begins.
        RunUntil(e, () => e.State.TurnNumber > startTurn);
        var log = e.State.Events.ToList();
        int At(Func<GameEvent, bool> match) => log.FindIndex(x => match(x));
        bool Applies(GameEvent x, int amount) => x.Type == "delayedEffectApplied" && x.Message.Contains($"+{amount} Force");
        var phase = log.Where(x => x.Type == "phaseChanged").Select(x => x.Message).ToList();
        var endPhaseAt = At(x => x.Type == "phaseChanged" && x.Message == "Phase: End.");
        var dynastyAt = At(x => x.Type == "phaseChanged" && x.Message == "Phase: Dynasty.");
        var actionEndAt = At(x => Applies(x, 10)); var dynastyEffectAt = At(x => Applies(x, 100)); var endEffectAt = At(x => Applies(x, 1000)); var turnEndAt = At(x => Applies(x, 10000));
        Check(actionEndAt >= 0 && dynastyEffectAt >= 0 && endEffectAt >= 0 && turnEndAt >= 0, "all four applied: " + string.Join(" | ", log.Where(x => x.Type == "delayedEffectApplied").Select(x => x.Message)));
        Check(actionEndAt < dynastyAt && dynastyAt < dynastyEffectAt && dynastyEffectAt < turnEndAt && turnEndAt < endPhaseAt && endPhaseAt < endEffectAt,
            $"the order of the trigger points: phase end {actionEndAt}, Dynasty begins {dynastyAt}, its effect {dynastyEffectAt}, turn end {turnEndAt}, End begins {endPhaseAt}, its effect {endEffectAt}");
        Eq(log.Count(x => x.Type == "delayedEffectApplied"), 5, "each of the five effects was applied exactly once (the temporary bonuses ended with the turn)");
        Check(e.State.DelayedEffects.Count == 0, "the store is empty");
    }

    // ---- the duel's delay --------------------------------------------------------------------------------------------------------------------------------

    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));

    private static void ClosePassingDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // Both sides strike at once; the duel resolves as far as it can without a decision.
    private static void DriveToFocusEffects(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
    }

    // Player 1's Mirumoto Ichizo, Experienced (Iaijutsu: "duel him. Destroy the duel's loser") challenges player 0's Personality; player 0 focuses `focusDef` and, when `p1Focus` names one, player 1 does too.
    // The winner is the one given the Chi: player 1's Ichizo when p0Loses, else player 0's Personality.
    private static (MatchEngine E, CardInstance Ichizo, CardInstance Target, CardInstance Focus) IchizoDuel(string focusDef, bool p0Loses = true, string? p1Focus = null)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        ChiBoost(p0Loses ? ichizo : target, 20);
        Force(target, 10); // player 0's army wins the battle that follows, so it is the duel (not the battle) that would destroy him
        var focus = Add(s, focusDef, 0, Zone.Hand);
        var other = p1Focus is null ? null : Add(s, p1Focus, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        ClosePassingDuel(e);
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
        if (other is not null) Accept(e, new GameCommand("focus", 1, e.Version, CardId: other.Id));
        DriveToFocusEffects(e);
        // Two Focus Effects are ordered by the active player (player 1): the loser's own first.
        if (other is not null && e.State.PendingChoice is null && e.State.PendingDuel is { Stage: "OrderingEffects" }) Accept(e, new GameCommand("orderDuelFocusEffects", e.State.ActivePlayerId, e.Version, CardIds: [focus.Id, other.Id]));
        return (e, ichizo, target, focus);
    }

    private static readonly string[] DelayCards = [Compassion, Courage, Honesty, LionHonor, Loyalty, SpiderStrength];

    private static void DuelLossDelayChoice()
    {
        foreach (var card in DelayCards)
        {
            var name = CardCatalog.Get(card).Name;
            // A yes: the destruction of the loser is recorded, not applied; he stands through the battle and is destroyed as the next Dynasty Phase begins.
            var (e, _, target, focus) = IchizoDuel(card);
            Eq(e.State.PendingChoice?.Type, "focusEffectChoice", $"{name}: the choice opens before the outcome applies");
            Eq(e.State.PendingChoice!.PlayerId, 0, $"{name}: it is asked of the focus card's owner, whose Personality lost");
            Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, $"{name}: nothing has happened to the loser yet");
            var offered = e.QueryActions(0).Where(a => a.Type == "chooseFocusEffect").ToArray();
            Check(offered.Length == 2 && offered.Any(o => o.Command!.TargetId == "delay") && offered.Any(o => o.Command!.Choice == "decline"), $"{name}: the answers are 'delay' and 'do not delay'");
            Accept(e, new GameCommand("chooseFocusEffect", 0, e.Version, CardId: focus.Id, TargetId: "delay"));
            Check(e.State.PendingChoice is null && e.State.PendingDuel is null, $"{name}: the duel finished");
            Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, $"{name}: the loser was not destroyed by the duel");
            var recorded = e.State.DelayedEffects.Single();
            Check(recorded.Kind == DelayedKinds.Destroy && recorded.CardId == target.Id && recorded.Until == DelayUntil.NextDynastyPhase, $"{name}: a destruction waiting for the next Dynasty Phase: {recorded.Kind}/{recorded.Until}");
            Check(e.State.Events.Any(x => x.Type == "duelLossDelayed") && e.State.Events.Any(x => x.Type == "destructionDelayed"), $"{name}: logged");
            RunUntil(e, () => e.State.Phase == Phase.Dynasty);
            Eq(e.State.Cards[target.Id].Zone, Zone.Dead, $"{name}: destroyed as the Dynasty Phase began");
            var log = e.State.Events.Select(x => x.Type + ":" + x.Message).ToList();
            Check(log.FindIndex(x => x == "phaseChanged:Phase: Dynasty.") < log.FindIndex(x => x.StartsWith("delayedDestruction:")), $"{name}: after the phase began: " + string.Join(" | ", log.Where(x => x.StartsWith("phaseChanged") || x.StartsWith("delayed") || x.StartsWith("battle")).Select(x => x[..Math.Min(60, x.Length)])));
            Check(e.State.DelayedEffects.Count == 0, $"{name}: the store is empty");

            // A no: the duel's outcome applies as usual.
            var (e2, _, target2, focus2) = IchizoDuel(card);
            Accept(e2, new GameCommand("chooseFocusEffect", 0, e2.Version, CardId: focus2.Id, Choice: "decline"));
            Eq(e2.State.Cards[target2.Id].Zone, Zone.Dead, $"{name}: declined, the loser is destroyed at once");
            Check(e2.State.DelayedEffects.Count == 0, $"{name}: nothing was recorded");
            Eq(e2.State.Cards[focus2.Id].Zone, Zone.FateDiscard, $"{name}: the focused card is spent");
        }
    }

    private static void DuelLossDelayScope()
    {
        // The focus card's owner WON (player 0's Personality has the Chi): nobody is asked, the loser (player 1's Ichizo) is destroyed as ever.
        foreach (var card in DelayCards)
        {
            var (e, ichizo, target, _) = IchizoDuel(card, p0Loses: false);
            Check(e.State.PendingChoice is not { Type: "focusEffectChoice" }, $"{CardCatalog.Get(card).Name}: a winner has no losing effects to delay, so nothing is asked");
            Eq(e.State.Cards[ichizo.Id].Zone, Zone.Dead, "the other player's loser is destroyed at once: only the focus owner's own loss is delayed");
            Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the winner is untouched");
            Check(e.State.DelayedEffects.Count == 0, "nothing was recorded");
        }
        // Both players focus a delay card and player 0 loses: only player 0 is asked; player 1's card is not.
        var (e2, _, target2, focus2) = IchizoDuel(LionHonor, p0Loses: true, p1Focus: Loyalty);
        Eq(e2.State.PendingChoice?.Type, "focusEffectChoice"); Eq(e2.State.PendingChoice!.PlayerId, 0, "only the loser's controller is asked");
        Accept(e2, new GameCommand("chooseFocusEffect", 0, e2.Version, CardId: focus2.Id, TargetId: "delay"));
        Check(e2.State.PendingChoice is null, "and player 1's delay card was not asked at all (his Personality won)");
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Battlefield, "the loser's destruction is delayed");
        // Another player cannot answer for him, and only the offered answers are legal.
        var (e3, _, _, focus3) = IchizoDuel(Compassion);
        Check(Rejection(e3, new GameCommand("chooseFocusEffect", 1, e3.Version, CardId: focus3.Id, TargetId: "delay")).Length > 0, "the other player may not answer the choice");
        Reject(e3, new GameCommand("chooseFocusEffect", 0, e3.Version, CardId: focus3.Id, TargetId: "not-a-choice"));
    }

    private static void DuelLossDelayOrdering()
    {
        // Reckless Duel (a Strategy): "Your Personality duels the target. Bow the duel's loser. Move him home." Player 0 loses and delays.
        var s = Table();
        var performer = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0); ChiBoost(performer, 20);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(target, 10);
        var reckless = Add(s, DefinitionWith("reckless-duel"), 1, Zone.Hand);
        var focus = Add(s, Courage, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "reckless-duel", reckless.Id, target.Id, performer.Id));
        ClosePassingDuel(e);
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
        DriveToFocusEffects(e);
        Accept(e, new GameCommand("chooseFocusEffect", 0, e.Version, CardId: focus.Id, TargetId: "delay"));
        Check(!e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].Zone == Zone.Battlefield, "the loser is neither bowed nor moved by the duel");
        Eq(string.Join(",", e.State.DelayedEffects.OrderBy(x => x.Seq).Select(x => x.Kind)), $"{DelayedKinds.Bow},{DelayedKinds.MoveHome}", "the bow and the move home are recorded in the order the outcome applied them");
        Check(e.State.DelayedEffects.All(x => x.Until == DelayUntil.NextDynastyPhase), "both wait for the next Dynasty Phase");
        RunUntil(e, () => e.State.Phase == Phase.Dynasty);
        Check(e.State.Cards[target.Id].Bowed, "the bow was applied as the Dynasty Phase began");
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "he is home by then (the battle is long over)");
        Check(e.State.Events.Any(x => x.Type == "delayedEffectNegated" && x.Message.Contains("moving home") && x.Message.Contains("already home")), "the move home was negated: he was already home");
    }

    // ---- The Honor of the Lion ---------------------------------------------------------------------------------------------------------------------------

    private static int PhBonus(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "PersonalHonor").Sum(m => m.Amount);

    private static void HonorOfTheLion()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var lion = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);   // Personal Honor 2
        var enemyLow = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);   // Personal Honor 2
        var enemyHigh = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        enemyHigh.Modifiers.Add(new("test-source", "test", "PersonalHonor", 5)); // 3 + 5 = 8: above 4
        var strategy = Add(s, LionHonor, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "honor-lion-boost", strategy.Id, lion.Id).Command!);
        Eq(PhBonus(e, lion.Id), 2, "a bonus raising his Personal Honor from 2 to 4");
        Check(e.State.Cards[lion.Id].Modifiers.Single(m => m.Stat == "PersonalHonor").Permanent, "and it is permanent");
        Check(e.State.Cards[lion.Id].GrantedAbilities.Any(g => g.AbilityId == "honor-lion-bow" && g.Duration == GrantDuration.Permanent), "the ability is granted permanently");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // The granted ability: an enemy Personality with equal or lower Personal Honor (4) than the holder; not one with higher.
        e = Edit(e, st => st.PriorityPlayerId = 0);
        var granted = Enabled(e, 0, "honor-lion-bow", lion.Id);
        Check(granted.TargetIds!.Contains(enemyLow.Id) && !granted.TargetIds.Contains(enemyHigh.Id), "only the enemy with equal or lower Personal Honor is a target");
        Reject(e, granted.Command! with { TargetId = enemyHigh.Id });
        var honor = Honor(e, 0);
        Accept(e, granted.Command! with { TargetId = enemyLow.Id });
        Check(e.State.Cards[enemyLow.Id].Bowed, "he is bowed");
        Eq(Honor(e, 0), honor + 2, "and its controller gains 2 Honor");

        // No bonus when his Personal Honor is 4 or more already; the ability is still granted.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var high = Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0); high.Modifiers.Add(new("test-source", "test", "PersonalHonor", 3));
        var st2 = Add(s2, LionHonor, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "honor-lion-boost", st2.Id, high.Id).Command!);
        Eq(PhBonus(e2, high.Id), 3, "no bonus is added when he has 4 or more");
        Check(e2.State.Cards[high.Id].GrantedAbilities.Any(g => g.AbilityId == "honor-lion-bow"), "but he gets the ability");

        // Performers: not a Crane, not bowed, not dishonorable.
        foreach (var (name, adjust) in new (string, Action<CardInstance>)[] { ("a Personality who is not Lion Clan", c => c.DefinitionId = "training-crane-samurai"), ("a bowed Lion", c => c.Bowed = true), ("a dishonorable Lion", c => c.Dishonorable = true) })
        {
            var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
            var performer = Add(s3, "training-lion-samurai", 0, Zone.Battlefield, province: 0); adjust(performer);
            Add(s3, LionHonor, 0, Zone.Hand);
            var e3 = MatchEngine.FromTestState(s3);
            Check(!CanUse(e3, 0, "honor-lion-boost"), $"{name} may not perform it");
        }
        // The Open half: in the Action Phase from home.
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var homeLion = Add(s4, "training-lion-samurai", 0, Zone.Home);
        var st4 = Add(s4, LionHonor, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 0, "honor-lion-boost", st4.Id, homeLion.Id).Command!);
        Eq(PhBonus(e4, homeLion.Id), 2, "usable as an Open action, from home");
        // A real printed Lion (Akodo Furu, reported unplayable): the same Open play from home, for the active player and for the other player.
        foreach (var active in new[] { 0, 1 })
        {
            var s5 = MatchEngine.CreatePublishedMatch().State; s5.Phase = Phase.Action; s5.ActivePlayerId = active; s5.PriorityPlayerId = 0;
            var furu = Add(s5, "oracle-l5r-9823", 0, Zone.Home);
            var st5 = Add(s5, LionHonor, 0, Zone.Hand);
            var e5 = MatchEngine.FromTestState(s5);
            Check(CanUse(e5, 0, "honor-lion-boost"), $"Akodo Furu at home may perform it (player {active} active)");
            Accept(e5, Enabled(e5, 0, "honor-lion-boost", st5.Id, furu.Id).Command!);
        }
    }

    // ---- The Loyalty of the Scorpion ---------------------------------------------------------------------------------------------------------------------

    // Player 1 attacks and acts with `actorDef`'s Battle action; player 0 has a Scorpion Clan Personality at the battlefield (the target), another Scorpion Clan Personality at home (the performer) and the Strategy.
    private static (MatchEngine E, CardInstance Target, CardInstance Sacrifice, CardInstance Strategy, CardInstance Actor) LoyaltyRig(string actorDef, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var target = Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        var sacrifice = Add(s, "training-scorpion-veteran", 0, Zone.Home);
        var strategy = Add(s, Loyalty, 0, Zone.Hand);
        var actor = Add(s, actorDef, 1, Zone.Battlefield, province: 0);
        edit?.Invoke(s);
        return (MatchEngine.FromTestState(s), target, sacrifice, strategy, actor);
    }

    private static void LoyaltyOfTheScorpion()
    {
        // The bowing: a Battle action that bows him is answered; the performing Scorpion is destroyed as the cost, and the target is not bowed.
        var (e, target, sacrifice, strategy, actor) = LoyaltyRig("training-battle-bower");
        Accept(e, Offer(e, 1, "training-bow-at-battle", actor.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the target's controller");
        Check(CanUse(e, 0, "loyalty-scorpion-bow", strategy.Id) && !CanUse(e, 0, "loyalty-scorpion-destroy", strategy.Id) && !CanUse(e, 0, "loyalty-scorpion-move", strategy.Id),
            "only the bowing can be negated: the action neither destroys nor moves him");
        Accept(e, Enabled(e, 0, "loyalty-scorpion-bow", strategy.Id, sacrifice.Id).Command!);
        ClosePassing(e);
        Eq(e.State.Cards[sacrifice.Id].Zone, Zone.Dead, "the performing Scorpion Clan Personality was destroyed as the cost");
        Check(!e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].Zone == Zone.Battlefield, "the target's bowing was negated");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");

        // The destruction.
        var (e2, target2, sacrifice2, strategy2, actor2) = LoyaltyRig("training-no-attachments-destroyer");
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", actor2.Id, target2.Id));
        Check(CanUse(e2, 0, "loyalty-scorpion-destroy", strategy2.Id) && !CanUse(e2, 0, "loyalty-scorpion-bow", strategy2.Id), "only the destruction can be negated");
        Accept(e2, Enabled(e2, 0, "loyalty-scorpion-destroy", strategy2.Id, sacrifice2.Id).Command!);
        ClosePassing(e2);
        Eq(e2.State.Cards[sacrifice2.Id].Zone, Zone.Dead, "the performer is destroyed");
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Battlefield, "the target's destruction was negated");

        // The movement: a move-home action (Mihato's).
        var mihato = CardCatalog.Cards.Values.First(c => c.Abilities?.Contains("mihato-move") == true).Id;
        var (e3, target3, sacrifice3, strategy3, actor3) = LoyaltyRig(mihato);
        Accept(e3, Offer(e3, 1, "mihato-move", actor3.Id, target3.Id));
        Check(CanUse(e3, 0, "loyalty-scorpion-move", strategy3.Id) && !CanUse(e3, 0, "loyalty-scorpion-bow", strategy3.Id), "only the movement can be negated");
        Accept(e3, Enabled(e3, 0, "loyalty-scorpion-move", strategy3.Id, sacrifice3.Id).Command!);
        ClosePassing(e3);
        Eq(e3.State.Cards[target3.Id].Zone, Zone.Battlefield, "the target's movement was negated");
        Eq(e3.State.Cards[sacrifice3.Id].Zone, Zone.Dead, "the performer is destroyed");

        // The performer may be the target himself (any of your performing Scorpion Clan Personalities), and only one of the three negations is played (one Strategy).
        var (e4, target4, _, strategy4, actor4) = LoyaltyRig("training-no-attachments-destroyer");
        Accept(e4, Offer(e4, 1, "training-destroy-no-attachments", actor4.Id, target4.Id));
        Check(Options(e4, 0, "loyalty-scorpion-destroy", strategy4.Id, target4.Id).Any(o => o.Enabled), "the target himself may be the performing Personality");

        // Not for a target who is not a Scorpion Clan Personality.
        var (e5, _, _, strategy5, actor5) = LoyaltyRig("training-battle-bower", st => Add(st, "training-crane-samurai", 0, Zone.Battlefield, province: 0));
        var crane = e5.State.Cards.Values.Single(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.DefinitionId == "training-crane-samurai");
        Accept(e5, Offer(e5, 1, "training-bow-at-battle", actor5.Id, crane.Id));
        Check(!CanUse(e5, 0, "loyalty-scorpion-bow", strategy5.Id), "an action that targets a Personality who is not Scorpion Clan gives it nothing to negate");
    }

    private static void LoyaltyAndHalls()
    {
        // A Scorpion Berserker under Halls' Reaction with Loyalty in hand: the bowing is negated by Loyalty; the movement is delayed by Halls -- both are answered before the action resolves.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 1;
        var halls = StrongholdOf(s, 0, Halls);
        var target = Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0); GiveKeyword(target, "Berserker"); Force(target, 10);
        var sacrifice = Add(s, "training-scorpion-veteran", 0, Zone.Home);
        var strategy = Add(s, Loyalty, 0, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var blockSupply = Add(s, DefinitionWith("block-supply"), 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "block-supply", blockSupply.Id, target.Id));
        Accept(e, Enabled(e, 0, "loyalty-scorpion-bow", strategy.Id, sacrifice.Id).Command!);
        Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the window alternates: the attacker passes, then Halls' player may use his second Reaction
        Accept(e, ReactionOffer(e, 0, "halls-forgotten-delay", halls.Id));
        ClosePassing(e);
        Check(!e.State.DelayedEffects.Any(x => x.Kind == DelayedKinds.Bow), "the bow was negated, so there is none to delay");
        Eq(string.Join(",", e.State.DelayedEffects.Select(x => x.Kind)), DelayedKinds.MoveHome, "the move home was delayed");
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "he has not moved");
        FinishBattle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "he went home with the battle's aftermath; the delayed move was negated (already home)");
    }

    // ---- The Compassion of the Unicorn -------------------------------------------------------------------------------------------------------------------

    private static void CompassionOfTheUnicorn()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var unicorn = Add(s, "training-battle-maiden", 0, Zone.Battlefield, province: 0);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var ownFollower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: own.Id);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, bowed: true, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: enemy.Id);
        var strategy = Add(s, Compassion, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "compassion-unicorn-straighten", strategy.Id, unicorn.Id);
        Check(option.TargetIds!.Contains(own.Id) && option.TargetIds.Contains(enemy.Id) && !option.TargetIds.Contains(unicorn.Id), "another unit of either player is a target, not the performer's own");
        Reject(e, option.Command! with { TargetId = unicorn.Id });
        var honor = Honor(e, 0);
        Accept(e, option.Command! with { TargetId = own.Id });
        Check(!e.State.Cards[own.Id].Bowed && !e.State.Cards[ownFollower.Id].Bowed, "his own unit is straightened, Follower included");
        Eq(Honor(e, 0), honor + 1, "1 Honor for a unit its player controls");

        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var unicorn2 = Add(s2, "training-battle-maiden", 0, Zone.Battlefield, province: 0);
        var enemy2 = Add(s2, "training-lion-veteran", 1, Zone.Battlefield, bowed: true, province: 0);
        var enemyFollower2 = Add(s2, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: enemy2.Id);
        var strategy2 = Add(s2, Compassion, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Accept(e2, Offer(e2, 0, "compassion-unicorn-straighten", strategy2.Id, enemy2.Id, unicorn2.Id));
        Check(!e2.State.Cards[enemy2.Id].Bowed && !e2.State.Cards[enemyFollower2.Id].Bowed, "another player's unit is straightened too");
        Eq(Honor(e2, 0), honor2 + 2, "2 Honor if you do not control the target");

        // Performers: bowed, or not Unicorn Clan.
        foreach (var (name, adjust) in new (string, Action<CardInstance>)[] { ("a bowed Unicorn", c => c.Bowed = true), ("a Personality who is not Unicorn Clan", c => c.DefinitionId = "training-crane-samurai") })
        {
            var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
            var performer = Add(s3, "training-battle-maiden", 0, Zone.Battlefield, province: 0); adjust(performer);
            Add(s3, "training-lion-veteran", 1, Zone.Battlefield, bowed: true, province: 0);
            Add(s3, Compassion, 0, Zone.Hand);
            var e3 = MatchEngine.FromTestState(s3);
            Check(!CanUse(e3, 0, "compassion-unicorn-straighten"), $"{name} may not perform it");
        }
        // The Open half: from home, at any unit in play.
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var home = Add(s4, "training-battle-maiden", 0, Zone.Home);
        var far = Add(s4, "training-lion-veteran", 1, Zone.Home, bowed: true);
        var strategy4 = Add(s4, Compassion, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "compassion-unicorn-straighten", strategy4.Id, far.Id, home.Id));
        Check(!e4.State.Cards[far.Id].Bowed, "usable as an Open action, at a unit at home");
    }

    // ---- The Honesty of the Phoenix ----------------------------------------------------------------------------------------------------------------------

    private static void HonestyOfThePhoenix()
    {
        MatchEngine Named(string title, out CardInstance menace, out CardInstance courtier, out CardInstance victim, out CardInstance honesty, bool inspect = false)
        {
            var a = Table(Phase.Action, 0);
            a.Players[0].Clan = "Phoenix"; // "If you are a Phoenix Clan player" (the corrected scan, issue #494)
            honesty = Add(a, Honesty, 0, Zone.Hand);
            menace = Add(a, "oracle-l5r-9130", 0, Zone.Hand); // Veiled Menace
            var phoenix = Add(a, "training-samurai", 0, Zone.Home);   // Phoenix Clan
            courtier = Add(a, "training-crane-courtier", 0, Zone.Home); GiveKeyword(courtier, "Courtier");
            victim = Add(a, "training-lion-veteran", 1, Zone.Home); victim.Dishonorable = true;
            var deck = Add(a, "training-focus-strategy", 0, Zone.FateDeck); a.Players[0].FateDeck.Insert(0, deck.Id);
            Add(a, "training-focus-strategy", 1, Zone.Hand);
            var ae = MatchEngine.FromTestState(a);
            Accept(ae, Enabled(ae, 0, "honesty-phoenix-name", honesty.Id, phoenix.Id).Command!);
            Eq(ae.State.PendingChoice?.Type, "effectChoice", "the card is a choice");
            var names = ae.QueryActions(0).Where(o => o.Type == "chooseEffect").Select(o => o.Label).ToArray();
            if (inspect)
            {
                Check(names.Contains("Name Veiled Menace") && names.Contains("Name The Honesty of the Phoenix") && names.Contains("Name Block Supply Lines"), "the Strategies' titles can be named");
                Check(names.Length == names.Distinct().Count(), "each title once");
                Check(!names.Contains("Name Crane training samurai") && names.All(n => n.StartsWith("Name ")), "a Personality's title is not offered (it could never match an action)");
                Check(CardCatalog.PlayableFromDecks.Where(c => c.Id.StartsWith("oracle-l5r-") && c.Type == CardType.Ring).All(r => names.Contains($"Name {r.Name}")), "Rings can be named as well");
            }
            Accept(ae, ae.QueryActions(0).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == $"name:{title}").Command!);
            Eq(ae.State.SincerityWatches.Single().Title, title, "the title is recorded");
            Eq(ae.State.Cards[honesty.Id].Zone, Zone.RemovedFromGame, "'Remove this card from the game': naming a card removes it, it is not discarded");
            Check(!ae.State.Players[0].Hand.Contains(honesty.Id), "and it is no longer in the hand");
            return ae;
        }
        {
            var ae = Named("Veiled Menace", out var menace, out var courtier, out var victim, out _, inspect: true);
            var theirs = ae.State.Players[1].Hand.Count;
            ae = Edit(ae, st => st.PriorityPlayerId = 0);
            var mine = ae.State.Players[0].Hand.Count;
            Accept(ae, Offer(ae, 0, "veiled-menace-loss", menace.Id, victim.Id, courtier.Id));
            Eq(ae.State.PendingChoice?.Type, "eitherOr", "the watched action resolved: its player chooses a player");
            Eq(ae.State.Players[0].Hand.Count, mine, "he drew (Veiled Menace left the hand)");
            Accept(ae, ae.QueryActions(0).Single(o => o.Type == "chooseEitherOr" && o.Command!.Choice == "opponent").Command!);
            Eq(ae.State.PendingChoice?.Type, "forcedDiscard", "the chosen player must discard a card");
            Eq(ae.State.PendingChoice!.PlayerId, 1);
            Accept(ae, new GameCommand("forcedDiscard", 1, ae.Version, TargetId: ae.State.Players[1].Hand.First()));
            Eq(ae.State.Players[1].Hand.Count, theirs - 1, "he discarded a card");
            Eq(ae.State.SincerityWatches.Count, 0, "the watch is spent");
        }
        {
            // Another title does not trigger it, and the watch waits.
            var ae = Named("Block Supply Lines", out var menace, out var courtier, out var victim, out _);
            ae = Edit(ae, st => st.PriorityPlayerId = 0);
            Accept(ae, Offer(ae, 0, "veiled-menace-loss", menace.Id, victim.Id, courtier.Id));
            Check(ae.State.PendingChoice?.Type != "eitherOr", "Veiled Menace is not the named title");
            Eq(ae.State.SincerityWatches.Count, 1, "the watch waits");
        }
        {
            // The card that named it is not "another" Strategy: naming its own title never triggers on itself; and a Personality who is not a Phoenix may not perform.
            var s = Table(Phase.Action, 0);
            s.Players[0].Clan = "Phoenix";
            Add(s, Honesty, 0, Zone.Hand); Add(s, "training-crane-samurai", 0, Zone.Home);
            Check(!CanUse(MatchEngine.FromTestState(s), 0, "honesty-phoenix-name"), "only an unbowed Phoenix Clan Personality performs it");
        }
        {
            // "If you are a Phoenix Clan player": a player of another Clan may not take the action even with a Phoenix Clan Personality; a bowed Phoenix Personality may not perform either.
            foreach (var (clan, bowed, allowed) in new[] { ("Crane", false, false), ("Dragon", false, false), ("Phoenix", true, false), ("Phoenix", false, true) })
            {
                var s = Table(Phase.Action, 0);
                s.Players[0].Clan = clan;
                var card = Add(s, Honesty, 0, Zone.Hand); var phoenix = Add(s, "training-samurai", 0, Zone.Home, bowed: bowed);
                var e = MatchEngine.FromTestState(s);
                Eq(CanUse(e, 0, "honesty-phoenix-name", card.Id, phoenix.Id), allowed, $"a {(bowed ? "bowed " : "")}Phoenix Clan Personality of a {clan} Clan player: {allowed}");
                if (!allowed) Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "the card stays in hand");
            }
        }
        {
            // Negated, the card is still removed from the game (as Mendon ruled for The Sincerity of the Dragon): "Remove this card from the game" is not one of the negatable effects; no watch is set.
            foreach (var negated in new[] { false, true })
            {
                var s = Table(Phase.Action, 0);
                s.Players[0].Clan = "Phoenix";
                var card = Add(s, Honesty, 0, Zone.Hand); var phoenix = Add(s, "training-samurai", 0, Zone.Home);
                var deck = Add(s, "training-focus-strategy", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, deck.Id);
                var e = MatchEngine.FromTestState(s);
                e.NegateEveryAction = negated;
                Accept(e, Enabled(e, 0, "honesty-phoenix-name", card.Id, phoenix.Id).Command!);
                if (!negated) Accept(e, e.QueryActions(0).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == "name:Veiled Menace").Command!);
                Eq(e.State.Cards[card.Id].Zone, Zone.RemovedFromGame, negated ? "negated: still removed from the game" : "not negated: removed from the game");
                Check(!e.State.Players[0].Hand.Contains(card.Id), "and not in the hand");
                Eq(e.State.SincerityWatches.Count, negated ? 0 : 1, negated ? "a negated action sets no watch" : "the watch is set");
            }
        }
    }

    // ---- The Courage of the Mantis -----------------------------------------------------------------------------------------------------------------------

    private static void CourageMove()
    {
        MatchEngine Fresh(bool enemyThere, bool bowed = false, string performerDef = "training-moshi-ikako")
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            if (enemyThere) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, performerDef, 0, Zone.Home, bowed: bowed);
            Add(s, Courage, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Fresh(true);
        var strategy = e.State.Cards.Values.Single(c => c.Zone == Zone.Hand && c.OwnerId == 0);
        var mantis = e.State.Cards.Values.Single(c => c.OwnerId == 0 && c.DefinitionId == "training-moshi-ikako");
        Accept(e, Enabled(e, 0, "courage-mantis-move", strategy.Id, mantis.Id).Command!);
        Eq(e.State.Cards[mantis.Id].Zone, Zone.Battlefield, "he moved to the current battlefield");
        Eq(e.State.Cards[mantis.Id].ProvinceIndex, (int?)0);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action is granted");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // No enemy units at the battlefield: not usable.
        var e2 = Fresh(false);
        Check(!CanUse(e2, 0, "courage-mantis-move"), "without enemy units at the current battlefield it cannot be used");
        // A bowed Mantis, or a Personality who is not Mantis Clan, cannot perform.
        var e3 = Fresh(true, bowed: true);
        Check(!CanUse(e3, 0, "courage-mantis-move"), "a bowed Mantis Clan Personality may not perform it");
        var e4 = Fresh(true, performerDef: "training-crane-samurai");
        Check(!CanUse(e4, 0, "courage-mantis-move"), "a Personality who is not Mantis Clan may not perform it");
    }

    private static void ExistingDelaysUseTheStore()
    {
        // In the Heart of Battle: "Destroy him before this turn ends" is a delayed destruction until the end of the turn.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(samurai, "Samurai");
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, "oracle-l5r-3725", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "heart-of-battle-surge", strategy.Id, samurai.Id).Command!);
        var scheduled = e.State.DelayedEffects.Single();
        Check(scheduled.Kind == DelayedKinds.Destroy && scheduled.Until == DelayUntil.EndOfTurn && scheduled.CardId == samurai.Id, "a delayed destruction until the end of the turn is in the store");
        Check(e.State.PendingDelayedDestructions.SequenceEqual(new[] { samurai.Id }), "and the old view reads it");
        Eq(e.State.Cards[samurai.Id].Zone, Zone.Battlefield, "he is alive now");
        RunUntil(e, () => e.State.Cards[samurai.Id].Zone == Zone.Dead);
        Check(e.State.Events.Any(x => x.Type == "delayedDestruction"), "applied by the store at the end of the turn");
    }
}
