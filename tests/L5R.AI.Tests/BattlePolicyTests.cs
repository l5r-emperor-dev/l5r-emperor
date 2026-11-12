using L5R.AI;
using L5R.Core;

// Issue #788: Mendon's six bot changes, each pinned with a small synthetic table. The policy is card-agnostic, so the abilities used here are found in the catalogue by what
// they do rather than by name.
internal static class BattlePolicyTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Battle odds: the attacker needs more than defenders plus Strength; the spare Force is what a unit may leave with (16 v 4, Strength 7 spares 4)", Odds),
        ("Pass when won (rule 1): a winning defender or a province-destroying attacker passes instead of acting, unless the action gains or takes Honor", PassWhenWon),
        ("Pass when won (rule 1): a spare unit moves to an opposed battlefield, a needed one stays, and a threatening enemy Personality is still removed", WinningMoves),
        ("In-play abilities come before hand cards when the enemy has little to answer with (rule 1)", InPlayFirst),
        ("Attack targets (rule 2): the highest Force the attack can destroy, ties to the most printed Battle actions, then the Personality, augments counted", AttackTargets),
        ("Attack target step: the champion and the Contender take the largest card they can destroy, whatever order the targets are listed, and never their own", AttackTargetStep),
        ("Bow targets (rule 3): most attachments, then gold; without attachments a destroyer, a bow ability, then other Personalities", BowTargets),
        ("Losing defender (rule 4): sends a non-contributing unit home to save it, a fighting unit only when the Province still holds", LosingDefender),
        ("First-action reactions (rule 5): claimed only when the battle is not already won", EngageReactions),
        ("Border Keep (rule 6): refreshes once with 22 Gold and three stocked provinces, never with 21", BorderKeep),
        ("Outcome ladder: defender lose-and-lose-Province < lose < tie < win; attacker lose < tie < win < win-and-destroy", OutcomeLadder),
        ("Equip plan: at most three attachments, biggest Force gain first, within the Gold available", EquipPlanLimits),
        ("Equip (item 2): a bot that has already won equips while its hand is at the limit and passes once it is under it", EquipAtHandLimit),
        ("Parked rules: the six battle rules and trade attacks are off unless switched on; the equip rules are on", ParkedRulesAreOffByDefault),
        ("Equip (item 2): equipping is chosen when 3 or fewer cards change the outcome, and not when they cannot", EquipChangesOutcome),
        ("Equip (item 1): an attachment is valued by its best Battle ability in strategy order, and the granted additional action is always used", EquipAttachmentPriority),
        ("Trade attacks: a battle won outright is worth making without razing, a tie only when the defenders are worth clearly more, attachments and Personalities count", TradeAttacks),
        ("Trade attacks: the bot declares and assigns such an attack, and not when the rule is off or the defence is too strong", TradeAttackChoice),
        ("Issue #1019: sending one of its own units home to save it (Block Supply Lines), the bot saves the unit with the highest printed Gold cost counting its attachments, not the bare one", SupplyLinesSavesCostliest),
    ];

    internal static CardView Home(string id, int owner, int force, int gold, string? attachedTo = null, CardType? type = null)
    {
        var def = type is null ? Person() with { GoldCost = gold } : CardCatalog.Cards.Values.First(d => d.Type == type) with { GoldCost = gold };
        return new CardView(id, def, owner, Zone.Home, true, false, null, attachedTo, force);
    }

    private static void SupplyLinesSavesCostliest()
    {
        // Two attackers of Force 3 lose to 12. own-main is bare (2 Gold); own-rich is 2 Gold with a 5-Gold attachment, so it is worth more.
        var main = Unit("own-main", 0, 3, def: Person() with { GoldCost = 2 });
        var rich = Unit("own-rich", 0, 3, def: Person() with { GoldCost = 2 });
        var view = Battle(3, 12, 7, true, [rich]);
        view = view with { Players = [view.Players[0] with { Army = [main, rich], Home = [Home("own-gear", 0, 0, 5, attachedTo: "own-rich", type: CardType.Item)] }, view.Players[1]] };
        var use = Use(view, "block-supply", "own-main") with { TargetIds = ["own-main", "own-rich"] };
        var command = TrainingOpponent.Choose(view, [Pass(view), use]).Command;
        Check(command?.AbilityId == "block-supply" && command.TargetId == "own-rich", "it sends home the unit with the attachment (got " + (command?.AbilityId ?? command?.Type) + " " + command?.TargetId + ")");
    }

    private static void OutcomeLadder()
    {
        // Defender, Strength 7: 5 v 13 loses the Province, 5 v 12 loses the battle but the Province holds, 5 v 5 ties, 6 v 5 wins.
        int Def(int own, int enemy, int gain = 0) => new BattlePolicy.Odds(false, own, enemy, 7).Tier(gain);
        Check(Def(5, 13) == 0 && Def(5, 12) == 1 && Def(5, 5) == 2 && Def(6, 5) == 3, $"defender ladder {Def(5, 13)}{Def(5, 12)}{Def(5, 5)}{Def(6, 5)}");
        Check(Def(5, 13, 1) == 1 && Def(4, 5, 1) == 2 && Def(4, 5, 2) == 3, "gains climb the defender's ladder");
        int Att(int own, int enemy, int gain = 0) => new BattlePolicy.Odds(true, own, enemy, 7).Tier(gain);
        Check(Att(3, 5) == 0 && Att(5, 5) == 1 && Att(6, 5) == 2 && Att(12, 5) == 2 && Att(13, 5) == 3, $"attacker ladder {Att(3, 5)}{Att(5, 5)}{Att(6, 5)}{Att(12, 5)}{Att(13, 5)}");
        Check(new BattlePolicy.Odds(true, 6, 5, null).Tier() == 3, "an attacker that wins at a battlefield with no Province has nothing more to destroy");
    }

    private static void EquipPlanLimits()
    {
        var plan = BattlePolicy.EquipPlan([("a", 1, 2), ("b", 4, 3), ("c", 3, 2), ("d", 2, 1), ("e", 0, 1)], 20);
        Check(plan.SequenceEqual(["b", "c", "d"]), "three best gains, none with no gain: " + string.Join(",", plan));
        Check(BattlePolicy.EquipPlan([("a", 5, 6), ("b", 2, 1), ("c", 1, 1)], 5).SequenceEqual(["b", "c"]), "a card the Gold cannot pay for is skipped");
    }

    /// <summary>An Equip option for an item in the bot's hand at <paramref name="cost"/> Gold, with no Battle ability of its own.</summary>
    private static (CardView Card, ActionOption Option) EquipOffer(MatchView v, string id, int force, int cost = 2)
    {
        var def = CardCatalog.Cards.Values.First(d => d.Type == CardType.Item && (d.Abilities ?? []).All(a => !AbilityCatalog.All.TryGetValue(a, out var ab) || ab.Timing is not ("Battle" or "BattleOrOpen"))) with { Force = force, GoldCost = cost };
        var card = new CardView(id, def, 0, Zone.Hand, true, false, null, null, force);
        return (card, new ActionOption("equip", "Equip", true, null, new("equip", 0, v.Version, id, TargetId: "own-main"), id, ["own-main"], Gold: new GoldQuote(cost, "Printed", "", null, [])));
    }

    private static MatchView WithHand(MatchView v, int handCount, params CardView[] hand) =>
        v with { Players = [v.Players[0] with { Hand = hand, HandCount = handCount, HandLimit = new HandLimitView(8, 8, []) }, v.Players[1]] };

    private static string ChoseType(MatchView v, params ActionOption[] options) => TrainingOpponent.Choose(v, options, SixRules).Command?.Type ?? "none";

    private static void EquipAtHandLimit()
    {
        var won = Battle(16, 4, 7);
        var (card, equip) = EquipOffer(won, "item-1", 0);
        var full = WithHand(won, 8, card);
        Check(ChoseType(full, Pass(full), equip) == "equip", "already won with a full hand: equips instead of passing");
        var under = WithHand(won, 7, card);
        Check(ChoseType(under, Pass(under), equip) == "pass", "already won with room in the hand: passes");
        var over = WithHand(won, 9, card);
        Check(ChoseType(over, Pass(over), equip) == "equip", "a hand over the limit equips too");
        var defender = WithHand(Battle(6, 4, 7, botAttacks: false), 8, card);
        Check(ChoseType(defender, Pass(defender), equip) == "equip", "a defender that is already winning equips with a full hand");
        var ruleOff = TrainingOpponent.Choose(under, [Pass(under), equip], new BotContext { UseEquipPolicy = false }).Command?.Type;
        Check(ruleOff == "equip", "with the equip rules off the ordinary equip score is untouched (it equips rather than passes)");
    }

    private static void ParkedRulesAreOffByDefault()
    {
        Check(!new BotContext().UseBattlePolicy && !new BotContext().UseTradeAttacks && new BotContext().UseEquipPolicy, "the six battle rules and trade attacks are off by default; the equip rules are on");
        var plain = FindAbility((id, a, i) => a.Timing == "Battle" && a.Target == "none" && !i.IsReaction && i.Rank == EffectRank.Help && !i.HonorGain && !i.HonorLoss && !i.Draws);
        var winning = Battle(16, 4, 7);
        Check(TrainingOpponent.Choose(winning, [Pass(winning), Use(winning, plain)]).Command?.AbilityId == plain, "by default a won battle still plays its ordinary abilities (the pass-when-won rule is parked)");
        var (card, equip) = EquipOffer(winning, "item-1", 0);
        var full = WithHand(winning, 8, card);
        Check(TrainingOpponent.Choose(full, [Pass(full), Use(full, plain), equip]).Command?.Type == "equip", "by default the equip rule still applies in a won battle with a full hand");
    }

    private static void EquipChangesOutcome()
    {
        // Attacker, Strength 7: 12 v 5 only wins the battle; one Force 1 item makes it 13, which razes the Province.
        var short_ = Battle(12, 5, 7);
        var (card, equip) = EquipOffer(short_, "item-1", 1);
        var view = WithHand(short_, 4, card);
        Check(ChoseType(view, Pass(view), equip) == "equip", "one card lifts a win to a win that razes the Province");
        // 5 v 5 is a tie; one Force turns it into a win.
        var tie = WithHand(Battle(5, 5, 7), 4, card);
        Check(ChoseType(tie, Pass(tie), equip) == "equip", "a tie changed to a win");
        // Three cards is the most the bot counts on: 6 v 10 needs four Force 1 cards to tie, so it does not spend on them (a defender at 6 v 10 stays on the same rung with three).
        var odds = new BattlePolicy.Odds(false, 6, 10, 7);
        var four = BattlePolicy.EquipPlan([("i1", 1, 1), ("i2", 1, 1), ("i3", 1, 1), ("i4", 1, 1)], 10);
        Check(four.Length == 3 && odds.Tier(four.Length) == odds.Tier() && odds.Tier(4) > odds.Tier(), "6 v 10 needs four Force 1 cards to climb a rung and the plan stops at three, so no equip is justified by the outcome");
    }

    private static void EquipAttachmentPriority()
    {
        var spells = CardCatalog.Cards.Values.Where(d => d.Type == CardType.Spell).Select(d => (Def: d, Best: (d.Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var ab) && ab.Timing == "Battle")
            .Select(AbilityIntent.Of).Where(i => !i.IsReaction && i.Side != TargetSide.Own).Select(i => i.Rank).DefaultIfEmpty(EffectRank.Neutral).Max())).ToArray();
        var destroyer = spells.First(s => s.Best == EffectRank.Destroy).Def;
        var helper = spells.First(s => s.Best == EffectRank.Help).Def;
        var view = Battle(6, 7, 7);
        CardView Spell(string id, CardDefinition def) => new(id, def, 0, Zone.Hand, true, false, null, null, 0);
        ActionOption EquipBattle(string id) => new("useAbility", "Equip (Battle)", true, null, new("useAbility", 0, view.Version, "own-main", AbilityId: "equip-battle", TargetId: id), "own-main", [id]);
        var held = WithHand(view, 2, Spell("spell-help", helper), Spell("spell-destroy", destroyer));
        var pick = TrainingOpponent.Choose(held, [Pass(held), EquipBattle("spell-help"), EquipBattle("spell-destroy")], SixRules).Command;
        Check(pick?.AbilityId == "equip-battle" && pick.TargetId == "spell-destroy", $"the spell that destroys is attached before the one that only helps: {pick?.TargetId}");
        // The additional action an attached spell grants: a battle already won still uses it, never declining.
        var won = Battle(16, 4, 7);
        var abilityId = destroyer.Abilities!.First(a => AbilityCatalog.All.TryGetValue(a, out var ab) && ab.Timing == "Battle");
        var attached = new CardView("spell-destroy", destroyer, 0, Zone.Attached, true, false, 0, "own-main", 0);
        var grantView = won with { Players = [won.Players[0] with { Army = [.. won.Players[0].Army, attached] }, won.Players[1]] };
        var decline = new ActionOption("declineAdditionalAction", "Decline", true, null, new("declineAdditionalAction", 0, grantView.Version));
        var use = new ActionOption("useAbility", abilityId, true, null, new("useAbility", 0, grantView.Version, "spell-destroy", AbilityId: abilityId, TargetId: "enemy-main"), "spell-destroy", ["enemy-main"]);
        Check(Chose(grantView, Pass(grantView), decline, use) == abilityId, "the granted additional action is used, not declined, even when the battle is won");
    }

    private static void TradeAttacks()
    {
        CardView[] none = [];
        // Won outright without razing: one Force 6 unit against a Force 4 defender worth 5 Gold plus the Personality bonus.
        var mine = new[] { Home("a", 0, 6, 3) };
        var enemy = new[] { Home("d", 1, 4, 5) };
        var win = BattlePolicy.PlanTrade(mine, mine, enemy);
        Check(win is { Tie: false, Losses: 0, Kills: 7, Net: 7 }, $"a won battle nets the defenders' worth (7): {win}");
        Check(BattlePolicy.PlanTrade([Home("w", 0, 3, 3)], [Home("w", 0, 3, 3)], enemy) is null, "Force 3 against a Force 4 defence loses the army for nothing: no trade");
        Check(BattlePolicy.PlanTrade(mine, mine, none) is null, "nothing to destroy, nothing to trade");
        // A tie destroys both armies: worthwhile only when the defenders are worth clearly more than the attackers.
        var even = new[] { Home("a", 0, 4, 2) };
        var rich = BattlePolicy.PlanTrade(even, even, [Home("d", 1, 4, 9)]);
        Check(rich is { Tie: true, Net: 3 }, $"a tie against a 9 Gold defender (11 with the bonus) for a 2 Gold attacker (4): net 3, {rich}");
        Check(BattlePolicy.PlanTrade(even, even, [Home("d", 1, 4, 3)]) is null, "a tie against a cheaper defender is a bad trade");
        // Attachments are part of the defending unit's worth.
        var armed = new[] { Home("d", 1, 4, 3), Home("blade", 1, 0, 6, "d", CardType.Item) };
        var withItem = BattlePolicy.PlanTrade(even, even, armed);
        Check(withItem is { Tie: true } && withItem.Kills == 11, $"the attachment's 6 Gold is destroyed with its Personality: kills {withItem?.Kills}");
        // Sends the smallest sufficient subset with a spare point of Force, keeping the rest home.
        var big = Home("big", 0, 9, 4); var small = Home("small", 0, 2, 1);
        var subset = BattlePolicy.PlanTrade([big, small], [big, small], enemy);
        Check(subset is { UnitIds: ["big"] }, $"the Force 9 unit alone beats a Force 4 defence with room to spare: [{string.Join(",", subset?.UnitIds ?? [])}]");
    }

    private static MatchView AttackView(int ownForce, int enemyForce, int enemyGold, int strength)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        ProvinceView[] Provinces(int owner) => view.Players[owner].Provinces.Select(p => p with { Strength = strength, Destroyed = false }).ToArray();
        return view with
        {
            Phase = Phase.AttackDeclaration,
            ActivePlayerId = 0,
            PriorityPlayerId = 0,
            Players = [view.Players[0] with { Home = [Home("a", 0, ownForce, 3)], Army = [], Provinces = Provinces(0) },
                view.Players[1] with { Home = [Home("d", 1, enemyForce, enemyGold)], Army = [], Provinces = Provinces(1) }]
        };
    }

    private static void TradeAttackChoice()
    {
        ActionOption Declare(MatchView v) => new("declareAttack", "Declare", true, null, new("declareAttack", 0, v.Version));
        ActionOption Attack(MatchView v) => new("attack", "Attack", true, null, new("attack", 0, v.Version, Assignments: []));
        var on = new BotContext { UseBattlePolicy = true, UseTradeAttacks = true, UseSpec818 = false };
        var strong = AttackView(6, 4, 5, 9); // beats the defender, cannot raze a Strength 9 Province
        Check(TrainingOpponent.Choose(strong, [Pass(strong), Declare(strong)], on).Command?.Type == "declareAttack", "a battle it can win outright is declared even though the Province would hold");
        var assigned = TrainingOpponent.Choose(strong, [Pass(strong), Attack(strong)], on).Command;
        Check(assigned is { Type: "attack", Assignments: { Count: 1 } } && assigned.Assignments.ContainsKey("a"), "the winning unit is assigned to the attack");
        var off = TrainingOpponent.Choose(strong, [Pass(strong), Declare(strong)], SixRules).Command;
        Check(off?.Type == "pass", "the rule is off by default: the old caution holds, no declaration");
        var weak = AttackView(3, 4, 5, 9);
        Check(TrainingOpponent.Choose(weak, [Pass(weak), Declare(weak)], on).Command?.Type == "pass", "a defence that would win is not attacked");
        var razing = AttackView(12, 4, 5, 3); // 12 > 4 + 3: the ordinary planner razes the Province
        Check(TrainingOpponent.Choose(razing, [Pass(razing), Declare(razing)], on).Command?.Type == "declareAttack", "an attack that razes the Province is unchanged");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static CardDefinition Person(int force = 4) => CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force == force);
    internal static CardView Unit(string id, int owner, int force, int province = 0, bool bowed = false, CardDefinition? def = null) =>
        new(id, def ?? Person(), owner, Zone.Battlefield, true, bowed, province, null, force);
    /// <summary>True when the ability's cost (the text before the colon) bows something, which would take Force out of the battle.</summary>
    private static bool BowCost(AbilityDefinition a) => (a.Label ?? "").Split(':') is { Length: > 1 } parts && parts[0].Contains("Bow", StringComparison.OrdinalIgnoreCase);
    private static string FindAbility(Func<string, AbilityDefinition, AbilityIntent, bool> test) =>
        AbilityCatalog.All.Where(p => !p.Key.StartsWith("training-", StringComparison.Ordinal) && test(p.Key, p.Value, AbilityIntent.Of(p.Value))).OrderBy(p => p.Key, StringComparer.Ordinal).First().Key;

    /// <summary>A battle at province 0 with seat 0 (the bot) holding <paramref name="own"/> Force against <paramref name="enemy"/>; every Province has Strength <paramref name="strength"/>.</summary>
    internal static MatchView Battle(int own, int enemy, int strength, bool botAttacks = true, CardView[]? ownExtra = null, CardView[]? enemyExtra = null, int enemyHand = 5)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        ProvinceView[] Provinces(int owner) => view.Players[owner].Provinces.Select(p => p with { Strength = strength }).ToArray();
        var mine = new List<CardView> { Unit("own-main", 0, own) }; mine.AddRange(ownExtra ?? []);
        var theirs = new List<CardView> { Unit("enemy-main", 1, enemy) }; theirs.AddRange(enemyExtra ?? []);
        return view with
        {
            Phase = Phase.Battle,
            CurrentBattleProvince = 0,
            ActivePlayerId = botAttacks ? 0 : 1,
            PriorityPlayerId = 0,
            Players = [view.Players[0] with { Army = mine.ToArray(), Home = [], Provinces = Provinces(0) },
                view.Players[1] with { Army = theirs.ToArray(), Home = [], HandCount = enemyHand, Provinces = Provinces(1) }]
        };
    }

    internal static ActionOption Pass(MatchView v) => new("pass", "Pass", true, null, new("pass", 0, v.Version));
    internal static ActionOption Use(MatchView v, string id, string? card = "own-main", string? target = null, string[]? targets = null) =>
        new("useAbility", id, true, null, new("useAbility", 0, v.Version, CardId: card, AbilityId: id, TargetId: target), card, targets);
    /// <summary>The six parked battle rules are off by default; their tests switch them on.</summary>
    private static BotContext SixRules => new() { UseBattlePolicy = true, UseSpec818 = false }; // the older rules are measured without the issue #818 changes
    private static string Chose(MatchView v, params ActionOption[] options) => TrainingOpponent.Choose(v, options, SixRules).Command is { } c ? c.AbilityId ?? c.Type : "none";

    private static void Odds()
    {
        var o = new BattlePolicy.Odds(true, 16, 4, 7);
        Check(o.Satisfied && o.Winning && o.Margin == 4, "16 against 4 with Strength 7 destroys the Province and spares 4 Force: " + o.Margin);
        Check(!new BattlePolicy.Odds(true, 11, 4, 7).Satisfied && new BattlePolicy.Odds(true, 11, 4, 7).Winning && new BattlePolicy.Odds(true, 12, 4, 7).Satisfied, "11 against 4 with Strength 7 wins the battle but not the Province (needs more than 11)");
        Check(new BattlePolicy.Odds(false, 5, 5, 7).Satisfied && !new BattlePolicy.Odds(false, 4, 5, 7).Winning, "a tie goes to the defender");
        var holding = new BattlePolicy.Odds(false, 8, 12, 7);
        Check(!holding.WouldFallAfterLosing(0) && !holding.WouldFallAfterLosing(3) && holding.WouldFallAfterLosing(4), "a defender 8 v 12 holds a Strength 7 Province, and would lose it only after losing 4 Force");
    }

    private static void PassWhenWon()
    {
        var plain = FindAbility((id, a, i) => a.Timing == "Battle" && a.Target == "none" && !i.IsReaction && i.Rank == EffectRank.Help && !i.HonorGain && !i.HonorLoss && !i.Draws);
        var gain = FindAbility((id, a, i) => a.Timing is "Battle" or "BattleOrOpen" && a.Target == "none" && !i.IsReaction && i.HonorGain && !i.HonorLoss && i.Rank == EffectRank.Help && !BowCost(a));
        var winning = Battle(16, 4, 7);
        Check(Chose(winning, Pass(winning), Use(winning, plain)) == "pass", "a province-destroying attacker passes instead of acting");
        Check(Chose(winning, Pass(winning), Use(winning, gain)) == gain, "but an action that gains Honor is still taken");
        var short_ = Battle(8, 4, 7);
        Check(Chose(short_, Pass(short_), Use(short_, plain)) == plain, "an attacker that does not yet destroy the Province keeps acting");
        var defender = Battle(6, 4, 7, botAttacks: false);
        Check(Chose(defender, Pass(defender), Use(defender, plain)) == "pass", "a defender that is winning passes");
        var losing = Battle(3, 6, 7, botAttacks: false);
        Check(Chose(losing, Pass(losing), Use(losing, plain)) == plain, "a defender that is losing keeps acting");
    }

    private static void WinningMoves()
    {
        var move = FindAbility((id, a, i) => a.Timing is "Battle" or "BattleOrOpen" && BattlePolicy.Moves(id) && a.Target.StartsWith("own", StringComparison.Ordinal));
        var spare = Unit("own-spare", 0, 3, 0);
        var needed = Unit("own-needed", 0, 6, 0);
        var opposedElsewhere = Unit("enemy-elsewhere", 1, 2, 1);
        MatchView View(CardView mover) => Battle(16 - mover.Force, 4, 7, ownExtra: [mover], enemyExtra: [opposedElsewhere]);
        ActionOption MoveOption(MatchView v, CardView mover) => new("useAbility", move, true, null, new("useAbility", 0, v.Version, CardId: "own-main", AbilityId: move, TargetId: mover.Id), "own-main", [mover.Id], [1], Movement: new MoveDestinationView(1, [], true));
        var v1 = View(spare); var v2 = View(needed);
        Check(Chose(v1, Pass(v1), MoveOption(v1, spare)) == move, "a unit whose Force fits inside the spare margin moves to the opposed battlefield");
        Check(Chose(v2, Pass(v2), MoveOption(v2, needed)) == "pass", "a unit the win depends on stays");
        var threatDef = CardCatalog.Cards.Values.FirstOrDefault(d => d.Type == CardType.Personality && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && AbilityIntent.Of(a) is { Rank: EffectRank.Destroy or EffectRank.Bow, Side: not TargetSide.Own } && a.Timing is "Battle" or "BattleOrOpen") == true);
        Check(threatDef is not null, "the catalogue has a Personality that destroys or bows");
        var threat = Unit("enemy-threat", 1, 2, 0, def: threatDef);
        var bow = FindAbility((id, a, i) => a.Timing == "Battle" && i.Rank == EffectRank.Bow && i.Side == TargetSide.Enemy && !i.IsReaction && a.Target == "enemy-personality" && !BowCost(a) && !a.IssuesDuel && (a.Label ?? "").StartsWith("Bow", StringComparison.Ordinal));
        var fight = Battle(16, 4, 7, enemyExtra: [threat]);
        Check(Chose(fight, Pass(fight), Use(fight, bow, target: "enemy-threat", targets: ["enemy-threat"])) == bow, "an enemy that could undo the result is bowed even though the battle is won");
        var harmless = Battle(16, 4, 7);
        Check(Chose(harmless, Pass(harmless), Use(harmless, bow, target: "enemy-main", targets: ["enemy-main"])) == "pass", "bowing an enemy that cannot do anything is not needed");
    }

    private static void InPlayFirst()
    {
        var plain = FindAbility((id, a, i) => a.Timing == "Battle" && a.Target == "none" && !i.IsReaction && i.Rank == EffectRank.Help && !i.HonorGain && !i.HonorLoss && !i.Draws);
        var calm = Battle(8, 4, 7, enemyHand: 0);
        var inPlay = new ActionOption("useAbility", plain, true, null, new("useAbility", 0, calm.Version, CardId: "own-main", AbilityId: plain), "own-main");
        var fromHand = new ActionOption("useAbility", plain, true, null, new("useAbility", 0, calm.Version, CardId: "hand-1", AbilityId: plain), "hand-1");
        var withHand = calm with { Players = [calm.Players[0] with { Hand = [new CardView("hand-1", Person(2), 0, Zone.Hand, true, false, null, null, 0)] }, calm.Players[1]] };
        var decision = TrainingOpponent.Choose(withHand, [Pass(withHand), fromHand, inPlay], SixRules).Command;
        Check(decision?.CardId == "own-main", "against an enemy with nothing in hand the ability already in play comes before the one from the hand: " + decision?.CardId);
    }

    private static void AttackTargets()
    {
        static int Battles(CardDefinition d) => d.Abilities?.Count(id => AbilityCatalog.All.TryGetValue(id, out var ab) && ab.Timing is "Battle" or "BattleOrOpen") ?? 0;
        CardView T(string id, int force, CardDefinition def, bool bowed = false) => new(id, def, 1, Zone.Battlefield, true, bowed, 0, null, force);
        var people = CardCatalog.Cards.Values.Where(d => d.Type == CardType.Personality).ToArray();
        var plain = people.First(d => Battles(d) == 0);
        var busy = people.OrderByDescending(Battles).First();
        var busier = people.First(d => Battles(d) > 0 && Battles(d) < Battles(busy));
        Check(Battles(busy) > Battles(busier) && Battles(busier) > 0, "the catalogue has Personalities with different numbers of printed Battle actions");
        var all = new List<CardView>();
        double Score(CardView t, int strength) => BattlePolicy.AttackTargetScore(t, strength, all);
        string First(int strength, params CardView[] targets) => targets.OrderByDescending(t => Score(t, strength)).First().Id;

        // The largest Force the Attack can destroy comes first, whatever else the cards print; what it cannot destroy comes last.
        var big = T("big", 3, plain); var small = T("small", 2, busy); var tooBig = T("too-big", 5, busy);
        Check(First(4, small, big, tooBig) == "big", "strength 4: the Force 3 target before the Force 2 one with more Battle actions, and the Force 5 one it cannot destroy last");
        Check(First(6, small, big, tooBig) == "too-big", "an augment that lifts the attack to 6 makes the Force 5 target the best");
        Check(new[] { small, big, tooBig }.OrderByDescending(t => Score(t, 1)).First().Id == "small", "when nothing can be destroyed the weakest is nearest to falling");

        // Same Force: the card with the most printed Battle actions it could still use.
        var few = T("few", 3, busier); var many = T("many", 3, busy); var none = T("none", 3, plain);
        Check(First(4, none, few, many) == "many" && First(4, many, none, few) == "many", "a tie in Force goes to the card with the most printed Battle actions");
        Check(First(4, none, few) == "few", "a printed Battle action beats none");
        Check(First(4, T("bowed-many", 3, busy, bowed: true), few) == "few", "a bowed card cannot use its Battle actions, so they do not count");
        var spentIds = busy.Abilities!.Where(id => AbilityCatalog.All.TryGetValue(id, out var ab) && ab.Timing is "Battle" or "BattleOrOpen").ToArray();
        Check(First(4, many with { SpentAbilityIds = spentIds }, few) == "few", "Battle actions a card has already used this turn do not count");

        // Still tied: the Personality over the Follower.
        var follower = CardCatalog.Cards.Values.First(d => d.Type == CardType.Follower && Battles(d) == 0);
        var person = T("person", 3, plain); var attached = T("follower", 3, follower);
        Check(First(4, attached, person) == "person" && First(4, person, attached) == "person", "an equal Personality and Follower: the Personality");
        var attack = FindAbility((id, ab, i) => ab.Target == "attack" && BattlePolicy.AttackStrength(id) is 4);
        Check(BattlePolicy.AttackStrength(attack) == 4, "the attack's printed strength is read from its label");
    }

    // The engine's own "choose the Attack's target" step: the champion and the Contender both take the largest card they can destroy, never one of their own, and leave the order the targets are listed in out of it.
    private static void AttackTargetStep()
    {
        var attack = FindAbility((id, ab, i) => ab.Target == "attack" && BattlePolicy.AttackStrength(id) is 4);
        var shooterDef = CardCatalog.Cards.Values.OrderBy(d => d.Type == CardType.Personality ? 0 : 1).First(d => d.Abilities?.Contains(attack) == true);
        CardView Foe(string id, int force) => Unit(id, 1, force);
        var shooter = Unit("shooter", 0, 3, def: shooterDef);
        var view = Battle(8, 6, 7, ownExtra: [shooter], enemyExtra: [Foe("foe-5", 5), Foe("foe-4", 4), Foe("foe-3", 3)]);
        ActionOption Step(string target) => new("chooseAttackTarget", "Attack", true, null, new("chooseAttackTarget", 0, view.Version, CardId: "shooter", TargetId: target), target);
        var none = new ActionOption("chooseAttackTarget", "Attack no target", true, null, new("chooseAttackTarget", 0, view.Version, CardId: "shooter", Choice: "none"), "shooter");
        foreach (var (name, bot) in new[] { ("champion", new BotContext()), ("Contender", BotContext.Contender()) })
        {
            foreach (var order in new[] { new[] { "foe-5", "foe-4", "foe-3" }, new[] { "foe-3", "foe-4", "foe-5" } })
            {
                var chosen = TrainingOpponent.Choose(view, order.Select(Step).Append(none).ToArray(), bot).Command;
                Check(chosen?.Type == "chooseAttackTarget" && chosen.TargetId == "foe-4", $"{name}: strength 4 destroys the Force 4 card, not the Force 5 one it cannot or the Force 3 one it need not ({chosen?.TargetId}, listed {string.Join(",", order)})");
            }
            var own = TrainingOpponent.Choose(view, [Step("own-main"), Step("foe-4"), none], bot).Command;
            Check(own?.TargetId == "foe-4", $"{name}: never one of its own cards while a card of the opponent's is on offer ({own?.TargetId})");
        }
    }

    private static void BowTargets()
    {
        CardView P(string id, int force, CardDefinition? def = null) => Unit(id, 1, force, def: def ?? Person());
        CardView Att(string id, string host) => new(id, CardCatalog.Cards.Values.First(d => d.Type == CardType.Item), 1, Zone.Battlefield, true, false, 0, host, 0);
        var one = P("one", 4); var two = P("two", 3); var none = P("none", 5);
        var all = new List<CardView> { one, two, none, Att("i1", "one"), Att("i2", "two"), Att("i3", "two") };
        Check(BattlePolicy.BowOrder([one, two, none], all, true).First().Id == "two", "most attachments first");
        var destroyer = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Target == "attack") == true);
        var plain = P("plain", 5); var shooter = P("shooter", 2, destroyer);
        Check(BattlePolicy.BowOrder([plain, shooter], [plain, shooter], false).First().Id == "shooter", "without attachments a Personality that destroys (an Attack counts) comes before a stronger plain one");
        var destroyingItem = CardCatalog.Cards.Values.FirstOrDefault(d => d.Type == CardType.Item && d.Abilities?.Any(id => AbilityCatalog.All.TryGetValue(id, out var a) && AbilityIntent.Of(a).Rank == EffectRank.Destroy) == true);
        if (destroyingItem is not null)
        {
            var item = new CardView("item", destroyingItem, 1, Zone.Battlefield, true, false, 0, "plain", 0);
            Check(BattlePolicy.BowOrder([plain, shooter, item], [plain, shooter, item], false).First().Id == "shooter", "a destroying Personality outranks a destroying attachment");
            Check(BattlePolicy.BowOrder([plain, item], [plain, item], false).First().Id == "item", "a destroying attachment outranks a plain Personality");
        }
    }

    private static void LosingDefender()
    {
        var home = FindAbility((id, a, i) => a.Timing == "Battle" && BattlePolicy.SendsHome(id) && a.Target == "defending");
        var bowed = Unit("own-bowed", 0, 0, 0, bowed: true);
        MatchView Defend(int own, int enemy, CardView[]? extra = null) => Battle(own, enemy, 7, botAttacks: false, ownExtra: extra);
        ActionOption Send(MatchView v, string unit) => Use(v, home, "own-main", unit, [unit]);
        var saveBowed = Defend(3, 6, [bowed]);
        Check(Chose(saveBowed, Pass(saveBowed), Send(saveBowed, "own-bowed")) == home, "a bowed unit that adds no Force is sent home to be saved");
        var holds = Defend(5, 6);
        Check(Chose(holds, Pass(holds), Send(holds, "own-main")) == home, "a lone fighting unit (5 v 6) may leave: the attacker's 6 does not beat 0 plus Strength 7");
        var falls = Defend(5, 14, [Unit("own-two", 0, 4, 0)]);
        Check(Chose(falls, Pass(falls), Send(falls, "own-two")) == "pass", "but not when leaving would let the Province fall (9 v 14 holds Strength 7; 5 v 14 does not)");
    }

    private static void EngageReactions()
    {
        var reaction = FindAbility((id, a, i) => a.Timing == "Reaction" && !i.Negates && a.Target != "none" && i.Rank == EffectRank.Bow && i.Side == TargetSide.Enemy);
        var close = Battle(6, 6, 7) with { PendingChoice = new("engageReaction", 0, null, "Engage") };
        var won = Battle(16, 4, 7) with { PendingChoice = new("engageReaction", 0, null, "Engage") };
        ActionOption React(MatchView v) => new("useAbility", reaction, true, null, new("useAbility", 0, v.Version, CardId: "own-main", AbilityId: reaction, TargetId: "enemy-main"), "own-main", ["enemy-main"]);
        ActionOption Decline(MatchView v) => new("declineReaction", "Decline", true, null, new("declineReaction", 0, v.Version));
        Check(Chose(close, Decline(close), React(close)) == reaction, "an unsettled battle: the first-action reaction is played");
        Check(Chose(won, Decline(won), React(won)) == "declineReaction", "an already won battle: the bot would only pass with the first action, so it declines");
    }

    private static void BorderKeep()
    {
        var holding = CardCatalog.Cards.Values.First(d => d.Type == CardType.Holding && d.GoldProduction >= 3);
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        MatchView WithGold(int gold)
        {
            var own = view.Players[0];
            var stronghold = own.Stronghold with { Definition = own.Stronghold.Definition! with { GoldProduction = gold - holding.GoldProduction * 2 } };
            var provinces = own.Provinces.Select((p, i) => p with { Destroyed = false, Card = i < 3 ? new CardView("prov-" + i, holding, 0, Zone.Province, true, false, i, null, 0) : null }).ToArray();
            var homes = new[] { new CardView("h1", holding, 0, Zone.Home, true, false, null, null, 0), new CardView("h2", holding, 0, Zone.Home, true, false, null, null, 0) };
            return view with { Phase = Phase.Action, ActivePlayerId = 0, PriorityPlayerId = 0, Players = [own with { Stronghold = stronghold, Home = homes, Provinces = provinces }, view.Players[1]] };
        }
        ActionOption Border(MatchView v) => new("useAbility", "border-game", true, null, new("useAbility", 0, v.Version, CardId: "keep", AbilityId: "border-game", CardIds: []), "keep", ["prov-0", "prov-1", "prov-2"]);
        var rich = WithGold(22);
        var decision = TrainingOpponent.Choose(rich, [Pass(rich), Border(rich)], SixRules).Command;
        Check(decision?.AbilityId == "border-game" && decision.CardIds is { Length: 3 }, "22 Gold and three stocked provinces: the Border Keep refreshes all three");
        var poor = WithGold(21);
        var held = TrainingOpponent.Choose(poor, [Pass(poor), Border(poor)], SixRules).Command;
        Check(held?.AbilityId != "border-game" || held.CardIds is not { Length: 3 }, "21 Gold: the refresh is not taken under the new rule");
    }
}
