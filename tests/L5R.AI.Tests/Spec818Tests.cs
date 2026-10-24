using L5R.AI;
using L5R.Core;

// Issue #818: Mendon's eleven bot changes, each pinned on its own with a small synthetic table. The old rules stay measurable with BotContext.UseSpec818 = false.
internal static class Spec818Tests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Items 1 and 3: Holdings stay a priority to ten; with Gold for every province card nothing is discarded and all are bought; otherwise Personalities are discarded down to what the production affords", DynastyBuying),
        ("Item 2: after its second turn a bot with fewer Personalities recruits one before anything else, once", CatchUpRecruit),
        ("Item 4: an experienced Personality is overlaid for free only when the bot has more Personalities or the card would be discarded", OverlayLimit),
        ("Item 5: lobbies with the higher Honor when the opponent's units average more Force than its weakest, bowing the weakest bare unit; defending, sends home the largest unit", FavorRules),
        ("Item 6: cards in hand that add Force count toward an attack, and an army must beat the Province and the defenders each", AttackForce),
        ("Item 7: an unopposed attacker that destroys the Province passes, except for Honor plays that will resolve", AttackerPasses),
        ("Item 8: holding Honor-gaining cards, a unit goes at a Province it cannot destroy while the rest attack another", HonorAttack),
        ("Item 9: Force an equipped attachment removes from the enemy changes the outcome like Force added", EquipRemoval),
        ("Item 10: Shinden bows a small unit or against an opponent over 25 Honor, otherwise only when that cannot change the attack decision", ShindenBow),
        ("Issue #825: the bot never negates its own held action, but still negates the opponent's", NeverNegatesOwnAction),
        ("Item 11: a Reaction that destroys or bows an opponent's card is always played", AlwaysDestroyOrBow),
        ("Issue #829: the bot gives away its weakest Personality for the Wedding, ticks only its own Personalities for Benten's Blessing and confirms, and names the strongest enemy for Minor Blackmail", Wave829Decisions),
        ("Issue #827: Kokujin's question sacrifices the weakest Personality for a clearly stronger loser, declines otherwise; P'an Ku stays bowed; a Koan discard is answered", Wave827Decisions),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static readonly CardDefinition HoldingDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding && d.GoldProduction > 0);
    private static readonly CardDefinition PersonDef = BattlePolicyTests.Person();

    private static CardView Holding(string id, int production = 2, bool bowed = false) =>
        new(id, HoldingDef with { GoldProduction = production }, 0, Zone.Home, true, bowed, null, null, 0);

    private static CardView Candidate(string id, CardDefinition def, bool faceUp = true) => new(id, def, 0, Zone.Province, faceUp, false, null, null, def.Force);

    private static CardView Soldier(string id, int owner, int force, string clan = "Lion") =>
        new(id, PersonDef with { Id = "p-" + id, Clan = clan }, owner, Zone.Home, true, false, null, null, force);

    /// <summary>A Dynasty Phase table: the bot (seat 0) with Stronghold production <paramref name="production"/>, the given Holdings and Personalities at home, and the given cards face up in its provinces.</summary>
    private static MatchView Dynasty(int production, CardView[] home, CardView[] provinceCards, int turnsTaken = 4, CardView[]? opponentHome = null, string clan = "Lion")
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var self = view.Players[0];
        var provinces = provinceCards.Select((c, i) => new ProvinceView(i, 5, false, c, null)).ToArray();
        return view with
        {
            Phase = Phase.Dynasty,
            ActivePlayerId = 0,
            PriorityPlayerId = 0,
            PendingChoice = null,
            TurnNumber = 8,
            FavorPlayerId = null,
            Players = [self with { Clan = clan, TurnsTaken = turnsTaken, Stronghold = self.Stronghold with { Definition = self.Stronghold.Definition! with { GoldProduction = production }, Bowed = false }, Home = home, Army = [], Provinces = provinces, Hand = [], HandCount = 0 },
                view.Players[1] with { Home = opponentHome ?? [], Army = [] }]
        };
    }

    private static ActionOption Buy(MatchView v, CardView card, int cost) =>
        new("purchase", "Purchase " + card.Id, true, null, null, card.Id, Quote: new PurchaseQuote(card.Id, cost, false, false, false, null, [new PaymentOption(["gold-source"], cost, cost, 0)]));

    private static ActionOption Discard(CardView card) => new("discardProvince", "Discard " + card.Id, true, null, null, card.Id);
    private static ActionOption End() => new("endDynasty", "End Dynasty", true, null, null);

    private static string Picked(MatchView v, BotContext? memory, params ActionOption[] options)
    {
        var command = TrainingOpponent.Choose(v, options, memory ?? new BotContext()).Command;
        return command is null ? "none" : command.Type + (command.CardId is null ? "" : ":" + command.CardId);
    }

    private static void DynastyBuying()
    {
        // Item 1. Ten Holdings is the new ceiling: with 8 or 9 the next Holding is still bought over ending the phase, and at 10 it stops.
        CardView[] Many(int n) => Enumerable.Range(0, n).Select(i => Holding("h" + i)).ToArray();
        var holdingCard = Candidate("buy-h", HoldingDef with { GoldCost = 6 });
        string Eight(int holdings, bool spec) => Picked(Dynasty(0, Many(holdings), [holdingCard]), new BotContext { UseSpec818 = spec }, Buy(null!, holdingCard, 6), End());
        Check(Eight(8, true) == "purchase:buy-h" && Eight(9, true) == "purchase:buy-h", "the 9th and 10th Holdings are still bought");
        Check(Eight(8, false) == "purchase:buy-h", "the old ceiling (8) still scores a purchase above ending the phase");

        // Item 1: with Gold for every card in the provinces, a Holding is bought over discarding whatever the count; without it, the bot discards as it did.
        var rich = Dynasty(30, Many(12), [holdingCard]);
        Check(Picked(rich, null, Buy(rich, holdingCard, 6), Discard(holdingCard), End()) == "purchase:buy-h", "Gold for the whole row: every card is bought, none discarded");
        var poor = Dynasty(0, Many(12).Select(h => h with { Bowed = true }).ToArray(), [holdingCard]);
        Check(Picked(poor, null, Buy(poor, holdingCard, 6), Discard(holdingCard), End()).StartsWith("discardProvince", StringComparison.Ordinal), "no Gold: a surplus Holding is discarded");

        // Item 3: the discard rule. Production 14, Clan Personalities costing 10 and 9 count 8 and 7; the 7 goes.
        var clanDef = PersonDef with { Clan = "Lion", HonorRequirement = null };
        var ten = Candidate("ten", clanDef with { Id = "ten", GoldCost = 10 }); var nine = Candidate("nine", clanDef with { Id = "nine", GoldCost = 9 });
        var table = Dynasty(14, [Holding("bowed", 0, true)], [ten, nine]);
        Check(Picked(table with { Players = [table.Players[0] with { Stronghold = table.Players[0].Stronghold with { Bowed = true } }, table.Players[1]] }, null, Discard(ten), Discard(nine), End()) == "discardProvince:nine", "14 production and costs 8 and 7: the 7-cost Personality is discarded");
        var enough = Dynasty(20, [], [ten, nine]);
        Check(Picked(enough with { Players = [enough.Players[0] with { Stronghold = enough.Players[0].Stronghold with { Bowed = true } }, enough.Players[1]] }, null, Discard(ten), Discard(nine), End()) == "endDynasty",
            "capacity 20 covers 8 + 7 even with every Holding bowed: nothing is discarded");
        var unaligned = Candidate("far", PersonDef with { Id = "far", Clan = "Crane", GoldCost = 9 });
        Check(Picked(Dynasty(8, [], [unaligned]), null, Discard(unaligned), End()).StartsWith("discardProvince", StringComparison.Ordinal), "a Personality of another Clan gets no discount: 9 does not fit 8");
    }

    private static void CatchUpRecruit()
    {
        var holdingCard = Candidate("buy-h", HoldingDef with { GoldCost = 2 });
        var personality = Candidate("buy-p", PersonDef with { Id = "buy-p", GoldCost = 4, Clan = "Crane" });
        var behind = Dynasty(10, [Soldier("mine-1", 0, 3)], [holdingCard, personality], turnsTaken: 4, opponentHome: [Soldier("o1", 1, 3), Soldier("o2", 1, 3)]);
        var memory = new BotContext();
        Check(Picked(behind, memory, Buy(behind, holdingCard, 2), Buy(behind, personality, 4), End()) == "purchase:buy-p", "behind on Personalities after turn two: the Personality comes first");
        Check(Picked(behind, memory, Buy(behind, holdingCard, 2), Buy(behind, personality, 4), End()) == "purchase:buy-h", "only the first one: then normal priorities (the Holding)");
        var early = behind with { Players = [behind.Players[0] with { TurnsTaken = 2 }, behind.Players[1]] };
        Check(Picked(early, null, Buy(early, holdingCard, 2), Buy(early, personality, 4), End()) == "purchase:buy-h", "not before the third turn");
        var level = Dynasty(10, [Soldier("mine-1", 0, 3), Soldier("mine-2", 0, 3)], [holdingCard, personality], opponentHome: [Soldier("o1", 1, 3), Soldier("o2", 1, 3)]);
        Check(Picked(level, null, Buy(level, holdingCard, 2), Buy(level, personality, 4), End()) == "purchase:buy-h", "level on Personalities: normal priorities");
    }

    private static void OverlayLimit()
    {
        var experienced = Candidate("exp", PersonDef with { Id = "exp", Clan = "Lion", GoldCost = 10 });
        ActionOption Overlay() => new("overlay", "Overlay", true, null, new("overlay", 0, 0, "exp", TargetId: "mine-1"), "exp", ["mine-1"]);
        MatchView Table(int mine, int theirs, int production = 40) => Dynasty(production, Enumerable.Range(0, mine).Select(i => Soldier("mine-" + (i + 1), 0, 3)).ToArray(), [experienced],
            opponentHome: Enumerable.Range(0, theirs).Select(i => Soldier("o" + i, 1, 3)).ToArray());
        string Pick(MatchView v) { var c = TrainingOpponent.Choose(v with { Version = 0 }, [Overlay(), End()]).Command; return c?.Type ?? "none"; }
        Check(Pick(Table(3, 2)) == "overlay", "ahead on Personalities: the free overlay is taken");
        Check(Pick(Table(2, 3)) == "endDynasty", "behind on Personalities and nothing forces a discard: it keeps the card in the province");
        Check(Pick(Table(2, 3, production: 1)) == "overlay", "behind, but the card could not be afforded and would be discarded: overlay it instead");
    }

    private static void FavorRules()
    {
        var table = Dynasty(5, [], []);
        ActionOption Ability(string id, string card, string? target = null, string[]? targets = null) =>
            new("useAbility", id, true, null, new("useAbility", 0, table.Version, CardId: card, AbilityId: id, TargetId: target), card, targets);
        var strong = Soldier("s", 1, 6); var mid = Soldier("m", 1, 4);
        var weak = Soldier("w", 0, 2); var weakBare = Soldier("wb", 0, 2);
        var withSword = new CardView("sword", CardCatalog.Cards.Values.First(d => d.Type == CardType.Item), 0, Zone.Attached, true, false, null, "w", 1);
        MatchView View(int myHonor, int theirHonor) => table with { Players = [table.Players[0] with { Honor = myHonor, Home = [weak, weakBare, withSword] }, table.Players[1] with { Honor = theirHonor, Home = [strong, mid] }] };
        var rival = Ability("shunya-honor", "rival");
        string Pick(MatchView v, params ActionOption[] options) => TrainingOpponent.Choose(v, options, new BotContext()).Command?.AbilityId ?? "pass";
        Check(Pick(View(20, 10), rival, Ability("lobby", "lobby-source", targets: ["w", "wb"])) == "lobby", "higher Honor and the opponent's units average 5 against its weakest 2: it lobbies");
        Check(Pick(View(10, 20), rival, Ability("lobby", "lobby-source", targets: ["w", "wb"])) == "shunya-honor", "lower Honor: the old rule (fewer Personalities yields to other plays)");
        var command = TrainingOpponent.Choose(View(20, 10), [Ability("lobby", "lobby-source", targets: ["w", "wb"])], new BotContext()).Command;
        Check(command?.TargetId == "wb", "it bows the weakest unit with the fewest attachments (the bare one), not the one carrying a sword; got " + command?.TargetId);

        // Defending: the Favor sends home the largest unit.
        var defending = View(20, 10) with { FavorPlayerId = 0 };
        var bigger = Soldier("big", 1, 7) with { Zone = Zone.Battlefield, ProvinceIndex = 0 };
        var smaller = Soldier("small", 1, 3) with { Zone = Zone.Battlefield, ProvinceIndex = 0 };
        defending = defending with { Players = [defending.Players[0], defending.Players[1] with { Army = [bigger, smaller] }] };
        var send = TrainingOpponent.Choose(defending, [Ability("favor-battle", "favor-source", targets: ["small", "big"]), Ability("shunya-honor", "rival")], new BotContext()).Command;
        Check(send?.AbilityId == "favor-battle" && send.TargetId == "big", "defending, it always uses the Favor on the largest unit; got " + send?.AbilityId + "/" + send?.TargetId);
    }

    private static MatchView AttackTable(int ownForce, int enemyForce, int strength, params CardView[] hand)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        ProvinceView[] Provinces(PlayerView p) => p.Provinces.Select(q => q with { Strength = strength, Destroyed = false }).ToArray();
        return view with
        {
            Phase = Phase.AttackDeclaration,
            ActivePlayerId = 0,
            PriorityPlayerId = 0,
            PendingChoice = null,
            Players = [view.Players[0] with { Home = [BattlePolicyTests.Home("a", 0, ownForce, 3)], Army = [], Provinces = Provinces(view.Players[0]), Hand = hand, HandCount = hand.Length },
                view.Players[1] with { Home = [BattlePolicyTests.Home("d", 1, enemyForce, 3)], Army = [], Provinces = Provinces(view.Players[1]) }]
        };
    }

    private static void AttackForce()
    {
        var declare = (MatchView v) => new ActionOption("declareAttack", "Declare", true, null, new("declareAttack", 0, v.Version));
        string Choice(MatchView v, BotContext c) => TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), declare(v)], c).Command?.Type ?? "none";
        // Beating the Province (5) and the defenders (4) each is enough: 6 attacks. The old rule wanted more than 5 + 4.
        var table = AttackTable(6, 4, 5);
        Check(Choice(table, new BotContext()) == "declareAttack" && Choice(table, new BotContext { UseSpec818 = false }) == "pass", "6 beats Strength 5 and defenders 4 separately");
        Check(Choice(AttackTable(5, 4, 5), new BotContext()) == "pass", "5 does not beat Strength 5");
        // Cards in hand that add Force count: 6 Force plus two +3F cards beats Strength 9 and defenders 4.
        var forceCard = CardCatalog.Cards.Values.First(d => d.Effect == "force-three");
        CardView InHand(string id) => new(id, forceCard, 0, Zone.Hand, true, false, null, null, 0);
        Check(Choice(AttackTable(6, 4, 9), new BotContext()) == "pass", "6 against Strength 9: not on its own");
        Check(Choice(AttackTable(6, 4, 9, InHand("f1"), InHand("f2")), new BotContext()) == "declareAttack", "two +3 Force cards in hand lift 6 to 12: it attacks");
        Check(Choice(AttackTable(6, 4, 9, InHand("f1"), InHand("f2")), new BotContext { UseSpec818 = false }) == "pass", "the old bot ignored its hand");
    }

    private static void AttackerPasses()
    {
        // The bot attacks at province 0 with 16 Force against nothing, Strength 7: the Province falls. Anything but an Honor play that resolves is a pass.
        var view = BattlePolicyTests.Battle(16, 0, 7, enemyExtra: []);
        view = view with { Players = [view.Players[0], view.Players[1] with { Army = [] }] };
        var generic = BattlePolicyTests.Use(view, "shunya-honor");
        string Pick(params ActionOption[] options) => TrainingOpponent.Choose(view, [BattlePolicyTests.Pass(view), .. options], new BotContext()).Command is { } c ? c.AbilityId ?? c.Type : "none";
        var other = AbilityCatalog.All.First(p => p.Value.Timing == "Battle" && p.Value.Target == "none" && AbilityIntent.Of(p.Value) is { Rank: >= EffectRank.Bow, HonorGain: false, HonorLoss: false, IsReaction: false }).Key;
        Check(Pick(BattlePolicyTests.Use(view, other)) == "pass", "the Province already falls and the play gains no Honor: it passes");
        var honor = AbilityCatalog.All.First(p => p.Value.Timing == "Battle" && p.Value.Target == "none" && AbilityIntent.Of(p.Value) is { HonorGain: true, IsReaction: false } && !System.Text.RegularExpressions.Regex.IsMatch(p.Value.Label ?? "", "not (been )?destroyed")).Key;
        Check(Pick(BattlePolicyTests.Use(view, honor)) == honor, "a plain Honor gain is still played");
        // Not yet enough Force: it plays on as usual.
        var short7 = BattlePolicyTests.Battle(6, 0, 7);
        short7 = short7 with { Players = [short7.Players[0], short7.Players[1] with { Army = [] }] };
        // Issue #997: unopposed and short of the Province it plays only the Force it lacks ("+2F"), not an action that adds none.
        Check(TrainingOpponent.Choose(short7, [BattlePolicyTests.Pass(short7), BattlePolicyTests.Use(short7, other)], new BotContext()).Command?.AbilityId is not { } played || played != other, "short of the Province, an action that adds no Force is not played against nothing");
        Check(TrainingOpponent.Choose(short7, [BattlePolicyTests.Pass(short7), BattlePolicyTests.Use(short7, "kunji-boost")], new BotContext()).Command?.AbilityId == "kunji-boost", "short of the Province: it plays the Force it lacks");
        _ = generic;
    }

    private static void HonorAttack()
    {
        // The bot holds a Battle card that gains Honor, and two units that cannot raze anything; it sends the weaker at a Province it cannot destroy.
        var honorCardDef = CardCatalog.Cards.Values.FirstOrDefault(d => d.Type == CardType.Strategy && d.Name == "Fear Me!" /* issue #892: pinned; the first match used to be Surety of Purpose, which needs an enemy target now that its Target is enemy-only */ && BattlePolicy.Printed(new CardView("x", d, 0, Zone.Hand, true, false, null, null, 0)).Any(p => p.Ability.Timing is "Battle" && p.Intent.HonorGain && !p.Intent.IsReaction && p.Intent.Side != TargetSide.Enemy));
        Check(honorCardDef is not null, "the catalogue has an Honor-gaining Battle Strategy");
        var table = AttackTable(3, 6, 9);
        var holder = new CardView("hc", honorCardDef!, 0, Zone.Hand, true, false, null, null, 0);
        var withCard = table with { Players = [table.Players[0] with { Hand = [holder], HandCount = 1 }, table.Players[1]] };
        var declare = new ActionOption("declareAttack", "Declare", true, null, new("declareAttack", 0, withCard.Version));
        var pass = BattlePolicyTests.Pass(withCard);
        Check(TrainingOpponent.Choose(withCard, [pass, declare], new BotContext()).Command?.Type == "declareAttack", "with an Honor card (" + honorCardDef!.Name + ") in hand it attacks a Province it cannot destroy, to play it");
        Check(TrainingOpponent.Choose(table, [BattlePolicyTests.Pass(table), new ActionOption("declareAttack", "Declare", true, null, new("declareAttack", 0, table.Version))], new BotContext()).Command?.Type == "pass", "with no such card it does not");
        var attack = TrainingOpponent.Choose(withCard, [pass, new ActionOption("attack", "Attack", true, null, new("attack", 0, withCard.Version, Assignments: []))], new BotContext()).Command;
        Check(attack is { Type: "attack", Assignments.Count: 1 } && attack.Assignments.ContainsKey("a"), "the unit is assigned to the attack");
    }

    private static void EquipRemoval()
    {
        // The bot (attacker, 4 Force) faces 6; a Spell it can attach bows the 6-Force defender. Counting the removed Force the attachment lifts a loss to a win, so it is equipped.
        var view = BattlePolicyTests.Battle(4, 6, 9);
        var spellDef = CardCatalog.Cards.Values.FirstOrDefault(d => d.Type == CardType.Spell && d.Force == 0 && (d.Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var ab) && ab.Timing is "Battle" or "BattleOrOpen" && AbilityIntent.Of(ab) is { Rank: EffectRank.Bow or EffectRank.Destroy, Side: TargetSide.Enemy, IsReaction: false }));
        Check(spellDef is not null, "the catalogue has a Spell whose Battle ability bows or destroys an enemy");
        var spell = new CardView("spell", spellDef! with { GoldCost = 2 }, 0, Zone.Hand, true, false, null, null, 0);
        view = view with { Players = [view.Players[0] with { Hand = [spell], HandCount = 1, HandLimit = new HandLimitView(8, 8, []) }, view.Players[1]] };
        var equip = new ActionOption("equip", "Equip", true, null, new("equip", 0, view.Version, "spell", TargetId: "own-main"), "spell", ["own-main"], Gold: new GoldQuote(2, "Printed", "", null, []));
        var chosen = TrainingOpponent.Choose(view, [BattlePolicyTests.Pass(view), equip], new BotContext()).Command?.Type;
        Check(chosen == "equip", "equipping the Spell, which removes 6 Force, changes the outcome: " + chosen);
    }

    private static void ShindenBow()
    {
        var table = Dynasty(5, [], []);
        ActionOption Choice(string choice) => new("chooseShinden", choice, true, null, new("chooseShinden", 0, table.Version, Choice: choice));
        string Pick(MatchView v) => TrainingOpponent.Choose(v, [Choice("honor"), Choice("bow")], new BotContext()).Command!.Choice!;
        // Units: the bot has "big" (Force 10) and "small" (1): bowing small is under 15% of 11? 1/11 = 9%: yes.
        MatchView Table(int opponentHonor, CardView[] mine, string target) =>
            table with { PendingChoice = new PendingChoice("shinden", 0, target, "Shinden Asahina", 1), Players = [table.Players[0] with { Home = mine }, table.Players[1] with { Honor = opponentHonor }] };
        var big = Soldier("big", 0, 10); var small = Soldier("small", 0, 1);
        Check(Pick(Table(10, [big, small], "small")) == "bow", "a unit under 15% of the army's Force is bowed");
        Check(Pick(Table(30, [big, small], "big")) == "bow", "an opponent over 25 Honor is not given more: bow");
        Check(Pick(Table(40, [big, small], "big")) == "honor" && Pick(Table(55, [big, small], "small")) == "honor", "an opponent who has already reached 40 Honor only waits for their turn: never bow, even for a small unit");
        Check(Pick(Table(39, [big, small], "big")) == "bow", "one Honor short of 40 is still held back");
        // A lone Personality with Force: bowing it decides whether the bot attacks at all, so the Honor goes to the opponent instead.
        var only = Soldier("only", 0, 8);
        var attackable = Table(10, [only], "only");
        attackable = attackable with { Players = [attackable.Players[0], attackable.Players[1] with { Home = [], Army = [] }] };
        Check(Pick(attackable) == "honor", "bowing the only attacker would change whether it attacks: allow the Honor");
        // Two strong units: either alone still attacks, so bowing one changes nothing.
        var twin = Table(10, [Soldier("t1", 0, 8), Soldier("t2", 0, 8)], "t1");
        twin = twin with { Players = [twin.Players[0], twin.Players[1] with { Home = [], Army = [] }] };
        Check(Pick(twin) == "bow", "the other unit still attacks: bowing is fine");
    }


    private static void NeverNegatesOwnAction()
    {
        var table = AttackTable(4, 6, 9);
        var negate = AbilityCatalog.All.First(p => p.Value.Timing == "Reaction" && AbilityIntent.Of(p.Value).Negates).Key;
        var use = new ActionOption("useAbility", negate, true, null, new("useAbility", 0, table.Version, CardId: "src", AbilityId: negate));
        var decline = new ActionOption("declineReaction", "Decline", true, null, new("declineReaction", 0, table.Version));
        string Pick(int actingPlayer) => TrainingOpponent.Choose(table with { PendingChoice = new PendingChoice("beforeActionReaction", actingPlayer, null, "Respond", 1) }, [use, decline], new BotContext()).Command!.Type;
        Check(Pick(0) == "declineReaction", "its own action is held: negating it only costs it Gold and cancels its own play");
        Check(Pick(1) == "useAbility", "the opponent's action is held: the negate is played");
    }

    private static void AlwaysDestroyOrBow()
    {
        var table = AttackTable(4, 6, 9);
        var reaction = AbilityCatalog.All.FirstOrDefault(p => p.Value.Timing == "Reaction" && AbilityIntent.Of(p.Value) is { Rank: EffectRank.Destroy or EffectRank.Bow, Side: TargetSide.Enemy, Negates: false });
        Check(reaction.Key is not null, "the catalogue has a Reaction that destroys or bows an enemy card");
        var action = new ActionOption("useAbility", reaction.Key!, true, null, new("useAbility", 0, table.Version, CardId: "src", AbilityId: reaction.Key));
        var decline = new ActionOption("declineReaction", "Decline", true, null, new("declineReaction", 0, table.Version));
        // The weighted roll declines some of these; with the spec on it never does, whatever the match state.
        for (var version = 0; version < 60; version++)
        {
            var v = table with { Version = version };
            var on = TrainingOpponent.Choose(v, [action with { Command = action.Command! with { ExpectedVersion = version } }, decline with { Command = decline.Command! with { ExpectedVersion = version } }], new BotContext()).Command;
            Check(on?.AbilityId == reaction.Key, $"declined an optional destroy/bow at version {version}");
        }
    }

    private static void Wave827Decisions()
    {
        foreach (var (loserForce, expected) in new[] { (6, "w827KokujinChoose:weak"), (2, "w827KokujinChoose:decline") })
        {
            var view = MatchEngine.CreateStarterMatch().GetView(0);
            var kokujin = Soldier("kokujin", 0, 4); var weak = Soldier("weak", 0, 2); var strong = Soldier("strong", 0, 5); var loser = Soldier("loser", 1, loserForce, "Crab");
            view = view with
            {
                PendingChoice = new PendingChoice("w827KokujinChoice", 0, "kokujin", "Destroy a personality to take control of the loser?"),
                PendingDuel = new DuelView(0, 1, "kokujin", "loser", "Chi", "Resolved", 0, false),
                Players = [view.Players[0] with { Home = [kokujin, weak, strong] }, view.Players[1] with { Home = [loser] }]
            };
            ActionOption Pick(string id) => new("w827KokujinChoose", "Destroy " + id, true, null, new GameCommand("w827KokujinChoose", 0, view.Version, TargetId: id), id, DecisionCandidateIds: [id]);
            var options = new[] { Pick("kokujin"), Pick("weak"), Pick("strong"), new ActionOption("w827KokujinChoose", "Decline", true, null, new GameCommand("w827KokujinChoose", 0, view.Version, Choice: "decline")) };
            var command = TrainingOpponent.Choose(view, options, new BotContext()).Command!;
            var label = command.Choice == "decline" ? "decline" : command.TargetId;
            Check("w827KokujinChoose:" + label == expected, $"loser Force {loserForce}: expected {expected}, got {command.Type}:{label}");
        }
        var panKuView = MatchEngine.CreateStarterMatch().GetView(0) with { PriorityPlayerId = 0 };
        var straighten = new[] { new ActionOption("w827PanKuStraightenChoose", "Straighten", true, null, new GameCommand("w827PanKuStraightenChoose", 0, panKuView.Version, Choice: "straighten")),
            new ActionOption("w827PanKuStraightenChoose", "Remain", true, null, new GameCommand("w827PanKuStraightenChoose", 0, panKuView.Version, Choice: "remain"))};
        var kept = TrainingOpponent.Choose(panKuView, straighten, new BotContext());
        Check(kept.Command?.Choice == "remain", "P'an Ku stays bowed to keep what he took: " + kept.Command?.Choice + " / " + kept.Reason);
    }

    private static void Wave829Decisions()
    {
        // Cross-Clan Wedding's second effect: the weakest of the bot's own Personalities is given back.
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var weak = Soldier("weak", 0, 2); var strong = Soldier("strong", 0, 6);
        view = view with { PendingChoice = new PendingChoice(Wave829Abilities.WeddingType, 0, "wedding", "give"), PriorityPlayerId = 0, Players = [view.Players[0] with { Home = [weak, strong] }, view.Players[1]] };
        ActionOption Give(string id) => new(Wave829Abilities.WeddingCommand, "Give " + id, true, null, new GameCommand(Wave829Abilities.WeddingCommand, 0, view.Version, TargetId: id), id, DecisionCandidateIds: [id]);
        var give = TrainingOpponent.Choose(view, [Give("strong"), Give("weak")], new BotContext()).Command!;
        Check(give.TargetId == "weak", "the weakest Personality is given away: " + give.TargetId);

        // Benten's Blessing: its own Personalities are ticked (never the enemy's, never removed), then it confirms.
        var mine = Soldier("mine", 0, 3); var theirs = Soldier("theirs", 1, 5);
        var benten = MatchEngine.CreateStarterMatch().GetView(0);
        benten = benten with { PendingChoice = new PendingChoice(Wave829Abilities.BentenType, 0, "benten", "choose"), PriorityPlayerId = 0, Players = [benten.Players[0] with { Home = [mine] }, benten.Players[1] with { Home = [theirs] }] };
        ActionOption Add(string id, string label) => new("budgetSelectAdd", label + " " + id, true, null, new GameCommand("budgetSelectAdd", 0, benten.Version, TargetId: id), id, DecisionCandidateIds: [id]);
        var confirm = new ActionOption("budgetSelectConfirm", "Confirm selection (0 of 2)", true, null, new GameCommand("budgetSelectConfirm", 0, benten.Version));
        var clear = new ActionOption("budgetSelectClear", "Clear selection", false, "Nothing is chosen.", null);
        var first = TrainingOpponent.Choose(benten, [Add("theirs", "Add"), Add("mine", "Add"), confirm, clear], new BotContext()).Command!;
        Check(first.Type == "budgetSelectAdd" && first.TargetId == "mine", "it ticks its own Personality first: " + first.Type + "/" + first.TargetId);
        var second = TrainingOpponent.Choose(benten, [Add("theirs", "Add"), Add("mine", "Remove"), confirm, clear], new BotContext()).Command!;
        Check(second.Type == "budgetSelectConfirm", "with its own Personality ticked it confirms, never removing it or ticking an enemy's: " + second.Type + "/" + second.TargetId);

        // The Events that stay face up ask their owner a card: Minor Blackmail names the strongest enemy.
        var events = MatchEngine.CreateStarterMatch().GetView(0);
        var blackmailCard = new CardView("blackmail", CardCatalog.Get(Wave829Abilities.Blackmail), 0, Zone.Province, true, false, null, null, 0);
        events = events with { PendingChoice = new PendingChoice("eventChoice", 0, "blackmail", "choose"), PriorityPlayerId = 0, Players = [events.Players[0] with { Provinces = [new ProvinceView(0, 5, false, blackmailCard, null)] }, events.Players[1] with { Home = [Soldier("lesser", 1, 2), Soldier("greater", 1, 7)] }] };
        ActionOption Name(string id) => new("eventChoice", "Blackmail " + id, true, null, new GameCommand("eventChoice", 0, events.Version, CardId: "blackmail", TargetId: id), "blackmail", DecisionCandidateIds: [id]);
        var named = TrainingOpponent.Choose(events, [Name("lesser"), Name("greater")], new BotContext()).Command!;
        Check(named.TargetId == "greater", "the strongest enemy Personality is named: " + named.TargetId);
    }
}
