using L5R.Core;
using static Batch336Support;
using static RegressionAssertions;

// Issues #1129 and #1131 (Mendon's rulings, 2026-10-27): an Attack on a province is targeted, so every "after an Attack is targeted" Reaction triggers on it; unless its text lets it be used from another location
// it needs presence at the battlefield of the attacked province (an Attack on a card too: #1131); a Reaction that needs a CARD to be targeted cannot react to a province. Position: the economy table, player 1 the Attacker with a Ranged 3
// province Attack at province 0 (the battlefield), player 0 the Defender. For each Reaction the card is placed where it has presence and where it has none, driven only through QueryActions.
internal static class ProvinceAttackReactionTests
{
    private const string ProvinceAttacker = "training-province-attacker", ProvinceAttack = "training-province-attack";

    public static void Run(TestRunner runner) => runner.Run("province-attack-reactions", [
        ("Each 'after an Attack is targeted' Reaction reacts to an Attack on a province from where it has presence at the battlefield, and never from home", PresenceMatrix),
        ("A Strategy played from a hand needs its player to have a Personality at the attacked battlefield", StrategyNeedsPresence),
        ("A Reaction that needs a card to be targeted (A Close Call, Akodo Kakihara, Flesh Cutter Arrows) never reacts to a province Attack", CardTargetReactionsRefused),
        ("An Attack on a card gives the same answer: Kaname, Umiko, Fumiko, Khol Regulars and Steal the Candle's Flame need presence at the battlefield, Archery Range reacts from home", CardAttackPresence),
        ("The reaction takes effect: Strong Defenses' -3 leaves the province with all its strength", ReactionTakesEffect),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static string Def(string ability) => CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    // The engine after player 1 declares the province Attack on province 0.
    private static MatchEngine Attack(Action<MatchState> arrange, bool onCard = false)
    {
        var s = Table(Phase.Battle, 1);
        arrange(s);
        if (onCard)
        {
            // A Ranged 3 Attack (a Strategy in the Attacker's hand) on the Defender's Personality at the battlefield.
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
            var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var onCardEngine = MatchEngine.FromTestState(s);
            Accept(onCardEngine, Offer(onCardEngine, 1, "training-honor-cost-attack", strategy.Id, victim.Id));
            return onCardEngine;
        }
        var attacker = Add(s, ProvinceAttacker, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(1).Single(a => a.Type == "useAbility" && a.CardId == attacker.Id && a.Command?.AbilityId == ProvinceAttack).Command! with { TargetId = null, Province = 0 });
        return e;
    }

    // Is the ability on that card offered enabled to its owner in the window the Attack opened (no window: no)?
    private static bool Offered(MatchEngine e, int owner, string ability, string cardId)
    {
        if (e.State.PendingChoice?.Type != "attackReaction") return false;
        if (e.State.PriorityPlayerId != owner) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        return e.State.PendingChoice?.Type == "attackReaction" && CanUse(e, owner, ability, cardId);
    }

    // (ability, owner, how the card is carried: a Personality, a Follower or Spell on one, a Holding)
    private static readonly (string Ability, int Owner, string Kind)[] Cases =
    [
        ("kaname-attack-boost", 0, "personality"), ("umiko-attack-boost", 0, "personality"), ("umiko-attack-reduce", 0, "personality"), ("fumiko-attack-boost", 1, "personality"),
        ("khol-regulars-reaction", 0, "follower"), ("archery-range-reaction", 0, "holding"), ("tf-steal-candle-interrupt", 0, "spell"),
    ];

    private static (MatchEngine E, CardInstance Card) Place(string ability, int owner, string kind, bool present, bool onCard = false)
    {
        CardInstance card = null!;
        var e = Attack(s =>
        {
            var zone = present ? Zone.Battlefield : Zone.Home;
            int? province = present ? 0 : null;
            if (owner == 0) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // the Defender is present either way: only the reactor's own place varies
            card = kind switch
            {
                "personality" => Add(s, Def(ability), owner, zone, province: province),
                "follower" => Add(s, Def(ability), owner, Zone.Attached, province: province, parent: Add(s, "training-crane-samurai", owner, zone, province: province).Id),
                "spell" => Add(s, Def(ability), owner, Zone.Attached, province: province, parent: Add(s, "training-shugenja", owner, zone, province: province).Id),
                _ => Add(s, Def(ability), owner, Zone.Home), // a Holding is never at a battlefield
            };
        }, onCard);
        return (e, card);
    }

    private static void PresenceMatrix()
    {
        foreach (var (ability, owner, kind) in Cases)
        {
            var present = Place(ability, owner, kind, present: true);
            Check(Offered(present.E, owner, ability, present.Card.Id), ability + ": offered from the battlefield");
            var absent = Place(ability, owner, kind, present: false);
            Eq(Offered(absent.E, owner, ability, absent.Card.Id), kind == "holding", ability + (kind == "holding" ? ": Archery Range may react from home" : ": not offered from home"));
        }
    }

    private static void CardAttackPresence()
    {
        foreach (var (ability, owner, kind) in Cases)
        {
            var present = Place(ability, owner, kind, present: true, onCard: true);
            Check(present.E.State.PendingAttack is { TargetId: not null }, ability + ": the Attack names a card");
            Check(Offered(present.E, owner, ability, present.Card.Id), ability + ": offered from the battlefield");
            var absent = Place(ability, owner, kind, present: false, onCard: true);
            Eq(Offered(absent.E, owner, ability, absent.Card.Id), kind == "holding", ability + (kind == "holding" ? ": Archery Range may react from home" : ": not offered from home"));
        }
    }

    private static void StrategyNeedsPresence()
    {
        foreach (var ability in new[] { "strong-defenses-reaction", "trusting-instinct-reaction", "precision-boost", "sniping-boost", "unholy-strike-boost" })
        {
            CardInstance card = null!;
            var with = Attack(s => { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); card = Add(s, Def(ability), 0, Zone.Hand); });
            Check(Offered(with, 0, ability, card.Id), ability + ": offered with a Personality at the battlefield");
            var without = Attack(s => { card = Add(s, Def(ability), 0, Zone.Hand); Add(s, "training-crane-samurai", 0, Zone.Home); });
            Check(!Offered(without, 0, ability, card.Id), ability + ": not offered with no Personality at the battlefield");
        }
    }

    private static void CardTargetReactionsRefused()
    {
        CardInstance kakihara = null!, closeCall = null!, cutter = null!;
        var e = Attack(s =>
        {
            kakihara = Add(s, Def("kakihara-attack-force-bonus"), 0, Zone.Battlefield, province: 0);
            var armored = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Kw(Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: armored.Id), "Armor");
            closeCall = Add(s, Def("a-close-call-negate"), 0, Zone.Hand);
            var wielder = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            cutter = Add(s, Def("flesh-cutter-arrows-reaction"), 1, Zone.Attached, province: 0, parent: wielder.Id);
        });
        Eq(e.State.PendingChoice?.Type, null, "nothing can react, so there is no window");
        Check(!CanUse(e, 0, "kakihara-attack-force-bonus", kakihara.Id) && !CanUse(e, 0, "a-close-call-negate", closeCall.Id) && !CanUse(e, 1, "flesh-cutter-arrows-reaction", cutter.Id), "none of the three is offered");
    }

    private static void ReactionTakesEffect()
    {
        int Lost(bool react)
        {
            CardInstance card = null!;
            var e = Attack(s => { Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); card = Add(s, Def("strong-defenses-reaction"), 0, Zone.Hand); });
            var before = e.ProvinceStrengthForTest(0, 0);
            if (react) { if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); Accept(e, Enabled(e, 0, "strong-defenses-reaction", card.Id).Command!); }
            for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            return before - e.ProvinceStrengthForTest(0, 0);
        }
        Eq(Lost(react: false), 3, "declined: the province loses the Attack's 3 strength");
        Eq(Lost(react: true), 0, "Strong Defenses' -3 leaves the Attack at 0 strength");
    }
}
