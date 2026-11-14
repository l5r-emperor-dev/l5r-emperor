using L5R.AI;
using L5R.Core;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4, the Read in shadow): the ledger, the belief and the exchange on constructed positions, including the traps of RULES_DIGEST.md
// section 10: a tie destroys both armies and no Province, a bowed unit counts nothing and still dies, the Province needs strictly more than defenders plus Strength, a unit sent home survives,
// a defender with nobody at the battlefield has no presence, the defender acts first, a Ranged Attack cannot hit a Personality with a Follower.
internal static class ReadTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static CardDefinition Person(int force) => BattlePolicyTests.Person(force);
    private static CardView Unit(string id, int owner, int force, int province = 0, bool bowed = false, string[]? abilities = null) =>
        new(id, Person(force) with { Abilities = abilities }, owner, Zone.Battlefield, true, bowed, province, null, force);
    private static LedgerUnit L(CardView c, int? province = 0) => new(c.Id, c.Id, c.OwnerId, Math.Max(0, c.Force), Math.Max(0, c.Force), c.Bowed, false, province,
        (c.Definition!.Abilities ?? []).Select(EffectDescriptors.Of).ToArray(), [], c.Chi, 0, false, false, 0, 0, 0);
    private static LedgerSide Side(int id, int gold = 0, bool favor = false, Answer[]? hand = null) => new(id, [], [(0, 7)], gold, gold, 10, favor, 0, hand ?? [], favor ? [new(EffectDescriptors.Of("favor-battle"), "Favor", false, 0)] : []);
    private static string FirstAbility(Func<EffectDescriptor, bool> test) => AbilityCatalog.All.Keys.Where(k => !k.StartsWith("training-") && !k.StartsWith("belief-")).Select(EffectDescriptors.Of).Where(d => d.IsBattle && test(d)).OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;

    private static LedgerUnit K(string id, int owner, int force, string[]? abilities = null, string[]? keywords = null) =>
        new(id, id, owner, force, force, false, false, null, (abilities ?? []).Select(EffectDescriptors.Of).ToArray(), [], 2, 0, false, false, abilities?.Length ?? 0, 0, 0, keywords);

    public static readonly (string, Action)[] Tests =
    [
        ("the Phoenix table (block 9800, seed 99500072, turn 12): a Shugenja bowed for one Spell casts no second, a rescue of a defender is no answer against attackers, and 42 Force razes Strength 6 past 6", () =>
        {
            var spider = new[] { 8, 5, 5, 5, 4, 4, 4, 4, 3 }.Select((f, i) => L(Unit($"s{i}", 1, f), null)).ToArray();
            var tomohiro = K("tomohiro", 0, 4, ["earths-hunger-destroy", "unnatural-flood-move", "peace-of-the-kami-negate"], ["Shugenja", "Earth"]); var tsunefusa = K("tsunefusa", 0, 2, ["tsunefusa-bow"]);
            var table = new[] { "mountains-power-bow", "mountains-power-bow", "scouring-flood-move-home" }.Select(EffectDescriptors.Of).Where(d => d.IsBattle && d.Neutralises).Select(d => new Answer(d, d.Id, false, d.GoldCost)).ToArray();
            var phoenix = new LedgerSide(0, [tomohiro, tsunefusa], [(2, 6)], 22, 22, 24, false, 1, [], table); var them = new LedgerSide(1, spider, [(0, 7)], 32, 32, 4, false, 3, [], []);
            var r = Exchange.Run(them, spider, phoenix, [tomohiro, tsunefusa], 6, [], []);
            Check(r.Falls, $"after a destroy, a bow and two Holding bows, 19 against 6 + 6 still razes: {r}");
            Check(!r.DefenderPlayed.Any(p => p.StartsWith("SendHome@tomohiro")) && !r.DefenderPlayed.Any(p => p.Contains("scouring-flood")), $"the bowed Shugenja casts no second Spell and the defender's rescue targets no attacker: {string.Join(",", r.DefenderPlayed)}");
        }),
        ("resolution by the rules: strictly more than defenders plus Strength razes; equal Force is a tie that destroys both armies and no Province; a bowed unit counts nothing and dies", () =>
        {
            var a = new[] { L(Unit("a1", 0, 8)) }; var d = new[] { L(Unit("d1", 1, 1)) };
            var raze = Exchange.Run(Side(0), a, Side(1), d, 7, [], []); Check(!raze.Falls && raze.AttackerWins && raze.DefendersDestroyed.Length == 1, $"8 v 1 + 7 is not strictly more: {raze}");
            var over = Exchange.Run(Side(0), [L(Unit("a1", 0, 9))], Side(1), d, 7, [], []); Check(over.Falls, $"9 v 1 + 7 razes: {over}");
            var tie = Exchange.Run(Side(0), [L(Unit("a1", 0, 5))], Side(1), [L(Unit("d1", 1, 5))], 7, [], []); Check(tie.Tie && !tie.Falls && tie.AttackersDestroyed.Length == 1 && tie.DefendersDestroyed.Length == 1, $"a tie destroys both: {tie}");
            var bowed = Exchange.Run(Side(0), [L(Unit("a1", 0, 5))], Side(1), [L(Unit("d1", 1, 3)), L(Unit("d2", 1, 9, bowed: true))], 7, [], []);
            Check(bowed.AttackerWins && bowed.DefendersDestroyed.Length == 2 && bowed.DefenceForce == 3, $"the bowed 9 counts nothing and still dies: {bowed}");
        }),
        ("need and have: the fewest defenders to neutralise, and the neutralisers usable now (a spent or bowed unit's do not count; the Favor counts for its holder)", () =>
        {
            var a = new[] { L(Unit("a1", 0, 6)), L(Unit("a2", 0, 4)) }; var d = new[] { L(Unit("d1", 1, 5)), L(Unit("d2", 1, 2)), L(Unit("d3", 1, 1)) };
            Check(Exchange.Need(a, d, 3) == 1, $"10 v 8 + 3: one defender (the 5) must go: {Exchange.Need(a, d, 3)}");
            Check(Exchange.Need(a, d, 20) is null, "nobody can raze Strength 20");
            var bow = FirstAbility(x => x.Class == EffectClass.Bow && x.Side == TargetSide.Enemy && x.Conditions.Length == 0 && x.GoldCost == 0);
            var withBow = L(Unit("d4", 1, 2, abilities: [bow])); var spent = withBow with { Spent = [bow] }; var bowedUnit = withBow with { Bowed = true };
            Check(Exchange.Have([withBow], Side(1), []).Length == 1 && Exchange.Have([spent], Side(1), []).Length == 0 && Exchange.Have([bowedUnit], Side(1), []).Length == 0, "a printed bow counts once, not when spent or bowed");
            Check(Exchange.Have([L(Unit("d5", 1, 2))], Side(1, favor: true), []).Length == 1, "the Favor is a send-home for its holder");
        }),
        ("the defender acts first and a send-home keeps the Province: the attacker that goes home survives; nobody at the battlefield means no presence and no answers", () =>
        {
            var bow = FirstAbility(x => x.Class == EffectClass.Bow && x.Side == TargetSide.Enemy && x.Conditions.Length == 0 && x.GoldCost == 0);
            var a = new[] { L(Unit("a1", 0, 9)) }; var d = new[] { L(Unit("d1", 1, 1)) };
            var held = Exchange.Run(Side(0), a, Side(1, favor: true), d, 7, [], []);
            Check(!held.Falls && held.AttackersDestroyed.Length == 0 && held.DefenderPlayed.Length == 1 && held.AttackForce == 0, $"the Favor sends the 9 home before anything else: {held}");
            var empty = Exchange.Run(Side(0), a, Side(1, favor: true), [], 7, [], []);
            Check(empty.Falls && empty.DefenderPlayed.Length == 0, $"with no defender there the Favor cannot be used and the Province falls: {empty}");
            var bowFirst = Exchange.Run(Side(0), [L(Unit("a1", 0, 6)), L(Unit("a2", 0, 3))], Side(1), [L(Unit("d1", 1, 4, abilities: [bow]))], 7, [], []);
            Check(!bowFirst.Falls && bowFirst.DefenderPlayed.Length == 1 && bowFirst.DefenderPlayed[0].Contains("a1"), $"the defender bows the largest attacker first: {bowFirst}");
        }),
        ("a Ranged Attack cannot hit a Personality with a Follower; a Force cut takes its number and no more", () =>
        {
            var ranged = FirstAbility(x => x.Class == EffectClass.Destroy && x.Magnitude == 4 && x.Conditions.Any(c => c.Kind == "without-followers") && x.GoldCost == 0 && !x.BowsSource);
            var shooter = L(Unit("d1", 1, 2, abilities: [ranged]));
            var bare = Exchange.Run(Side(0), [L(Unit("a1", 0, 4))], Side(1), [shooter], 7, [], []);
            Check(bare.AttackersDestroyed.Length == 1 && !bare.Falls, $"a bare 4F attacker dies to Ranged 4: {bare}");
            var escorted = L(Unit("a1", 0, 4)) with { Followers = 1, UnitForce = 6, Force = 4 };
            var safe = Exchange.Run(Side(0), [escorted], Side(1), [shooter], 7, [], []);
            Check(safe.DefenderPlayed.Length == 0, $"with a Follower the Personality is out of the Ranged Attack's reach: {safe}");
            var cut = FirstAbility(x => x.Class == EffectClass.ForceCut && x.Magnitude == 4 && x.Side == TargetSide.Enemy && x.Conditions.Length == 0 && x.GoldCost == 0);
            var cutter = L(Unit("d1", 1, 3, abilities: [cut]));
            var cutRun = Exchange.Run(Side(0), [L(Unit("a1", 0, 9))], Side(1), [cutter], 3, [], []);
            Check(cutRun.AttackForce == 5 && !cutRun.Falls, $"9 - 4 = 5 is not more than 3 + 3: {cutRun}");
        }),
        ("late Force: a Tactician with a card in hand adds its Focus Value, so 7 against Strength 7 razes; without a hand it does not (PM 3.5, GT T5)", () =>
        {
            var tactician = L(Unit("a1", 0, 7)) with { Keywords = ["Tactician"] };
            var withHand = Exchange.Run(Side(0) with { HandCount = 3, BestFocus = 3 }, [tactician], Side(1), [], 7, [], []);
            Check(withHand.Falls && withHand.AttackerPlayed.Length == 1 && withHand.AttackerPlayed[0].Contains("TA"), $"7 + 3 > 7 with Tactical Advantage: {withHand}");
            var empty = Exchange.Run(Side(0), [tactician], Side(1), [], 7, [], []);
            Check(!empty.Falls, $"no card to discard: {empty}");
        }),
        ("the belief: a hand of eight with nothing seen holds the prior; three send-homes shown raise that class; the upper bound never exceeds the hand", () =>
        {
            var game = MatchEngine.CreateStarterMatch(); var view = game.GetView(0);
            var them = view.Players[1] with { HandCount = 8, Discard = [] };
            var fresh = Belief.Of(view, them);
            Check(fresh.Likely(EffectClass.Bow) == 1 && fresh.Worst(EffectClass.Bow) == 2 && fresh.Seen == 0, $"prior: {fresh}");
            var sendHome = AbilityCatalog.All.Keys.Where(k => !k.StartsWith("training-")).Select(EffectDescriptors.Of).First(d => d.IsBattle && d.Class == EffectClass.SendHome && d.Side == TargetSide.Enemy).Id;
            var strategy = CardCatalog.Cards.Values.First(c => c.Type == CardType.Strategy) with { Abilities = [sendHome] };
            var shown = them with { Discard = Enumerable.Range(0, 3).Select(i => new CardView("s" + i, strategy, 1, Zone.FateDiscard, true, false, null, null, 0)).ToArray() };
            var after = Belief.Of(view, shown);
            Check(after.Seen == 3 && after.Share[EffectClass.SendHome] > fresh.Share[EffectClass.SendHome] && after.Likely(EffectClass.SendHome) >= 2, $"three send-homes seen: {after}");
            Check(Belief.Classes.All(c => (after with { HandCount = 1 }).Worst(c) <= 1), "the upper bound is capped by the hand");
        }),
        ("the Read in shadow drives nothing: with rule read on, the Contender's choices on a battle position are unchanged and the log holds the exchange", () =>
        {
            var view = BattlePolicyTests.Battle(6, 4, 7, botAttacks: true);
            var pass = BattlePolicyTests.Pass(view); var use = BattlePolicyTests.Use(view, FirstAbility(x => x.Class == EffectClass.Bow && x.Side == TargetSide.Enemy && x.Conditions.Length == 0), target: "enemy-main");
            var off = TrainingOpponent.Choose(view, [pass, use], BotContext.Contender()).Command; var on = BotContext.Contender(rules: [.. BotContext.T2Rules, "read"]);
            var with = TrainingOpponent.Choose(view, [pass, use], on).Command;
            Check(HumanReplayCompare.Key(off!) == HumanReplayCompare.Key(with!), "the shadow Read changed a choice");
            Check(on.ReadLog.Count == 1 && on.ReadLog[0].Contains("P1 s7 1v1"), $"the Read was logged: {string.Join(" / ", on.ReadLog)}");
        })
    ];
}
