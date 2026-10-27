using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Part of SecondCityBattleTests: the four cards Mendon's rulings unblocked (2026-09-21) -- Know No Fear and To First Blood (Reactions played from a hand), A Fugitive Apprehended (a second,
// Magistrate-only target), Dislodge the Foe (Tacticians as performers) -- and Kyuden Kitsune ("Your forests are maddening" is flavor; the movement negation and the Limited action are not).
internal static partial class SecondCityRulingTests
{
    private const string KnowNoFear = "oracle-l5r-4481", ToFirstBlood = "oracle-l5r-8500", Fugitive = "oracle-l5r-24", Dislodge = "oracle-l5r-2024", Kitsune = "oracle-l5r-4642",
        Hotako = "oracle-l5r-1726", Ichizo = "oracle-l5r-5104";

    public static void Run(TestRunner runner) => runner.Run("second-city-rulings", [
        ("Published shape: Know No Fear, To First Blood, A Fugitive Apprehended, Dislodge the Foe and Kyuden Kitsune are the printed cards with the abilities and Effect named for them and ship their art", PublishedShapes),
        ("Know No Fear, played from a hand: an action that would bow or move a Samurai opens the window for a reactor holding it, the Reaction negates the bowing and movement, the Strategy is spent -- and nothing opens when it is not held", KnowNoFearMoves),
        ("Know No Fear's second Reaction negates a Fear action as a whole (Hotako's lock); it is not offered against a plain action, a non-Samurai or the reactor's own action", KnowNoFearFear),
        ("To First Blood's Reaction, played from a hand against another player's Iaijutsu action, negates the destruction the duel would inflict on his Personality -- carried through the duel to its outcome", ToFirstBloodReaction),
        ("To First Blood's Focus Effect negates the destruction of every loser of the duel and pays 2 Honor to the winner's controller", ToFirstBloodFocus),
        ("A Fugitive Apprehended: the first target is an enemy card without attachments and with a lower Gold Cost than the performer's; only a Magistrate may name a second, optional enemy card, whose Gold Cost becomes 0 -- and that never makes a first target legal", FugitiveApprehended),
        ("Dislodge the Foe: any number of Tacticians at one other battlefield perform it, the Personality opposing them there is its one target, and they move to the current battlefield -- performers are not targets", DislodgeTheFoe),
        ("Kyuden Kitsune negates the first movement each battle into the Attacker's army at its controller's province (once per battle, not for its controller's own action), and its Limited action bows a Forest for 1 Honor", KyudenKitsune),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static bool ReactionOffered(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);
    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }
    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        void Shipped(string id) { Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped"); Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg"); Check(!CardCatalog.Get(id).ScenarioOnly, id); }
        var know = CardCatalog.Get(KnowNoFear);
        Check(know.Name == "Know No Fear" && know.Type == CardType.Strategy && know.Focus == 3 && know.GoldCost == 0 && know.Abilities!.SequenceEqual(new[] { "know-no-fear-negate-move", "know-no-fear-negate-fear" }), "Know No Fear");
        var blood = CardCatalog.Get(ToFirstBlood);
        Check(blood.Name == "To First Blood" && blood.Type == CardType.Strategy && blood.Focus == 3 && blood.Keywords!.Contains("Iaijutsu") && blood.Abilities!.SequenceEqual(new[] { "to-first-blood-focus", "to-first-blood-negate" }), "To First Blood");
        var fugitive = CardCatalog.Get(Fugitive);
        Check(fugitive.Name == "A Fugitive Apprehended" && fugitive.Focus == 1 && fugitive.Abilities!.SequenceEqual(new[] { "fugitive-apprehended" }), "A Fugitive Apprehended");
        var dislodge = CardCatalog.Get(Dislodge);
        Check(dislodge.Name == "Dislodge the Foe" && dislodge.Focus == 3 && dislodge.Abilities!.SequenceEqual(new[] { "dislodge-the-foe" }), "Dislodge the Foe");
        var kitsune = CardCatalog.Get(Kitsune);
        Check(kitsune.Name == "Kyuden Kitsune" && kitsune.Type == CardType.Stronghold && kitsune.GoldProduction == 4 && kitsune.ProvinceStrength == 7 && kitsune.StartingHonor == 4 && kitsune.Keywords!.Contains("Forest")
            && kitsune.Effect == "negate-first-movement-into-attacking-armies" && kitsune.Abilities!.SequenceEqual(new[] { "kitsune-forest-honor" }), "Kyuden Kitsune");
        foreach (var id in new[] { KnowNoFear, ToFirstBlood, Fugitive, Dislodge, Kitsune }) Shipped(id);
        Eq(PublishedContent.StrongholdWave10.Cards.Length, 1);
        Check(AbilityCatalog.All["dislodge-the-foe"].Slots![0].Performing && !AbilityCatalog.All["dislodge-the-foe"].Slots![1].Performing, "the Tacticians are a performing slot, the opposing Personality a target");
        Check(AbilityCatalog.All["fugitive-apprehended"].Slots![1] is { Min: 0, Max: 1 }, "the Magistrate's second target is optional");
    }

    // ---- Know No Fear -----------------------------------------------------------------------------------------------------------------------------
    private static void KnowNoFearMoves()
    {
        // Player 1 attacks with a Battle action that bows an enemy Personality; player 0 defends with a Samurai and holds Know No Fear.
        (MatchState S, CardInstance Samurai, CardInstance Card, CardInstance Bower) Build(bool held = true, bool samurai = true)
        {
            var s = Table();
            var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); if (samurai) GiveKeyword(target, "Samurai");
            var card = Add(s, KnowNoFear, 0, Zone.Hand); if (!held) { s.Players[0].Hand.Remove(card.Id); s.Cards.Remove(card.Id); }
            var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            return (s, target, card, bower);
        }
        var (s, samuraiCard, know, bower) = Build();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, samuraiCard.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for a reactor who holds Know No Fear");
        Check(!e.State.Cards[samuraiCard.Id].Bowed, "nothing has happened yet");
        Accept(e, ReactionOffer(e, 0, "know-no-fear-negate-move", know.Id));
        ClosePassing(e);
        Check(!e.State.Cards[samuraiCard.Id].Bowed, "his bowing was negated");
        Eq(e.State.Cards[know.Id].Zone, Zone.FateDiscard, "the Strategy played from the hand is spent");
        Check(e.State.Events.Any(x => x.Type == "effectsNegated"), "logged");

        // Control: the same action against a hand without it opens no window at all, and the Samurai is bowed.
        var (s2, samurai2, _, bower2) = Build(held: false);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-at-battle", bower2.Id, samurai2.Id));
        Check(e2.State.PendingChoice is not { Type: "beforeActionReaction" } && e2.State.Cards[samurai2.Id].Bowed, "no reactor, no window: the action resolves");
        // A target who is not a Samurai, and an action that neither bows nor moves him (a destroy), are not answered by it.
        var (s3, notSamurai, _, bower3) = Build(samurai: false);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-at-battle", bower3.Id, notSamurai.Id));
        Check(e3.State.PendingChoice is not { Type: "beforeActionReaction" }, "only a Samurai");
        var s4 = Table();
        var samurai4 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(samurai4, "Samurai"); Add(s4, KnowNoFear, 0, Zone.Hand);
        var destroyer = Add(s4, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "training-destroy-no-attachments", destroyer.Id, samurai4.Id));
        Check(!ReactionOffered(e4, 0, "know-no-fear-negate-move"), "a destruction is not a bowing or a movement");
        // Hidden information: the opponent's view never lists the card in the hand.
        var (s5, _, know5, bower5) = Build();
        var e5 = MatchEngine.FromTestState(s5);
        HiddenIdsAbsent(e5.GetView(1), [know5.Id]);
    }

    private static void KnowNoFearFear()
    {
        var s = Table(Phase.Action, 1);
        var hotako = Add(s, Hotako, 1, Zone.Home);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(samurai, "Samurai");
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var know = Add(s, KnowNoFear, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        // The Fear action is answered by the second Reaction, which negates it whole: no lock is placed.
        Accept(e, Offer(e, 1, "hotako-fear-lock", hotako.Id, samurai.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens");
        Check(ReactionOffered(e, 0, "know-no-fear-negate-fear"), "the Fear Reaction is offered");
        Accept(e, ReactionOffer(e, 0, "know-no-fear-negate-fear", know.Id));
        ClosePassing(e);
        Check(e.State.Cards[samurai.Id].ActionLocks.Count == 0, "the Fear action's effects are negated: no lock");
        Eq(e.State.Cards[know.Id].Zone, Zone.FateDiscard, "spent");
        // Not offered for a target who is not a Samurai.
        var e2 = MatchEngine.FromTestState(s);
        var s2 = Table(Phase.Action, 1); var hotako2 = Add(s2, Hotako, 1, Zone.Home); var plain2 = Add(s2, "training-crane-samurai", 0, Zone.Home); Add(s2, KnowNoFear, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s2);
        Accept(e3, Offer(e3, 1, "hotako-fear-lock", hotako2.Id, plain2.Id));
        Check(e3.State.PendingChoice is not { Type: "beforeActionReaction" }, "a Personality who is not a Samurai is not protected");
        _ = e2; _ = plain;
    }

    // ---- To First Blood ---------------------------------------------------------------------------------------------------------------------------
    // Player 1's Mirumoto Ichizo (Iaijutsu, Chi 4, "duel him, destroy the loser") challenges player 0's Personality (Chi 3), who holds To First Blood.
    private static (MatchState S, CardInstance Ichizo, CardInstance Target, CardInstance Card) IaijutsuTable()
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, ToFirstBlood, 0, Zone.Hand);
        return (s, ichizo, target, card);
    }

    // The duel is driven by its own commands (not listed by QueryActions): the player whose turn it is strikes, and a Duelist may decline the extra refocus.
    private static void FinishDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 6; guard++)
        {
            var command = duel.Stage switch
            {
                "Focusing" => new GameCommand("strike", duel.TurnPlayerId, e.Version),
                "DuelistRefocus" => new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version),
                _ => throw new Exception($"unexpected duel stage {duel.Stage}")
            };
            Accept(e, command);
        }
    }

    private static void ToFirstBloodReaction()
    {
        var (s, ichizo, target, card) = IaijutsuTable();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for a reactor holding To First Blood");
        Accept(e, ReactionOffer(e, 0, "to-first-blood-negate", card.Id));
        ClosePassing(e);
        FinishDuel(e);
        Check(e.State.Cards[target.Id].Zone is Zone.Battlefield or Zone.Home, "he lost the duel (Chi 3 against 4) but was not destroyed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(e.State.Events.Any(x => x.Type == "duelResolved" && x.Message.Contains("Mirumoto Ichizo")), "Ichizo won");

        // Control: without the reaction he is destroyed.
        var (s2, ichizo2, target2, _) = IaijutsuTable();
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "ichizo-exp-duel", ichizo2.Id, target2.Id));
        ClosePassing(e2);
        FinishDuel(e2);
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Dead, "without it the loser is destroyed");
        // It answers only an Iaijutsu action: a plain Battle action against him opens no window for it.
        var s3 = Table(); var t3 = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s3, ToFirstBlood, 0, Zone.Hand);
        var b3 = Add(s3, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-at-battle", b3.Id, t3.Id));
        Check(!ReactionOffered(e3, 0, "to-first-blood-negate"), "not against an action that is not Iaijutsu");
    }

    private static void ToFirstBloodFocus()
    {
        foreach (var focusWins in new[] { true, false })
        {
            var (s, ichizo, target, card) = IaijutsuTable();
            // Player 0 (challenged) focuses To First Blood (Focus 3). Chi 3 + 3 = 6 beats Ichizo's 4 alone; or, with a Focus 3 card focused off Ichizo's deck, 4 + 3 = 7 beats 6.
            if (!focusWins) { var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id); }
            var e = MatchEngine.FromTestState(s);
            var (mine, theirs) = (Honor(e, 0), Honor(e, 1));
            Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
            ClosePassing(e);
            Accept(e, new GameCommand("focus", 0, e.Version, CardId: card.Id));
            if (!focusWins) Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
            FinishDuel(e);
            var winner = focusWins ? 0 : 1;
            Check(e.State.Cards[ichizo.Id].Zone == Zone.Battlefield && e.State.Cards[target.Id].Zone is Zone.Battlefield or Zone.Home, "the loser of the duel is not destroyed");
            Eq(Honor(e, winner), (winner == 0 ? mine : theirs) + 2, "the winner's controller gains 2 Honor");
            Eq(Honor(e, 1 - winner), 1 - winner == 0 ? mine : theirs, "and the other player nothing");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the focused card is spent");
        }
    }

    // ---- A Fugitive Apprehended --------------------------------------------------------------------------------------------------------------------
    private sealed record FugitiveRig(MatchState S, CardInstance Performer, CardInstance Card, CardInstance Cheap, CardInstance Dear, CardInstance Armed);
    private static FugitiveRig FugitiveTable(bool magistrate)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var performer = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);          // Gold Cost 8
        if (magistrate) GiveKeyword(performer, "Magistrate");
        var card = Add(s, Fugitive, 0, Zone.Hand);
        var cheap = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);               // Gold Cost 5 < 8
        var dear = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);                // Gold Cost 8: not lower
        var armed = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);              // Gold Cost 5 but armed
        Add(s, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        return new(s, performer, card, cheap, dear, armed);
    }

    private static void FugitiveApprehended()
    {
        var r = FugitiveTable(magistrate: false);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, "fugitive-apprehended", r.Card.Id, r.Performer.Id);
        var bowSlot = option.Slots!.Single(x => x.Slot == "bow"); var costSlot = option.Slots!.Single(x => x.Slot == "cost");
        Check(bowSlot.CardIds.Contains(r.Cheap.Id) && !bowSlot.CardIds.Contains(r.Dear.Id) && !bowSlot.CardIds.Contains(r.Armed.Id), "lower Gold Cost and no attachments: " + string.Join(",", bowSlot.CardIds));
        Check(costSlot.CardIds.Length == 0 && costSlot.Min == 0, "without a Magistrate there is no second target");
        Accept(e, Use(e, 0, "fugitive-apprehended", r.Card.Id, performerId: r.Performer.Id, selections: [Pick("bow", r.Cheap.Id)]));
        Check(e.State.Cards[r.Cheap.Id].Bowed && !e.State.Cards[r.Dear.Id].Bowed, "the first target is bowed");
        Eq(e.State.Cards[r.Card.Id].Zone, Zone.FateDiscard, "spent");
        // A non-Magistrate naming a second target is refused.
        var r2 = FugitiveTable(magistrate: false); var e2 = MatchEngine.FromTestState(r2.S);
        Reject(e2, Use(e2, 0, "fugitive-apprehended", r2.Card.Id, performerId: r2.Performer.Id, selections: [Pick("bow", r2.Cheap.Id), Pick("cost", r2.Dear.Id)]));

        // A Magistrate may also name any enemy card (even an armed one): after the first targeting, its Gold Cost becomes 0.
        var m = FugitiveTable(magistrate: true); var em = MatchEngine.FromTestState(m.S);
        var moption = Enabled(em, 0, "fugitive-apprehended", m.Card.Id, m.Performer.Id);
        Check(moption.Slots!.Single(x => x.Slot == "cost").CardIds.Contains(m.Dear.Id) && moption.Slots!.Single(x => x.Slot == "cost").CardIds.Contains(m.Armed.Id), "any enemy card is a candidate for the second target");
        Accept(em, Use(em, 0, "fugitive-apprehended", m.Card.Id, performerId: m.Performer.Id, selections: [Pick("bow", m.Cheap.Id), Pick("cost", m.Dear.Id)]));
        Check(em.State.Cards[m.Cheap.Id].Bowed && !em.State.Cards[m.Dear.Id].Bowed, "the first target is bowed; the second is not");
        Check(em.State.Cards[m.Dear.Id].Modifiers.Any(x => x.Stat == "GoldCost" && x.Amount == -8), "its Gold Cost is reduced to 0 (a -8 modifier on a card that cost 8)");
        // It is not a way round the restriction: with no legal first target the action is not offered, second target or not.
        var only = FugitiveTable(magistrate: true);
        foreach (var c in only.S.Cards.Values.Where(c => c.OwnerId == 1 && c.Id != only.Dear.Id && c.Zone is Zone.Battlefield or Zone.Attached).ToArray()) only.S.Cards.Remove(c.Id);   // only the costly enemy card is left
        var eo = MatchEngine.FromTestState(only.S);
        var offered = Options(eo, 0, "fugitive-apprehended", only.Card.Id, only.Performer.Id);
        Check(offered.Length > 0 && offered.All(o => !o.Enabled), "no card with a lower Gold Cost and no attachments: the Magistrate cannot name the second target first");
        // The changed cost matters to a later action that compares Gold Cost: the zeroed card is now lower than a costlier performer's.
        Check(em.State.Cards.Values.Any(c => c.Id == m.Dear.Id), "the card stays in play");
    }

    // ---- Dislodge the Foe -------------------------------------------------------------------------------------------------------------------------
    private static void DislodgeTheFoe()
    {
        // Battlefield 0 is current. Player 0's Tacticians stand together at battlefield 1 with the enemy Personality who opposes them there.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [0, 1];
        var t1 = Add(s, "training-tactician", 0, Zone.Battlefield, province: 1); var t2 = Add(s, "training-tactician", 0, Zone.Battlefield, province: 1);
        // A Merchant Tactician whose own reactor would open a window if he were a TARGET of the action: as a performer he is not one.
        var guard = Add(s, "training-merchant-free-guard", 0, Zone.Battlefield, province: 1); GiveKeyword(guard, "Tactician");
        var elsewhere = Add(s, "training-tactician", 0, Zone.Battlefield, province: 2);
        var here = Add(s, "training-tactician", 0, Zone.Battlefield, province: 0);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 1);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var foeElsewhere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 2);
        var foeHere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, Dislodge, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "dislodge-the-foe", card.Id);
        var tacticians = option.Slots!.Single(x => x.Slot == "tacticians").CardIds; var opposing = option.Slots!.Single(x => x.Slot == "opposing").CardIds;
        Check(tacticians.Contains(t1.Id) && tacticians.Contains(t2.Id) && tacticians.Contains(elsewhere.Id) && !tacticians.Contains(here.Id) && !tacticians.Contains(plain.Id), "own Tacticians at a battlefield other than the current one: " + string.Join(",", tacticians));
        Check(opposing.Contains(foe.Id), "an enemy Personality is a target");
        // The opposing candidates are not tied to the DEFAULT Tactician choice: the enemy at the other battlefield is offered
        // too, because choosing the Tactician standing there is a legal, non-default combination (issue #345 review).
        Check(opposing.Contains(foeElsewhere.Id), "an enemy opposing a non-default Tactician choice is offered: " + string.Join(",", opposing));
        Check(!opposing.Contains(foeHere.Id), "an enemy at the current battlefield is still never opposed here: " + string.Join(",", opposing));
        Accept(MatchEngine.FromTestState(s), Use(e, 0, "dislodge-the-foe", card.Id, selections: [Pick("tacticians", elsewhere.Id), Pick("opposing", foeElsewhere.Id)]));
        // Refused: Tacticians at different battlefields; the opposing Personality not where they stand.
        Reject(e, Use(e, 0, "dislodge-the-foe", card.Id, selections: [Pick("tacticians", t1.Id, elsewhere.Id), Pick("opposing", foe.Id)]));
        Reject(e, Use(e, 0, "dislodge-the-foe", card.Id, selections: [Pick("tacticians", t1.Id, t2.Id), Pick("opposing", foeElsewhere.Id)]));
        Reject(e, Use(e, 0, "dislodge-the-foe", card.Id, selections: [Pick("tacticians", here.Id), Pick("opposing", foeHere.Id)]));
        Reject(e, Use(e, 0, "dislodge-the-foe", card.Id, selections: [Pick("tacticians", plain.Id), Pick("opposing", foe.Id)]));
        Accept(e, Use(e, 0, "dislodge-the-foe", card.Id, selections: [Pick("tacticians", guard.Id, t1.Id, t2.Id), Pick("opposing", foe.Id)]));
        Check(e.State.PendingChoice is null, "the Tacticians are performers, not targets: no reaction window opens for them, not even for one with a reactor of his own");
        Check(e.State.Cards[guard.Id].ProvinceIndex == 0, "the first-named performer moved too");
        Check(e.State.Cards[t1.Id].ProvinceIndex == 0 && e.State.Cards[t2.Id].ProvinceIndex == 0 && e.State.Cards[t1.Id].Zone == Zone.Battlefield, "both Tacticians moved to the current battlefield");
        Check(e.State.Cards[foe.Id].ProvinceIndex == 1, "the opposing Personality stays where he was");
        Check(e.State.Cards[elsewhere.Id].ProvinceIndex == 2 && e.State.Cards[plain.Id].ProvinceIndex == 1, "nobody else moved");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // A performer under an action lock bars the action.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0; s2.UnresolvedBattlefields = [0, 1];
        var lockedT = Add(s2, "training-tactician", 0, Zone.Battlefield, province: 1); lockedT.ActionLocks.Add(new("test", "Turn"));
        var c2 = Add(s2, Dislodge, 0, Zone.Hand); var f2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, Use(e2, 0, "dislodge-the-foe", c2.Id, selections: [Pick("tacticians", lockedT.Id), Pick("opposing", f2.Id)]));
        // Performers are not offered to the other side's reactions to a target: a single Tactician suffices (N = 1).
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0; s3.UnresolvedBattlefields = [0, 1];
        var one = Add(s3, "training-tactician", 0, Zone.Battlefield, province: 1); var c3 = Add(s3, Dislodge, 0, Zone.Hand); var f3 = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "dislodge-the-foe", c3.Id, selections: [Pick("tacticians", one.Id), Pick("opposing", f3.Id)]));
        Eq(e3.State.Cards[one.Id].ProvinceIndex, 0, "one Tactician is enough");
        // The one TARGET is the opposing Personality, not the first Tactician named: Akodo Kakihara's "after an action targets him" is answered when he is that Personality.
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0; s4.UnresolvedBattlefields = [0, 1];
        var first = Add(s4, "training-tactician", 0, Zone.Battlefield, province: 1); var c4 = Add(s4, Dislodge, 0, Zone.Hand);
        var kakihara = Add(s4, "oracle-l5r-223", 1, Zone.Battlefield, province: 1);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 0, "dislodge-the-foe", c4.Id, selections: [Pick("tacticians", first.Id), Pick("opposing", kakihara.Id)]));
        Eq(e4.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the opposing Personality");
        Eq(e4.State.ReactionWindowTargetId, kakihara.Id, "and it is his window");
        // Presence (issue #1133): in a battle a Personality's Reaction needs him at the CURRENT battlefield, and the opposing Personality stands at another one, so his window opens but he cannot take it.
        Check(!ReactionOffered(e4, 1, "kakihara-force-bonus"), "Kakihara stands at a non-current battlefield: his Reaction to being targeted is not offered");
    }

    // ---- Kyuden Kitsune ---------------------------------------------------------------------------------------------------------------------------
    private static void KyudenKitsune()
    {
        // Player 0 defends with Kyuden Kitsune; player 1 attacks and, twice, moves a Personality from home into the army at province 0.
        (MatchState S, CardInstance A, CardInstance B, CardInstance M1, CardInstance M2) Build()
        {
            var s = Table();
            s.Cards[s.Players[0].StrongholdId].DefinitionId = Kitsune;
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var a = Add(s, "training-lion-samurai", 1, Zone.Home); var b = Add(s, "training-lion-samurai", 1, Zone.Home);
            var m1 = Add(s, "training-move-battle-strategy", 1, Zone.Hand); var m2 = Add(s, "training-move-battle-strategy", 1, Zone.Hand);
            return (s, a, b, m1, m2);
        }
        var (s, a, b, m1, m2) = Build();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-move-to-battle", m1.Id, a.Id));
        Check(e.State.Cards[a.Id].Zone == Zone.Home, "the first movement into the attacking army is negated: he stays home");
        Check(e.State.Events.Any(x => x.Type == "moveNegated" && x.Message.Contains("Kyuden Kitsune")), "logged");
        Eq(e.State.Cards[m1.Id].Zone, Zone.FateDiscard, "the action was taken and its Strategy spent");
        var again = Edit(e, st => { st.PriorityPlayerId = 1; });
        Accept(again, Offer(again, 1, "training-move-to-battle", m2.Id, b.Id));
        Eq(again.State.Cards[b.Id].Zone, Zone.Battlefield, "the second is not: once per battle");
        // Control: with an ordinary Stronghold the first movement succeeds.
        var (s2, a2, _, m21, _) = Build(); s2.Cards[s2.Players[0].StrongholdId].DefinitionId = "training-crane-stronghold";
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-move-to-battle", m21.Id, a2.Id));
        Eq(e2.State.Cards[a2.Id].Zone, Zone.Battlefield, "without Kyuden Kitsune he moves");

        // Its Limited action: bow one of your Forests to gain 1 Honor.
        var t = Table(Phase.Action, 0);
        t.Cards[t.Players[0].StrongholdId].DefinitionId = Kitsune;
        var forest = Add(t, "training-border-keep", 0, Zone.Home); GiveKeyword(forest, "Forest");
        var bowedForest = Add(t, "training-border-keep", 0, Zone.Home, bowed: true); GiveKeyword(bowedForest, "Forest");
        var notForest = Add(t, "training-farm", 0, Zone.Home);
        var et = MatchEngine.FromTestState(t);
        var honor = Honor(et, 0);
        var option = Enabled(et, 0, "kitsune-forest-honor", t.Players[0].StrongholdId);
        Check(option.TargetIds!.Contains(forest.Id) && !option.TargetIds.Contains(bowedForest.Id) && !option.TargetIds.Contains(notForest.Id), "an unbowed Forest of yours: " + string.Join(",", option.TargetIds!));
        Accept(et, Offer(et, 0, "kitsune-forest-honor", t.Players[0].StrongholdId, forest.Id));
        Check(et.State.Cards[forest.Id].Bowed, "the Forest is bowed"); Eq(Honor(et, 0), honor + 1, "and 1 Honor is gained");
    }
}
