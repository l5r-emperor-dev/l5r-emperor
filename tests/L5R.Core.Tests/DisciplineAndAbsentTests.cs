using static RegressionAssertions;
using L5R.Core;

// Discipline as a play mode and Absent as an ability flag (issue #331), proven on Return to Action (strategy-wave-18) and
// on the wired cards migrated onto the flags: Banzai! and Lost in Transit (Discipline), Ring of Earth, Ring of Water and
// False Ring of Air (Absent). Each test builds an explicit position and asserts the positive path and the cases that must
// NOT be allowed; see docs/decisions/2026-09-19-discipline-and-absent-flags.md. Return to Action's table: player 1 is the
// Attacker (ActivePlayerId), player 0 the Defender, the current battlefield is the Defender's province 0, and player 0 is
// the one with Gold (the economy scenario's Crane marketplace and Border Keep).
internal static class DisciplineAndAbsentTests
{
    private const string ReturnToAction = "oracle-l5r-10953", Banzai = "oracle-l5r-10929", LostInTransit = "oracle-l5r-10945";
    private const string Rta = "return-to-action-move";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Return to Action is the printed card (Focus 3, no Gold Cost box, the Discipline cost as GoldCost), on both flags; the flag inventory is pinned and no ability keeps the old Discipline timing", PublishedShapeAndFlagInventory),
            ("Return to Action from HAND: free (no quote, no sources), the Defender moves a bowed or unbowed Personality from home to the battlefield with his attachments, straightens him (and only him), and the card ends in the Fate discard pile", HandPlayMovesAndStraightens),
            ("Return to Action: with NO unit at the battlefield (Absent) the moved Personality then fights in the defending army, and only because he was straightened does his Force count", AbsentAndFightsInTheArmy),
            ("Return to Action: the Attacker is rejected and shown disabled; the same position with the roles swapped is accepted", DefenderOnly),
            ("Return to Action: the target must be your OWN Personality AT HOME (a battlefield Personality, an opposing one, a dead one, a Holding, an attachment: rejected); exactly the legal ones are offered", TargetMustBeOwnAtHome),
            ("Return to Action: Battle phase only -- an Absent Battle action is still a Battle action (not offered and rejected in the Action phase)", BattlePhaseOnly),
            ("Return to Action from the DISCARD pile: the offer carries the 2 Gold Discipline quote, the payment is charged, the move and straighten resolve and the card is removed from the game", DiscardPlayPaysAndIsRemoved),
            ("Return to Action from the discard pile: insufficient Gold is disabled and rejected, an unpaid or over-sourced command is rejected, and a removed card can never be played again", DiscardPlayCostRules),
            ("Return to Action: a card in hand is offered only as the free play and a card in the discard pile only as the paid one (never both modes for one card)", ModesAreExclusive),
            ("Return to Action: once per turn per card -- played from hand it cannot be replayed from the discard pile the same turn (Aftermath supplement), but can on a later turn", SameTurnReplayRule),
            ("The same-turn replay rule is per ACTION, not per card (Mendon's ruling, issue #333): a Discipline card with two actions can play its other action from the discard pile the turn the first was played from hand, but not the same one", SameTurnReplayIsPerAction),
            ("Return to Action: a negated movement (Tamori Tamoko) leaves him at home and bowed -- no straighten -- while the card is still spent (hand: discarded; discard pile: paid and removed); declining lets it resolve", NegatedMovement),
            ("Return to Action: every enabled offered command (hand and discard copies, every offered target) validates on a fresh replay", OfferedCommandsAllValidate),
            ("Banzai! and Lost in Transit from HAND (newly possible): the ordinary free play, ending in the Fate discard pile; the printed Battle verb still gates Banzai!, and Battle/Open in a battle needs presence", HandPlayOfExistingDisciplineCards),
            ("Absent flag parity: Ring of Earth (no unit needed, Battle phase only), Ring of Water's move-home (its target is your own unit, so presence was already implied) and False Ring of Air (newly Absent: an enemy Personality is targetable with no unit of your own)", AbsentFlagParity),
        ];
        runner.Run("discipline-absent", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"da-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private sealed record Rig(MatchState S, CardInstance Card, CardInstance BowedHome, CardInstance UnbowedHome);

    // The Defender (player 0) holds Return to Action in `zone` with two Personalities at home, one bowed (Force 3) and one
    // unbowed (Force 5); the Attacker (player 1) has a Force 6 unit at the current battlefield, the Defender's province 0.
    // The Defender has NO unit at the battlefield: moving one in is the point of the card.
    private static Rig Table(Zone zone = Zone.Hand, Phase phase = Phase.Battle)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = phase; s.ActivePlayerId = phase == Phase.Battle ? 1 : 0; s.PriorityPlayerId = 0;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        var card = Add(s, ReturnToAction, 0, zone);
        var bowed = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var unbowed = Add(s, "training-crane-veteran", 0, Zone.Home);
        if (phase == Phase.Battle) Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        return new(s, card, bowed, unbowed);
    }

    private static GameCommand Use(MatchEngine e, CardInstance card, string ability, string? target = null, string[]? sources = null, int player = 0) =>
        new("useAbility", player, e.Version, CardId: card.Id, TargetId: target, AbilityId: ability, SourceIds: sources);

    private static ActionOption? Offer(MatchEngine e, CardInstance card, int player = 0) =>
        e.QueryActions(player).SingleOrDefault(a => a.Type == "useAbility" && a.CardId == card.Id);

    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);
    private static string[] TwoGold(MatchEngine e) => e.PaymentOptions(0, 2).FirstOrDefault()?.SourceIds ?? [];
    private static bool Paid(MatchEngine e, int gold) => e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains($"paid {gold} Gold"));
    private static bool AnyPayment(MatchEngine e) => e.State.Events.Any(x => x.Type == "payment");

    // ---- published shape and the flag inventory -----------------------------------------------------------------
    private static void PublishedShapeAndFlagInventory()
    {
        var c = CardCatalog.Get(ReturnToAction);
        Eq(PublishedContent.StrategyWave18.Cards.Length, 1, "strategy-wave-18 is exactly Return to Action");
        Check(!c.ScenarioOnly && c.Type == CardType.Strategy);
        Eq(c.Name, "Return to Action"); Eq(c.Focus, 3);
        Eq(c.GoldCost, 2, "the published GoldCost of a Discipline card holds the DISCIPLINE cost (Banzai! 2, Lost in Transit 1)");
        Eq(c.Text, "Discipline :g2: (You may pay 2 Gold to play this Discipline from your discard pile, then remove it from the game after its action resolves.)\nAbsent Battle: If you are the Defender, target your Personality at home and move him to this battlefield. After he moves, straighten him.");
        Eq(string.Join(",", c.Abilities!), Rta); Eq(c.Effect, "none");
        var ability = AbilityCatalog.All[Rta];
        Eq(ability.Timing, "Battle", "the timing is the printed verb; Discipline is a flag, not a timing");
        Check(ability.Discipline && ability.Absent);
        Eq(ability.Target, "own-personality-at-home");

        // The flag inventory: exactly what was migrated (and munefusa-move, which took the old move-ability id list's no-presence clause; nothing else), so a new flag on a card is a deliberate edit.
        string Ids(Func<AbilityDefinition, bool> flag) => string.Join(",", AbilityCatalog.All.Where(p => flag(p.Value)).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal));
        Eq(Ids(a => a.Discipline), "banzai,expensive-achievement-lower,expensive-achievement-raise,knowledge-and-power,lost-in-transit-bow,mercantile-conflict-bow,return-to-action-move,training-discipline-ability,training-discipline-counter-negate,two-steady-breaths-destined");
        // isul-bow/isul-straighten (Moto Isul), junpei-attack (Shinjo Junpei - exp), makoto-lock (Yasuki Makoto) and miro-create (Kitsu Miro)
        // are issue #460 Embers of War pass, batch 2; incite-phantoms-bow (Incite the Phantoms) and sudden-guardian-move (Sudden Guardian) are
        // batch 5 -- each printed "Even if [card] is at home" or "Home Battle", the same presence waiver. never-beyond-reach-attack (Never Beyond My Reach) and
        // siege-volley-raise/-lower (Siege Volley) are the Forgotten Legacy pass (issue #467), batch 4: "Even if you control no units at the current battlefield" / "Siege Absent Battle".
        // defensive-grill-move (Defensive Grill) is The Shadow's Embrace pass (issue #476), batch 3: "Even if you control no units at the current battlefield".
        // tfp-laoshe-move (Lao-she, issue #622, TfPersonalities.cs): "Home Battle" -- Home actions may be taken from home.
        Eq(Ids(a => a.Absent), "back-to-the-front-move,breaking-rhythm-bonuses,breaking-rhythm-penalties,defensive-grill-move,disgraceful-conduct-bow,disgraceful-conduct-bow-unit,false-ring-of-air-force,false-ring-of-air-recruit,false-ring-of-earth-recruit,heavily-engaged-bow,hidden-defenses-bow,incite-phantoms-bow,indomitable-home-strength,isul-bow,isul-straighten,junpei-attack,makoto-lock,miro-create,munefusa-move,never-beyond-reach-attack,p2-turning-away-lower,p2-turning-away-raise,p5-perplexing-guests-create,p5-searching-look,p5-tokiko-battlefield,p5-yutaka-return,p5-yutaka-return-dark,pe-re-fortification-move,return-to-action-move,ring-of-air-recruit,ring-of-earth-lower,ring-of-earth-raise,ring-of-earth-recruit,ring-of-fire-recruit,ring-of-water-move-home,ring-of-water-recruit,shadows-lair-move,shameful-injury-negate,siege-volley-lower,siege-volley-raise,stone-hewed-shrine-strength,sudden-guardian-move,tfp-laoshe-move,w835-warded-play,wl-false-ring-of-void-recruit,wl-sg-recovering-lost,wl-sg-tactical-setback,wlsac-way-of-the-world,yasuki-palaces-move"); // shadows-lair-move / yasuki-palaces-move: Emperor Edition Stronghold pass (issue #486), batch 3 -- "even if you control no units at the current battlefield"; heavily-engaged-bow and hidden-defenses-bow: Emperor Edition remaining-cards pass (issue #492, group B), "Even if you control no units at the current battlefield" / with no Personality of yours in play, hence no unit; p2-turning-away-raise/-lower (Turning Away, issue #503): "Even if you control no units at the current battlefield"
        Check(!AbilityCatalog.All.Values.Any(a => a.Timing == "Discipline"), "no ability keeps the old Discipline timing");
        Eq(AbilityCatalog.All["banzai"].Timing, "Battle"); Eq(AbilityCatalog.All["lost-in-transit-bow"].Timing, "BattleOrOpen");
    }

    // ---- Return to Action from hand ------------------------------------------------------------------------------
    private static void HandPlayMovesAndStraightens()
    {
        foreach (var bowedTarget in new[] { true, false })
        {
            var rig = Table();
            var target = bowedTarget ? rig.BowedHome : rig.UnbowedHome;
            var follower = Add(rig.S, "training-follower", 0, Zone.Attached, bowed: true, parent: target.Id);
            var gold = rig.S.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).Select(c => c.Id).ToArray();
            var e = MatchEngine.FromTestState(rig.S);
            var option = Offer(e, rig.Card)!;
            Check(option.Enabled, option.Reason ?? "");
            Check(option.Gold is null, "the hand play has no Gold quote");
            Check(option.Command!.SourceIds is null or { Length: 0 }, "and carries no sources");
            Check(option.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { rig.BowedHome.Id, rig.UnbowedHome.Id }.OrderBy(x => x)), "both home Personalities are legal targets, bowed or not");
            // Free means free: a payment offered for it is rejected outright.
            Reject(e, Use(e, rig.Card, Rta, target.Id, TwoGold(e)));
            Accept(e, Use(e, rig.Card, Rta, target.Id));
            var moved = e.State.Cards[target.Id];
            Eq(moved.Zone, Zone.Battlefield); Eq(moved.ProvinceIndex, (int?)0, "moved to the current battlefield");
            Check(!moved.Bowed, "straightened after he moved");
            Eq(e.State.Cards[follower.Id].AttachedTo, target.Id); Eq(e.State.Cards[follower.Id].ProvinceIndex, (int?)0, "the whole unit moved: attachments travel");
            Check(e.State.Cards[follower.Id].Bowed, "he is straightened, not his unit");
            Eq(e.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard, "an ordinary Strategy goes to the discard pile afterward");
            Check(!AnyPayment(e) && gold.All(id => !e.State.Cards[id].Bowed), "free: no payment event and no Gold source bowed");
            Check(e.GetView(0).Players[0].Army.Any(c => c.Id == target.Id), "he is now in the defending army");
            // The other home Personality is untouched.
            var other = e.State.Cards[(bowedTarget ? rig.UnbowedHome : rig.BowedHome).Id];
            Eq(other.Zone, Zone.Home);
        }
    }

    private static void AbsentAndFightsInTheArmy()
    {
        var rig = Table();
        Eq(rig.S.Cards.Values.Count(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield), 0, "precondition: the Defender has no unit at the battlefield");
        var e = MatchEngine.FromTestState(rig.S);
        // Nothing else is needed for the play to be legal: Absent waives the Defender's presence.
        Accept(e, Use(e, rig.Card, Rta, rig.BowedHome.Id));
        Accept(e, Pass(e)); Accept(e, Pass(e));
        var message = e.State.Events.Last(x => x.Type == "battleResolved").Message;
        Check(message.Contains("attacking Force 6, defending Force 3"), $"he fights, and unbowed: {message}");
    }

    private static void DefenderOnly()
    {
        var rig = Table();
        rig.S.ActivePlayerId = 0; // player 0 is now the ATTACKER of the same battle
        Add(rig.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // nothing else stands in the way: they even have a unit there
        var attacker = MatchEngine.FromTestState(rig.S);
        var option = Offer(attacker, rig.Card)!;
        Check(!option.Enabled && option.Reason!.Contains("Defender"), option.Reason ?? "");
        Reject(attacker, Use(attacker, rig.Card, Rta, rig.UnbowedHome.Id));
        Eq(attacker.State.Cards[rig.UnbowedHome.Id].Zone, Zone.Home);

        var defenderTable = Table();
        var defender = MatchEngine.FromTestState(defenderTable.S);
        Check(Offer(defender, defenderTable.Card)!.Enabled, "the identical hand, home and battlefield are legal for the Defender");
    }

    private static void TargetMustBeOwnAtHome()
    {
        var rig = Table();
        var onBattlefield = Add(rig.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var opposing = Add(rig.S, "training-lion-samurai", 1, Zone.Home);
        var dead = Add(rig.S, "training-crane-courtier", 0, Zone.Dead);
        var holding = Add(rig.S, "training-marketplace", 0, Zone.Home);
        var attachment = Add(rig.S, "training-item", 0, Zone.Attached, parent: rig.UnbowedHome.Id);
        var inProvince = Add(rig.S, "training-crane-courtier", 0, Zone.Province);
        var e = MatchEngine.FromTestState(rig.S);
        foreach (var bad in new[] { onBattlefield, opposing, dead, holding, attachment, inProvince, rig.Card })
            Reject(e, Use(e, rig.Card, Rta, bad.Id));
        var option = Offer(e, rig.Card)!;
        Check(option.Enabled, option.Reason ?? "");
        Check(option.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { rig.BowedHome.Id, rig.UnbowedHome.Id }.OrderBy(x => x)), $"offered exactly the own home Personalities: {string.Join(",", option.TargetIds!)}");
        Accept(e, Use(e, rig.Card, Rta, rig.UnbowedHome.Id));
        Eq(e.State.Cards[onBattlefield.Id].Zone, Zone.Battlefield, "no other card moved");
    }

    private static void BattlePhaseOnly()
    {
        var rig = Table(phase: Phase.Action);
        // Leave nothing else to reject it: player 0 is the non-active side and a battlefield is (artificially) set, so
        // the Defender rule and "no current battlefield" would both pass -- only the Battle-phase timing can refuse.
        rig.S.ActivePlayerId = 1; rig.S.CurrentBattleProvince = 0; rig.S.UnresolvedBattlefields = [0];
        var e = MatchEngine.FromTestState(rig.S);
        Check(Offer(e, rig.Card) is null or { Enabled: false }, "not playable in the Action phase");
        Reject(e, Use(e, rig.Card, Rta, rig.BowedHome.Id));
        var discardCopy = Table(Zone.FateDiscard, Phase.Action);
        discardCopy.S.ActivePlayerId = 1; discardCopy.S.CurrentBattleProvince = 0; discardCopy.S.UnresolvedBattlefields = [0];
        var d = MatchEngine.FromTestState(discardCopy.S);
        Reject(d, Use(d, discardCopy.Card, Rta, discardCopy.BowedHome.Id, TwoGold(d)));
        // The same position in the Battle phase is legal (control).
        var battle = Table();
        var b = MatchEngine.FromTestState(battle.S);
        Accept(b, Use(b, battle.Card, Rta, battle.BowedHome.Id));
    }

    // ---- Return to Action from the discard pile -----------------------------------------------------------------
    private static void DiscardPlayPaysAndIsRemoved()
    {
        var rig = Table(Zone.FateDiscard);
        var e = MatchEngine.FromTestState(rig.S);
        var option = Offer(e, rig.Card)!;
        Check(option.Enabled, option.Reason ?? "");
        Eq(option.Gold?.Kind, GoldCostKind.Fixed); Eq(option.Gold!.Cost, 2);
        Check(option.Gold.Basis.Contains("Discipline cost 2"), option.Gold.Basis);
        Check(option.Label.Contains("Discipline (removed from the game)"), $"the discard-pile play says what it is: {option.Label}");
        var handRig = Table();
        Check(!Offer(MatchEngine.FromTestState(handRig.S), handRig.Card)!.Label.Contains("Discipline"), "the hand play does not");
        Check(option.Gold.Payments.Length > 0 && option.Gold.Error is null);
        Check(option.Command!.SourceIds!.SequenceEqual(option.Gold.Payments[0].SourceIds), "the command carries the engine's suggested sources");
        Accept(e, option.Command with { TargetId = rig.BowedHome.Id });
        Check(option.Command.SourceIds!.All(id => e.State.Cards[id].Bowed), "exactly the offered sources bowed");
        Check(Paid(e, 2), "the payment event names the Discipline cost");
        var moved = e.State.Cards[rig.BowedHome.Id];
        Eq(moved.Zone, Zone.Battlefield); Eq(moved.ProvinceIndex, (int?)0); Check(!moved.Bowed, "straightened");
        Eq(e.State.Cards[rig.Card.Id].Zone, Zone.RemovedFromGame, "removed from the game after its action resolved, not left in the discard pile");
        Check(e.GetView(0).Players[0].RemovedFromGame.Any(c => c.Id == rig.Card.Id), "visible in the Removed-from-Game pile");
    }

    private static void DiscardPlayCostRules()
    {
        // Insufficient Gold: offered disabled with the shortfall, and a command without the payment is rejected.
        var rig = Table(Zone.FateDiscard);
        foreach (var c in rig.S.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        var poor = MatchEngine.FromTestState(rig.S);
        var disabled = Offer(poor, rig.Card)!;
        Check(!disabled.Enabled && disabled.Command is null, "unaffordable: offered disabled, with no command");
        Reject(poor, Use(poor, rig.Card, Rta, rig.UnbowedHome.Id));
        var shortfall = poor.Execute(Use(poor, rig.Card, Rta, rig.UnbowedHome.Id));
        Check(!shortfall.Accepted && shortfall.Error!.Contains("needs 2 Gold"), $"the refusal is the Discipline cost, not the target: {shortfall.Error}");

        var funded = Table(Zone.FateDiscard);
        var e = MatchEngine.FromTestState(funded.S);
        Reject(e, Use(e, funded.Card, Rta, funded.UnbowedHome.Id));                                     // the Discipline cost unpaid
        Reject(e, Use(e, funded.Card, Rta, funded.UnbowedHome.Id, [e.PaymentOptions(0, 2).First().SourceIds[0], e.PaymentOptions(0, 2).First().SourceIds[0]])); // a source twice
        Eq(e.State.Cards[funded.UnbowedHome.Id].Zone, Zone.Home);
        var pay = TwoGold(e);
        Accept(e, Use(e, funded.Card, Rta, funded.UnbowedHome.Id, pay));

        // Removed from the game: it never returns to a discard pile, is never offered again and can never be replayed.
        Accept(e, Pass(e));                                                                              // the Attacker passes; priority returns
        var later = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == funded.Card.Id).ToArray();
        Check(later.Length == 0 || later.All(a => !a.Enabled), "a removed card is not offered");
        Reject(e, Use(e, funded.Card, Rta, funded.BowedHome.Id, TwoGold(e)));
        Eq(e.State.Cards[funded.Card.Id].Zone, Zone.RemovedFromGame);
    }

    private static void ModesAreExclusive()
    {
        var rig = Table();                                                                               // a copy in hand
        var inDiscard = Add(rig.S, ReturnToAction, 0, Zone.FateDiscard);                                 // and a second copy in the discard pile
        var e = MatchEngine.FromTestState(rig.S);
        var options = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId is not null && (a.CardId == rig.Card.Id || a.CardId == inDiscard.Id)).ToArray();
        Eq(options.Length, 2, "one option per copy, never two modes for one card");
        var hand = options.Single(a => a.CardId == rig.Card.Id); var pile = options.Single(a => a.CardId == inDiscard.Id);
        Check(hand.Enabled && hand.Gold is null, "the hand copy is the free play");
        Check(pile.Enabled && pile.Gold is { Cost: 2 }, "the discard copy is the paid Discipline play");
        Reject(e, Use(e, rig.Card, Rta, rig.BowedHome.Id, pile.Command!.SourceIds));                    // the hand copy takes no payment
        Reject(e, Use(e, inDiscard, Rta, rig.BowedHome.Id));                                             // the discard copy needs its payment
        Accept(e, hand.Command! with { TargetId = rig.BowedHome.Id });
        Eq(e.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard); Eq(e.State.Cards[inDiscard.Id].Zone, Zone.FateDiscard, "the other copy is untouched");
        Check(!AnyPayment(e));
    }

    private static void SameTurnReplayRule()
    {
        // The real sequence: hand play, then the same card (now in the discard pile) the same turn -- rejected, because
        // the card's own once-per-turn use is spent (Aftermath supplement: "a Discipline card generally cannot be played
        // from the discard pile on the same turn it was played from hand").
        var rig = Table();
        var e = MatchEngine.FromTestState(rig.S);
        Accept(e, Use(e, rig.Card, Rta, rig.BowedHome.Id));
        Eq(e.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard);
        Accept(e, Pass(e));                                                                              // priority comes back to the Defender
        Check(Offer(e, rig.Card) is null or { Enabled: false }, "not offered again the same turn");
        Reject(e, Use(e, rig.Card, Rta, rig.UnbowedHome.Id, TwoGold(e)));
        Eq(e.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard, "still in the discard pile: the rejected replay changed nothing");

        // A later turn: the stamp is stale, so the discard-pile play is available (and then removes the card from the game).
        var later = Table(Zone.FateDiscard);
        later.Card.AbilityUses[Rta] = later.S.TurnNumber - 1;
        var l = MatchEngine.FromTestState(later.S);
        Accept(l, Use(l, later.Card, Rta, later.UnbowedHome.Id, TwoGold(l)));
        Eq(l.State.Cards[later.Card.Id].Zone, Zone.RemovedFromGame);
        // ...and the same-turn stamp on a discard-pile card blocks it (control for the line above).
        var sameTurn = Table(Zone.FateDiscard);
        sameTurn.Card.AbilityUses[Rta] = sameTurn.S.TurnNumber;
        var st = MatchEngine.FromTestState(sameTurn.S);
        Reject(st, Use(st, sameTurn.Card, Rta, sameTurn.UnbowedHome.Id, TwoGold(st)));
    }

    private static void SameTurnReplayIsPerAction()
    {
        const string Other = "training-discipline-ability";
        var rig = Table();
        rig.Card.DefinitionId = "training-two-action-discipline";
        var e = MatchEngine.FromTestState(rig.S);
        Accept(e, Use(e, rig.Card, Rta, rig.BowedHome.Id));                                              // the first action, from hand (free)
        Eq(e.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard);
        Accept(e, Pass(e));
        Reject(e, Use(e, rig.Card, Rta, rig.UnbowedHome.Id, TwoGold(e)));                                // the same action, same turn: refused
        var honor = e.State.Players[0].Honor;
        Accept(e, Use(e, rig.Card, Other, null, TwoGold(e)));                                            // the card's other action, same turn: allowed
        Eq(e.State.Players[0].Honor, honor + 1); Eq(e.State.Cards[rig.Card.Id].Zone, Zone.RemovedFromGame);
    }

    private static void NegatedMovement()
    {
        // Hand play, Tamori Tamoko (the opponent's) negates the movement.
        var rig = Table();
        var tamoko = Add(rig.S, "training-tamoko", 1, Zone.Battlefield, province: 0); // #1133: a Reaction in a battle needs its Personality at the battlefield
        var e = MatchEngine.FromTestState(rig.S);
        Accept(e, Use(e, rig.Card, Rta, rig.BowedHome.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the movement is offered for negation");
        Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: tamoko.Id, AbilityId: "tamoko-negate"));
        Accept(e, Decline(e)); Accept(e, Decline(e));
        var stayed = e.State.Cards[rig.BowedHome.Id];
        Eq(stayed.Zone, Zone.Home, "negated: he never moved"); Check(stayed.Bowed, "and, because the movement did not resolve, he is not straightened");
        Eq(stayed.ProvinceIndex, (int?)null);
        Eq(e.State.Cards[rig.Card.Id].Zone, Zone.FateDiscard, "the card is still spent");
        Check(e.State.Events.Any(x => x.Type == "moveNegated"));

        // Declined: the same position resolves normally.
        var open = Table();
        var openTamoko = Add(open.S, "training-tamoko", 1, Zone.Home);
        var d = MatchEngine.FromTestState(open.S);
        Accept(d, Use(d, open.Card, Rta, open.BowedHome.Id));
        Eq(d.State.PendingChoice?.Type, "beforeActionReaction");
        Accept(d, Decline(d)); Accept(d, Decline(d));
        Eq(d.State.Cards[open.BowedHome.Id].Zone, Zone.Battlefield); Check(!d.State.Cards[open.BowedHome.Id].Bowed, "declined: moved and straightened");

        // Discipline mode: the cost is paid and the card removed from the game even though the movement was negated.
        var paid = Table(Zone.FateDiscard);
        var paidTamoko = Add(paid.S, "training-tamoko", 0, Zone.Battlefield, province: 0); // the Defender's own Tamoko may negate their own action too
        var p = MatchEngine.FromTestState(paid.S);
        var sources = TwoGold(p);
        Accept(p, Use(p, paid.Card, Rta, paid.UnbowedHome.Id, sources));
        Eq(p.State.PendingChoice?.Type, "beforeActionReaction");
        Accept(p, new GameCommand("useAbility", 0, p.Version, CardId: paidTamoko.Id, AbilityId: "tamoko-negate"));
        Accept(p, Decline(p)); Accept(p, Decline(p));
        Eq(p.State.Cards[paid.UnbowedHome.Id].Zone, Zone.Home);
        Check(sources.All(id => p.State.Cards[id].Bowed) && Paid(p, 2), "the Discipline cost was paid");
        Eq(p.State.Cards[paid.Card.Id].Zone, Zone.RemovedFromGame);
    }

    private static void OfferedCommandsAllValidate()
    {
        var rig = Table();
        Add(rig.S, ReturnToAction, 0, Zone.FateDiscard);
        var source = MatchEngine.FromTestState(rig.S);
        var uses = source.QueryActions(0).Where(a => a.Type == "useAbility" && a.Enabled).ToArray();
        Eq(uses.Length, 2, "both copies are offered");
        OfferedCommandsValidate(source, () => MatchEngine.FromTestState(rig.S), alternateTargets: true);
    }

    // ---- Banzai! and Lost in Transit: the same model, from hand ---------------------------------------------------
    private static void HandPlayOfExistingDisciplineCards()
    {
        MatchState BattleTable()
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            return s;
        }
        // Banzai! from hand: opposed own Personality, +2F, free (its published GoldCost is the Discipline cost and must NOT be charged), then the Fate discard pile.
        {
            var s = BattleTable();
            var card = Add(s, Banzai, 0, Zone.Hand);
            var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var option = Offer(e, card)!;
            Check(option.Enabled && option.Gold is null, option.Reason ?? "the hand play is free");
            Reject(e, Use(e, card, "banzai", target.Id, TwoGold(e)));
            Accept(e, option.Command!);
            Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == target.Id).Force, 5, "3 + 2");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "an ordinary Strategy: discarded, not removed from the game");
            Check(!AnyPayment(e), "no Gold was charged");
        }
        // Its printed Battle verb still holds from hand: the Action phase is rejected.
        {
            var s = BattleTable(); s.Phase = Phase.Action;
            var card = Add(s, Banzai, 0, Zone.Hand);
            var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Reject(e, Use(e, card, "banzai", target.Id));
        }
        // Lost in Transit from hand: Battle/Open. Open (Action phase): free, no presence needed.
        {
            var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var card = Add(s, LostInTransit, 0, Zone.Hand);
            var host = Add(s, "training-lion-samurai", 1, Zone.Home);
            var attachment = Add(s, "training-item", 1, Zone.Attached, parent: host.Id);
            var e = MatchEngine.FromTestState(s);
            var option = Offer(e, card)!;
            Check(option.Enabled && option.Gold is null, option.Reason ?? "free from hand");
            Reject(e, Use(e, card, "lost-in-transit-bow", attachment.Id, TwoGold(e)));
            Accept(e, option.Command! with { TargetId = attachment.Id });
            Check(e.State.Cards[attachment.Id].Bowed, "Bow a target attachment");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard); Check(!AnyPayment(e));
        }
        // ...and as a Battle action during a battle it needs the acting player's presence (the printed verb is enforced now).
        {
            MatchEngine Battle(bool withPresence)
            {
                var s = BattleTable();
                Add(s, LostInTransit, 0, Zone.Hand);
                var host = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
                Add(s, "training-item", 1, Zone.Attached, province: 0, parent: host.Id);
                if (withPresence) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
                return MatchEngine.FromTestState(s);
            }
            var lonely = Battle(false);
            var lostCard = lonely.State.Cards.Values.Single(c => c.DefinitionId == LostInTransit);
            var attachmentId = lonely.State.Cards.Values.Single(c => c.Zone == Zone.Attached).Id;
            Reject(lonely, Use(lonely, lostCard, "lost-in-transit-bow", attachmentId));
            var present = Battle(true);
            Accept(present, Use(present, present.State.Cards.Values.Single(c => c.DefinitionId == LostInTransit), "lost-in-transit-bow", present.State.Cards.Values.Single(c => c.Zone == Zone.Attached).Id));
        }
        // The fixture Item (not a Strategy) still has no hand play: the flag only adds the discard-pile mode to it.
        {
            var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
            var item = Add(s, "training-discipline-item", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Reject(e, Use(e, item, "training-discipline-ability"));
            Reject(e, Use(e, item, "training-discipline-ability", sources: TwoGold(e)));
        }
    }

    // ---- Absent flag parity ------------------------------------------------------------------------------------------
    private static void AbsentFlagParity()
    {
        MatchState BattleTable(Phase phase = Phase.Battle)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            s.Phase = phase; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
            return s;
        }
        // Ring of Earth (the stub carries the real abilities): "Even if you control no units at the current battlefield".
        foreach (var ability in new[] { "ring-of-earth-raise", "ring-of-earth-lower" })
        {
            var s = BattleTable(); var ring = Add(s, "training-ring-of-earth-stub", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: ability, TargetPlayerId: 1, Province: 0));
            var action = BattleTable(Phase.Action); var actionRing = Add(action, "training-ring-of-earth-stub", 0, Zone.Home);
            var a = MatchEngine.FromTestState(action);
            Reject(a, new GameCommand("useAbility", 0, a.Version, CardId: actionRing.Id, AbilityId: ability, TargetPlayerId: 1, Province: 0)); // still a Battle action
            var handTable = BattleTable(); var handRing = Add(handTable, "training-ring-of-earth-stub", 0, Zone.Hand);
            var h = MatchEngine.FromTestState(handTable);
            Accept(h, new GameCommand("useAbility", 0, h.Version, CardId: handRing.Id, AbilityId: ability, TargetPlayerId: 1, Province: 0));
        }
        // Ring of Water's move-home (real card): the target is your own Personality AT the battlefield, so you already have
        // presence -- the Absent flag changes nothing observable there.
        {
            var s = BattleTable(); var ring = Add(s, "oracle-l5r-6339", 0, Zone.Home);
            var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var ownHome = Add(s, "training-crane-courtier", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-water-move-home", TargetId: enemy.Id));
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-water-move-home", TargetId: ownHome.Id));
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "ring-of-water-move-home", TargetId: own.Id));
            Eq(e.State.Cards[own.Id].Zone, Zone.Home);
            var action = BattleTable(Phase.Action); var actionRing = Add(action, "oracle-l5r-6339", 0, Zone.Home);
            var actionOwn = Add(action, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var a = MatchEngine.FromTestState(action);
            Reject(a, new GameCommand("useAbility", 0, a.Version, CardId: actionRing.Id, AbilityId: "ring-of-water-move-home", TargetId: actionOwn.Id));
        }
        // False Ring of Air (real card): "Absent Battle" -- newly Absent, so a player with NO unit at the battlefield may give an
        // enemy Personality there -3F (before, presence was required); with a unit of their own it is unchanged.
        foreach (var withPresence in new[] { false, true })
        {
            var s = BattleTable(); var ring = Add(s, "oracle-l5r-10531", 0, Zone.Home);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (withPresence) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "false-ring-of-air-force", TargetId: enemy.Id));
            Eq(e.GetView(1).Players[1].Army.Single(c => c.Id == enemy.Id).Force, 1, "4 - 3");
            Check(e.State.Cards[ring.Id].Bowed, "bows itself as the in-play cost");
        }
        {
            // Absent does not waive the target's own location rule, nor the Battle phase.
            var s = BattleTable(); var ring = Add(s, "oracle-l5r-10531", 0, Zone.Home);
            var away = Add(s, "training-lion-samurai", 1, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: ring.Id, AbilityId: "false-ring-of-air-force", TargetId: away.Id));
            var action = BattleTable(Phase.Action); var actionRing = Add(action, "oracle-l5r-10531", 0, Zone.Home);
            var actionEnemy = Add(action, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var a = MatchEngine.FromTestState(action);
            Reject(a, new GameCommand("useAbility", 0, a.Version, CardId: actionRing.Id, AbilityId: "false-ring-of-air-force", TargetId: actionEnemy.Id));
        }
    }
}
