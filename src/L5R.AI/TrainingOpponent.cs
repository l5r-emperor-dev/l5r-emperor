using L5R.Core;

namespace L5R.AI;

public sealed record OpponentDecision(GameCommand? Command, string Reason);

/// <summary>
/// A practice opponent. Its only inputs are one player's permitted view and offered actions (and, optionally, its own <see cref="BotContext"/> memory of what it
/// has seen), so it can never cheat. Since issue #642 it can answer ANY action the engine offers: a generic completer fills in targets, slots, payments and
/// costs from the metadata the engine ships, a card-agnostic <see cref="AbilityIntent"/> scores an ability it has no hand-written rule for, and nothing that is a
/// card play scores below passing. See Policy, Complete and Plans for the parts.
/// </summary>
public static partial class TrainingOpponent
{
    /// <summary>What passing is worth. No card play is ever worth less.</summary>
    internal const double PassScore = 10;

    public static OpponentDecision Choose(MatchView view, ActionOption[] actions) => Choose(view, actions, new BotContext());

    public static OpponentDecision Choose(MatchView view, ActionOption[] actions, BotContext memory)
    {
        if (view.WinnerId is not null) return new(null, "The match has finished.");
        // A seat waits unless the engine offers it something to do: a pending choice can belong to the player who does not hold priority (the province order, a reaction).
        if (view.ViewerId != view.PriorityPlayerId && !actions.Any(a => a.Enabled && a.Type is not ("concede" or "wait"))) return new(null, "Waiting for the other player's decision.");
        memory.Observe(view);
        var self = view.Players.Single(p => p.Id == view.ViewerId);
        var opponent = view.Players.Single(p => p.Id != view.ViewerId);
        var x = new Ctx(view, self, opponent, BotProfile.Assess(self, memory.DeckAveragePersonalHonor), memory, AllCards(view)) { Actions = actions, Odds = BattlePolicy.Assess(view) };
        var candidates = actions.Where(a => a.Enabled && a.Type is not ("concede" or "wait"))
            .Select(a => (Action: a, Command: Prepare(x, a)))
            .Where(p => p.Command is not null && !memory.IsRejected(view.Version, p.Command))
            .Select(p => (p.Action, Command: p.Command!, Score: Score(x, p.Action, p.Command!)))
            .OrderByDescending(p => p.Score).ThenBy(p => p.Action.Type, StringComparer.Ordinal)
            .ThenBy(p => p.Command.CardId, StringComparer.Ordinal).ThenBy(p => p.Command.AbilityId, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return new(null, "No complete legal action was supplied for this decision.");
        var chosen = candidates[0];
        Remember(x, chosen.Command);
        if (chosen.Command.Type is "useAbility" or "playStrategy" or "equip") memory.NoteUse(view.TurnNumber, chosen.Command.AbilityId, chosen.Command.CardId);
        return new(chosen.Command, Explain(chosen.Action.Type));
    }

    /// <summary>Everything one decision reads, built once from the permitted view.</summary>
    private sealed record Ctx(MatchView View, PlayerView Self, PlayerView Opponent, BotProfile Profile, BotContext Memory, Dictionary<string, CardView> Cards)
    {
        /// <summary>Every action offered at this decision (issue #788: a rule may need to know whether a better one exists).</summary>
        public ActionOption[] Actions { get; init; } = [];
        /// <summary>The Force picture of the battle in progress, or null outside one.</summary>
        public BattlePolicy.Odds? Odds { get; init; }
        public int MaxHand => Self.HandLimit?.Maximum ?? 8;
        public CardView? Card(string? id) => id is not null && Cards.TryGetValue(id, out var c) ? c : null;
    }

    private static Dictionary<string, CardView> AllCards(MatchView view)
    {
        var map = new Dictionary<string, CardView>(StringComparer.Ordinal);
        foreach (var p in view.Players)
            foreach (var c in p.Home.Concat(p.Army).Concat(p.Hand).Concat(p.Discard).Concat(p.RemovedFromGame).Concat(p.Celestials ?? []).Concat(p.EventsInPlay ?? [])
                .Concat(p.Underneath ?? []).Concat(p.Provinces.Select(q => q.Card).OfType<CardView>()).Append(p.Stronghold))
                map.TryAdd(c.Id, c);
        foreach (var entry in view.ResolutionArea ?? []) map.TryAdd(entry.Card.Id, entry.Card); // issue #917: the held action's source sits in the resolution area, not in a hand
        return map;
    }

    private static GameCommand? Prepare(Ctx x, ActionOption action)
    {
        var self = x.Self; var opponent = x.Opponent; var view = x.View;
        var command = action.Command;
        if (command is null)
        {
            command = action.Type switch
            {
                "attack" or "assignDefense" => new(action.Type, self.Id, view.Version, Assignments: []),
                "pass" or "declineReaction" or "proclaim" or "endDynasty" or "declareAttack" => new(action.Type, self.Id, view.Version),
                "discardProvince" when action.CardId is not null => new(action.Type, self.Id, view.Version, CardId: action.CardId),
                "discardHand" => new(action.Type, self.Id, view.Version),
                "purchase" when action.Quote is { Error: null, Payments.Length: > 0 } q => new(action.Type, self.Id, view.Version, q.CardId, q.Payments[0].SourceIds, ClanDiscount: q.ClanDiscount, ClanAllegiance: q.ClanAllegiance),
                "selectBattle" when action.Provinces is { Length: > 0 } p => new(action.Type, self.Id, view.Version, Province: p[0]),
                _ => null
            };
        }
        if (command is null || command.PlayerId != view.ViewerId || command.ExpectedVersion != view.Version) return null;
        if (action.Type is "attack" or "assignDefense")
        {
            var legalIds = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
            var units = self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legalIds is null || legalIds.Contains(c.Id))).ToArray();
            var provinces = (action.Provinces ?? (action.Type == "attack" ? opponent.Provinces : self.Provinces).Where(p => !p.Destroyed).Select(p => p.Index).ToArray());
            if (provinces.Length == 0 || units.Length == 0) return null;
            if (action.Type == "attack" && x.Memory.RejectionMessages(view.Version, "attack").Any())
            {
                // The engine refused an assignment for a restriction only it checks (a unit that may not go to this battlefield). Try one unit at one province at a time, strongest
                // unit first, and offer the first pair not already refused.
                foreach (var unit in units.OrderByDescending(c => ArmyForce(self.Home.Where(h => h.Id == c.Id || h.AttachedTo == c.Id))).ThenBy(c => c.Id, StringComparer.Ordinal))
                    foreach (var province in provinces.OrderBy(p => opponent.Provinces.FirstOrDefault(q => q.Index == p)?.Strength ?? int.MaxValue).ThenBy(p => p))
                    {
                        var probe = command with { Assignments = new Dictionary<string, int> { [unit.Id] = province } };
                        if (!x.Memory.IsRejected(view.Version, probe)) return probe;
                    }
                return null;
            }
            if (action.Type == "attack")
            {
                // Mendon's spec (issue #642): as many sub-armies as can each beat their province plus every unit that could defend it.
                var assigned = AlreadyAssigned(x);
                var plan = PlanAttack(x, units, provinces, assigned) is { } whole ? HoldCavalry(x, OfferedOnly(whole, units)) : null;
                if (HonorAttackOf(x, units, provinces) is { } honorAttack) return command with { Assignments = HoldCavalry(x, honorAttack.Assignments) }; // issue #818 item 8
                if (plan is not null) return command with { Assignments = plan };
                if (TradeFor(x, units) is { } trade)
                {
                    var weakest = opponent.Provinces.Where(p => provinces.Contains(p.Index)).OrderBy(p => p.Strength).ThenBy(p => p.Index).First().Index;
                    return command with { Assignments = HoldCavalry(x, trade.UnitIds.ToDictionary(id => id, _ => weakest, StringComparer.Ordinal)) };
                }
                if (command.Assignments is { Count: > 0 }) return command with { Assignments = HoldCavalry(x, command.Assignments) }; // no plan clears its threshold: the engine's own assignment (all units at this province), scored low by the Attack rule
            }
            if (action.Type == "assignDefense" && (CavalryLastStandPlan(x, action, units) ?? CavalryDefensePlan(x, action, units)) is { } cavalryDefense) return command with { Assignments = cavalryDefense }; // issue #946
            // Mendon's rule (playtest 2026-10): when every province it has left will fall to the attack, the bot always defends, at the one battlefield it is likeliest to hold.
            var lastStand = action.Type == "assignDefense" ? LastStandProvince(x, provinces) : null;
            if (action.Type == "assignDefense" && x.Memory.UseInformedDefense && x.Memory.UseSplitDefense && lastStand is null && SplitDefense(x, units, provinces) is { } split) return command with { Assignments = split };
            var target = action.Type == "attack"
                ? opponent.Provinces.Where(p => provinces.Contains(p.Index)).OrderBy(p => p.Strength).ThenBy(p => p.Index).First().Index
                : lastStand ?? (x.Memory.UseInformedDefense ? InformedDefenseProvince(x, units, provinces) : null) ?? provinces.OrderByDescending(p => ArmyForce(opponent.Army.Where(c => c.ProvinceIndex == p))).ThenBy(p => p).First();
            return command with { Assignments = action.Type == "attack" ? HoldCavalry(x, units.ToDictionary(c => c.Id, _ => target)) : units.ToDictionary(c => c.Id, _ => target) };
        }
        if (action.Type == "discardHand")
        {
            var count = self.HandCount - x.MaxHand;
            if (count <= 0 || self.Hand.Length != self.HandCount) return null;
            return command with { CardIds = self.Hand.OrderBy(HandValue).ThenBy(c => c.Id, StringComparer.Ordinal).Take(count).Select(c => c.Id).ToArray() };
        }
        if (action.Type == "useAbility")
        {
            // Issue #788 (rule 6): with 22 or more Gold and most provinces holding holdings or unseen cards, the once-per-game refresh cycles exactly those.
            if (command.AbilityId == "border-game" && BorderRefreshWanted(x) && BorderRefreshCards(x, action.TargetIds ?? []) is { Length: > 0 } cycled)
            {
                x.Memory.NoteBorderRefresh(view.TurnNumber);
                return command with { CardIds = cycled };
            }
            if (command.AbilityId is "border-first" or "border-game")
            {
                var ready = Production(self.Stronghold, self.Clan) + self.Home.Sum(c => Production(c, self.Clan));
                var holdingCount = self.Home.Count(c => c.Definition?.Type == CardType.Holding);
                var selected = self.Provinces.Where(p => p.Card?.Definition is not null && (action.TargetIds ?? []).Contains(p.Card.Id))
                    .Where(p => p.Card!.Definition!.GoldCost > ready || self.TurnsTaken <= 2 && holdingCount < 3 && p.Card.Definition.Type == CardType.Personality || holdingCount >= 7 && p.Card.Definition.Type == CardType.Holding)
                    .Select(p => p.Card!.Id).ToArray();
                if (selected.Length == 0) return null;
                return command with { CardIds = selected };
            }
            // The engine marks a "select several cards" action by a CardIds array on its command (province cards, a set of Personalities): the answer is those cards, never a TargetId.
            if (command.CardIds is { Length: 0 } && action.TargetIds is { Length: > 0 } selectable)
            {
                var offered = selectable.Where(id => !id.StartsWith("hidden-", StringComparison.Ordinal)).ToArray();
                command = command with { CardIds = [(offered.Length > 0 ? offered : selectable)[0]] };
            }
            // Issue #902: the engine's default command already names the first legal target (found while tracing a bot that bowed a bowed Personality), so the bot chooses by what the ability does instead of keeping it.
            else if (command.CardIds is null && action.TargetIds is { Length: > 0 } && (command.TargetId is null || x.Memory.UsePrudence))
            {
                var intent = AbilityIntent.Of(command.AbilityId);
                var refused = x.Memory.RejectedTargets(view.Version, command.AbilityId, command.CardId, "useAbility");
                var legal = action.TargetIds.Where(id => !refused.Contains(id)).ToArray();
                // Issue #902: a bow is aimed at a card that is not already bowed; when every target is, the action is kept, not spent.
                if (x.Memory.UsePrudence) legal = legal.Where(id => !BowWasted(intent, x.Card(id))).ToArray();
                if (legal.Length == 0) return null;
                var known = legal.Select(id => x.Card(id)).OfType<CardView>().Select(c => (Card: c, Value: AbilityValue(command.AbilityId, c, self, opponent, view, x)))
                    .Where(p => p.Value > 0).OrderByDescending(p => p.Value).ThenBy(p => p.Card.Id, StringComparer.Ordinal).FirstOrDefault();
                // Issue #788: an Attack takes the highest-Force target it can destroy, and a bow takes the unit with the most attachments (or the most dangerous printed ability).
                var ruled = !(x.Memory.UseBattlePolicy || x.Memory.UseHonorAware && intent.IsAttack) ? null : intent.IsAttack ? PickAttackTarget(x, legal, x.Card(command.CardId))
                    : intent.Rank == EffectRank.Bow && intent.Side == TargetSide.Enemy ? PickBowTarget(x, legal, command.AbilityId) : null;
                var target = ruled ?? known.Card ?? PickCard(x, legal, intent, action.Label);
                if (target is not null && target.OwnerId == self.Id && BattlePolicy.SendsHome(command.AbilityId)) target = CostliestOwnUnit(x, legal, target); // Mendon 2026-10-18 (Block Supply Lines): save the unit worth the most printed Gold
                if (target is not null) command = WithTarget(command, action, target.Id);
                else if (command.TargetId is null) command = WithTarget(command, action, legal[0]);
                if (command is null) return null;
            }
        }
        if (action.Type is "equip" or "playStrategy" && action.TargetIds is { Length: > 0 } targets)
        {
            var refusedTargets = x.Memory.RejectedTargets(view.Version, command.AbilityId, command.CardId, action.Type);
            var targetCards = self.Home.Concat(self.Army).Concat(opponent.Army).Where(c => targets.Contains(c.Id) && !refusedTargets.Contains(c.Id)).ToArray();
            if (targetCards.Length == 0) return refusedTargets.Count > 0 ? null : Complete(x, action, command);
            // Issue #902: a Strategy that bows takes an unbowed target; with none, it is not played.
            if (x.Memory.UsePrudence && action.Type == "playStrategy" && StrategyIntent(x.Card(command.CardId)) is { Rank: EffectRank.Bow } bowing && bowing.Side != TargetSide.Own && targetCards.All(c => c.Bowed)) return null;
            var ordered = targetCards.OrderBy(c => c.Bowed).ThenByDescending(c => c.Force);
            var bowStrategy = x.Memory.UseBattlePolicy && action.Type == "playStrategy" && x.Card(command.CardId)?.Definition?.Effect == "bow-small" ? PickBowTarget(x, targetCards.Select(c => c.Id).ToArray(), null) : null;
            var withTarget = WithTarget(command, action, bowStrategy?.Id ?? ordered.ThenBy(c => c.Id, StringComparer.Ordinal).First().Id);
            return withTarget is null ? null : Complete(x, action, withTarget);
        }
        return Complete(x, action, command);
    }

    /// <summary>Retargets an offered command. When the cost depends on the target (issue #303), the engine's
    /// own quote for that target supplies the payment, so the bot never submits sources priced for a
    /// different target; a target it cannot pay for is not a candidate.</summary>
    private static GameCommand? WithTarget(GameCommand command, ActionOption action, string targetId)
    {
        command = command with { TargetId = targetId };
        if (action.TargetGold is not null && action.TargetGold.TryGetValue(targetId, out var quote))
            return quote.Payments.Length == 0 ? null : command with { SourceIds = quote.Payments[0].SourceIds };
        return command;
    }

    private static double Score(Ctx x, ActionOption action, GameCommand command) => Adjust(x, action, command, ScoreCore(x, action, command));

    private static double ScoreCore(Ctx x, ActionOption action, GameCommand command)
    {
        var view = x.View; var self = x.Self; var opponent = x.Opponent;
        var card = self.Provinces.Select(p => p.Card).Concat(self.Hand).FirstOrDefault(c => c?.Id == command.CardId);
        switch (action.Type)
        {
            // A conservative practice choice, not an automatic choice for the human.
            case "harshChoices": return command.Choice == "decline" ? 150 : 20;
            case "timesOfStrifeDiscard":
                var eventDiscard = self.Hand.FirstOrDefault(c => c.Id == command.TargetId);
                return eventDiscard is null ? -1000 : 150 - HandValue(eventDiscard);
            case "brashDraw":
            case "overconfidentDraw": return command.Choice == "draw" ? 150 : 100;
            case "chooseShinden":
                // An opponent already at 40 Honor has won and only waits to begin their turn: one more Honor changes nothing, so a bow would be a cost for no gain (Mendon, 2026-10-24).
                if (x.Opponent.Honor >= 40) return command.Choice == "honor" ? 100 : -100;
                // Never give the opponent Honor (Mendon's rule, issue #642): bowing is always the answer; allowing the Honor gain is only chosen if bowing were not offered.
                return Spec(x) ? ShindenScore(x, command) : command.Choice == "honor" ? -100 : 100; // issue #818 item 10
            case "useAbility":
                // A Reaction it announced and ordered is played: skipping it is never better than the announcement it made (issue #803).
                return AbilityScore(x, action, command) + (view.ReactionOrder is { Stage: "Resolve" } ? 1000 : 0);
            // Announce-and-order of a Reaction window (issue #803): announce what it would play anyway, then say it is done; order its own Reactions first and the other player's as announced.
            case "reactionAnnounce": return AbilityScore(x, action with { Type = "useAbility" }, command with { Type = "useAbility" });
            case "reactionAnnounceDone": return 100.001; // just above declineReaction: announced only what it would play instead of declining
            case "reactionUnannounce": return -1000;
            case "reactionOrder":
                var placed = view.ReactionOrder?.Announced.FirstOrDefault(i => i.Id == command.TargetId);
                var rank = int.TryParse(command.TargetId?.TrimStart('r'), out var number) ? number : 0;
                return (placed?.PlayerId == self.Id ? 1000 : 500) - rank;
            case "proclaim": return ProclaimActionScore(x);
            case "declineReaction": return 100;
            case "discardHand": return 100;
            case "selectBattle":
                return 100 + ArmyForce(self.Army.Where(c => c.ProvinceIndex == command.Province));
            case "purchase":
                var def = card?.Definition;
                if (def is null) return -100;
                var holdings = self.Home.Count(c => c.Definition?.Type == CardType.Holding);
                if (def.Type == CardType.Holding)
                    return HoldingPurchaseScore(x, holdings); // issue #818 item 1
                double baseScore = 110 + def.Force * 2 + def.PersonalHonor * 2 + (x.Memory.BorderRefreshedRecently(view.TurnNumber) ? 30 : 0); // issue #788: recruit Personalities from a refresh
                if (def.Type == CardType.Personality) baseScore += CatchUpRecruitBonus(x); // issue #818 item 2
                var cost = action.Quote?.Cost ?? def.GoldCost;
                var ready = Production(self.Stronghold, self.Clan) + self.Home.Sum(c => Production(c, self.Clan));
                var stance = x.Profile.StanceFor(self.Honor, opponent.Honor, ready - cost >= 2);
                // Proclaim and the Clan discount are the two ways a Clan Personality can pay off: which one the bot wants follows its Honor profile (issue #642).
                if (action.Quote?.ProclaimEligible == true) baseScore += stance switch { ProclaimStance.Prioritize => 40, ProclaimStance.Neutral => 20, _ => -10 };
                if (command.ClanAllegiance && self.Honor >= def.HonorRequirement) baseScore -= 20;
                if (command.ClanDiscount && !self.ProclaimUsedThisTurn) baseScore += stance switch { ProclaimStance.Prioritize => -25, ProclaimStance.Neutral => -15, _ => 15 };
                return baseScore - cost;
            case "overlay":
                return OverlayScore(x, card); // issue #818 item 4
            case "discardProvince": return DiscardProvinceScore(x, card); // issue #818 items 1 and 3
            case "endDynasty": return 0;
            case "equip":
                var totalProduction = Production(self.Stronghold, self.Clan) + self.Home.Sum(c => Production(c, self.Clan));
                return Math.Max(PassScore, totalProduction >= 6 ? 60 : 5);
            case "playStrategy":
                var target = self.Army.Concat(opponent.Army).FirstOrDefault(c => c.Id == command.TargetId);
                if (card?.Definition?.Effect == "bow-small") return Math.Max(PassScore, target is { Bowed: false } ? 90 + target.Force : 0);
                if (target?.Bowed == true) return PassScore;
                return 70;
            // The Declaration Segment: declare when the whole unbowed home army would threaten the weakest province, as the old
            // per-maneuver attack score did, otherwise decline (pass scores 10).
            case "declareAttack":
                return DeclareScore(x);
            case "attack":
                if (command.Assignments is not { Count: > 0 }) return -100;
                var assignments = WholeAttack(x, action, command.Assignments);
                if (Spec(x)) // issue #818 item 8: the unit sent only to gain Honor is not part of the attack that must destroy a Province
                {
                    var (honorUnits, honorProvinces) = AttackInputs(x, action);
                    if (HonorAttackOf(x, honorUnits, honorProvinces) is { } honorPlan && honorPlan.Assignments.Count == assignments.Count && honorPlan.Assignments.All(p => assignments.TryGetValue(p.Key, out var sent) && sent == p.Value))
                    {
                        assignments = assignments.Where(p => p.Key != honorPlan.UnitId).ToDictionary(p => p.Key, p => p.Value);
                        if (assignments.Count == 0) return 85;
                    }
                }
                var attacking = self.Home.Concat(x.Memory.UseCavalryAttack ? self.Army : []).Where(c => assignments.ContainsKey(c.Id) || c.AttachedTo is not null && assignments.ContainsKey(c.AttachedTo));
                var force = ArmyForce(attacking);
                var homeDefense = ArmyForce(opponent.Home);
                // Every army in the assignment must beat its own province plus every unit that could defend it.
                var armies = assignments.GroupBy(p => p.Value).Select(g => (Province: g.Key, Force: ArmyForce(self.Home.Concat(x.Memory.UseCavalryAttack ? self.Army : []).Where(c => g.Any(p => p.Key == c.Id) || c.AttachedTo is not null && g.Any(p => p.Key == c.AttachedTo))))).ToArray();
                if (HonorRace(x) && armies.Length > 0) return 150 + armies.Length; // the opponent wins on Honor at the start of its turn: destroy every Province the army can
                var weakestArmyMargin = armies.Length == 0 ? int.MinValue : WeakestArmyMargin(x, armies.Select(a => (a.Province, a.Force)).ToArray(), homeDefense);
                if (weakestArmyMargin > 0) return 90 + force - homeDefense;
                if (armies.Length == 1 && armies[0].Force > (opponent.Provinces.FirstOrDefault(p => p.Index == armies[0].Province)?.Strength ?? int.MaxValue / 2) && homeDefense == 0) return 80;
                return armies.Length == 1 && TradeOf(x, assignments.Keys) is { } traded ? TradeScore(traded) : 0;
            case "assignDefense":
                if (CavalryLastStandPlan(x, action, null) is not null) return 200; // the Cavalry, with any army already assigned, could destroy every Province: defend with everything
                if (x.Memory.UseInformedDefense && InformedDefenseScore(x, command) is { } informed) return informed;
                var enemyForce = ArmyForce(opponent.Army);
                var ownForce = ArmyForce(self.Home);
                if (enemyForce == 0) return CavalryLastStandPlan(x, action, null) is not null ? 200 : CavalryDefensePlan(x, action, null) is not null ? 95 : 0; // the last Province would fall to the Cavalry: defend with everything // issue #946: nobody is assigned yet, but Cavalry is on its way
                if (LastStandProvince(x, self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray()) is not null) return 200; // the last Provinces will fall: defend and fight
                return ownForce >= enemyForce ? 95 : ownForce + StrongestThreatProvinceStrength(self, opponent) >= enemyForce ? 60 : self.Provinces.Count(p => !p.Destroyed) == 1 ? 50 : 0;
            case "orderSeppuku": return x.Memory.UseSeppukuPolicy ? SeppukuScore(x, command) : PassScore + 1;
            case "pass": return PassScore;
            // A convenience for a human who wants to skip every remaining battle in one go; the bot simply passes when its turn comes (and it is refused while a window is open).
            case "passAllBattles": return -100;
            // Deck search (issue #312): a modest bot never stalls in a search. It takes the first eligible card the engine
            // lists (the list is already sorted by type then name, and holds only eligible entries), passes when the
            // engine offers Pass (which it does only when nothing qualifies), attaches a found Region to the first
            // offered province, and discards its least useful hand card after a Terrain. Ties keep the engine's order.
            case "chooseSearchDeck": case "selectSearchCard": case "passSearch": case "chooseSearchProvince": return 100;
            case "selectSearchCards": case "chooseSearchAttachTarget": return 100; // P20 (SearchExtensions.cs): the engine's default legal selection; the first Personality it can be attached to
            // "Put it into play or your hand" (issue #498): a modest bot puts the found Ring into play; the Attack target selection step: any offered target over none.
            case "chooseSearchDestination": return command.Choice == "play" ? 100 : 90;
            case "chooseAttackTarget": return AttackTargetChoiceScore(x, command);
            // Goju Asuka / Only a Shadow (issue #501): the Personality kept bowed is the engine's first listed candidate (every answer is legal and one is owed).
            case "chooseInfiltratorStraighten": return 100;
            // Look at the top N cards (issue #322): a modest bot answers a look with the first arrangement the engine offers -- the
            // simplest legal one (keep everything in order when that is legal, otherwise take the top card that may be taken) -- and
            // picks the first offered deck.
            case "chooseLookDeck": case "arrangeTopCards": return 100;
            case "chooseSearchDiscard":
                var discard = self.Hand.FirstOrDefault(c => c.Id == command.TargetId);
                return discard is null ? 0 : 100 - HandValue(discard);
            // The opposing player's Either/Or choice (issue #338): pay when the engine offers a payable Gold option (a modest bot keeps its action), then bow the
            // threatened Personality, then lose Honor, and only then take the consequence. Copying a keyword or ability
            // (Fukuzo): the first candidate the engine lists.
            case "chooseEitherOr": return command.Choice switch { "pay" => 110, "bow" => Spec(x) && !BowAcceptable(x, x.Card(view.PendingEitherOr?.AffectedCardId)) ? 59 : 90, "honor" => -100, /* "Lose 2 Honor": the bot never loses Honor by choice */ "done" => 61, /* P25: a multi-pick Either/Or takes the fewest options it must (Done is offered only once Min are picked) */ _ => 60 };
            case "chooseCopyKeyword": case "chooseCopyAbility": return 100;
            // The opposing player's variable payment (issue #373, Virtuous Charity): a modest bot answers with the engine's default, paying nothing.
            case "chooseOpponentPayment": return 100;
            // A forced discard (Times of Strife, Daigotsu Bofana; issue #333): the same modest rule as the discard after a Terrain -- give up
            // the least useful card in hand.
            case "forcedDiscard":
                var forced = self.Hand.FirstOrDefault(c => c.Id == command.TargetId);
                return forced is null ? 0 : 100 - HandValue(forced);
            // Battle Phase timing (issue #310): the before-this-battle-resolves choices. Any legal answer is acceptable to a
            // modest practice opponent: keep its strongest Personality, bow the strongest enemy one, and take effects in offered order.
            case "encircledSelect":
                var kept = self.Army.FirstOrDefault(c => c.Id == command.TargetId);
                return 100 + (kept?.Force ?? 0);
            case "encircledOrder": case "beforeBattleOrder": return 50;
            // The Killing Grounds (Emperor Edition remaining-cards pass, issue #492, group D): destroy the weakest of its own Personalities at the battlefield.
            case "killingGroundsSelect":
                var doomed = self.Army.FirstOrDefault(c => c.Id == command.TargetId);
                return 100 - (doomed?.Force ?? 0);
            // A conditional effect choice (issue #337, Mirumoto Yumaru; issue #642): "choose a player who gains or loses N Honor" is answered from the bot's Honor profile
            // (high deck gains Honor, low deck takes it from the opponent, in between depends on the race); any other effect choice takes the first offered option.
            case "chooseEffect": return EffectChoiceScore(x, command);
            case "beforeBattleOptional":
                var optionalTarget = opponent.Army.FirstOrDefault(c => c.Id == command.TargetId);
                return command.Choice == "use" ? 60 + (optionalTarget?.Force ?? 0) : 20;
            // Sleep's budgeted multi-select (issue #667): the bot bows enemy unbowed Personalities the budget allows (strongest first), never its own or an already bowed one, never clears, then confirms.
            case "budgetSelectAdd":
                if (x.View.PendingChoice?.Type == Wave829Abilities.BentenType) return Wave829BentenScore(x, action); // Benten's Blessing (issue #829) shares the three actions
                var sleepTarget = opponent.Army.Concat(opponent.Home).FirstOrDefault(c => c.Id == command.TargetId);
                return sleepTarget is { Bowed: false } ? 120 + sleepTarget.Force : -50;
            case "budgetSelectConfirm": return 100;
            case "budgetSelectClear": return -100;
            // Kokujin's question after a won duel (issue #827): give up one of its own Personalities only for a loser worth clearly more than it; P'an Ku stays bowed to keep the Personality he took; the Spell the Jingasa makes it drop is the first listed.
            case "w827KokujinChoose": return Wave827KokujinScore(x, command);
            case "w827PanKuStraightenChoose": return command.Choice == "remain" ? 105 : 100;
            case "w827KoanDiscardChoose": return 100;
            // Cross-Clan Wedding (issue #829): the Personality it gives away is the least valuable it has, whether it is the first gift or the one given back; Minor Blackmail names the other player's strongest.
            // Blood of Isawa's Tribe (issue #835): the bot takes a keyword to replace, always replaces it with a different element (the same one only asks for a confirmation), and never backs out.
            case Wave835Abilities.BloodFrom: return 100;
            case Wave835Abilities.BloodTo: return action.Label.Contains("the same keyword", StringComparison.Ordinal) ? 40 : 100;
            case Wave835Abilities.BloodConfirm: return 100;
            case Wave835Abilities.BloodCancel: case Wave835Abilities.BloodBack: return 10;
            // The Heavens' Blessing (issue #835): the bot declines to pay 2 Gold for a card it would only draw, and a Foe duel names its first Unique Personality.
            case Wave835Abilities.HeavensDeclineAll: return PassScore + 2;
            case Wave835Abilities.FoePick: return 100 + (x.Card(command.TargetId)?.Force ?? 0);
            case "w829WeddingGiveChoose": return 100 - Wave829Worth(x.Card(command.TargetId));
            case "eventChoice": return Wave829EventChoiceScore(x, action, command);
            // Fusako's pre-accept bow (issue #667): the bot never bows its own Personality to refuse; it accepts the duel.
            case "bowToRefuseDuel": return -100;
            // Any other decision the engine offers is answered rather than ignored (issue #642): a modest bot takes the first complete option, just above passing,
            // so a decision it has no rule for can never stall a match.
            default: return PassScore + 1;
        }
    }

    private static double AbilityValue(string? ability, CardView target, PlayerView self, PlayerView opponent, MatchView view, Ctx? x = null)
    {
        var own = target.OwnerId == self.Id;
        return ability switch
        {
            "shinden-choice" => !own && !target.Bowed ? 70 + target.Force : -20,
            "air-straighten" or "paid-straighten" => own && target.Bowed && !target.StraightenBlocked ? 75 + target.Force : -20,
            "paid-bow" => !own && !target.Bowed ? 65 + target.Force : -20,
            "prepared-death" => own && !target.Bowed && self.Id == view.ActivePlayerId ? 35 + target.Force : -20,
            "kouki-force" or "costly-feud" => !own && !target.Bowed && target.Force > 0 ? 75 + target.Force : -20,
            "defensive-stance" => own && target.Bowed && !target.StraightenBlocked ? 95 + target.Force : -20,
            "crushing-force" => own && !target.Bowed ? 70 + target.Force : -20,
            "crushing-destroy" or "never-afraid" => !own ? 90 + target.Force : -20,
            "favor-battle" when x is not null && Spec(x) => !own ? 300 + UnitForceOf(x, target) : -20, // issue #818 item 5: defending, always send home the largest unit
            "incapacitated" or "block-supply" or "favor-battle" => !own ? 100 + target.Force : -20,
            // Issue #818 item 5: lobbying bows the lowest-Force Personality, then the one with the fewest attachments, then the fewest printed abilities.
            "lobby" => view.FavorPlayerId == self.Id ? -20 : 60 - target.Force - (target.Definition?.Abilities?.Contains("shunya-honor") == true ? 25 : 0)
                - (x is not null && Spec(x) ? 0.01 * x.Cards.Values.Count(a => a.AttachedTo == target.Id) + 0.0001 * (target.Definition?.Abilities?.Length ?? 0) : 0),
            "favor-cycle" => self.HandCount >= 6 && !self.Home.Any(c => !c.Bowed && c.Definition?.Abilities?.Contains("shunya-honor") == true) ? 25 - HandValue(target) : -20,
            _ => -20
        };
    }

    private static int StrongestThreatProvinceStrength(PlayerView self, PlayerView enemy)
    {
        var target = self.Provinces.Where(p => !p.Destroyed).OrderByDescending(p => ArmyForce(enemy.Army.Where(c => c.ProvinceIndex == p.Index))).FirstOrDefault();
        return target?.Strength ?? 0;
    }
    private static int ArmyForce(IEnumerable<CardView> cards) => cards.Where(c => !c.Bowed && c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c => Math.Max(0, c.Force));
    private static int Production(CardView card, string clan) => card.Bowed ? 0 : (card.Definition?.GoldProduction ?? 0) + ClanGoldEffects.Bonus(card.Definition?.Effect, clan); // the Clan Holdings' Effects: ClanGold.cs (shared with the engine)
    private static int HandValue(CardView card) => card.Definition?.Effect switch { "force-three" => 8, "bow-small" => 6, _ => card.Definition?.Type == CardType.Follower ? 5 : 4 };
    private static string Explain(string type) => type switch
    {
        "harshChoices" => "Preserve the province instead of exchanging it for cards.",
        "timesOfStrifeDiscard" => "Resolve the mandatory discard with a lower-value card from my own hand.",
        "overconfidentDraw" => "Accept the offered optional draw.",
        "purchase" => "Develop Gold production early, then recruit Personalities and preserve a useful Proclaim opportunity.",
        "overlay" => "Recruit the higher-Experience Personality for free while preserving the existing unit.",
        "proclaim" => "Take the available once-per-turn Honor gain after the Personality enters play.",
        "declareAttack" => "Declare an attack when visible Force can threaten a province.",
        "attack" => "Commit the offered unbowed army when visible Force can threaten a province.",
        "assignDefense" => "Defend the battlefield with the largest visible attacking army.",
        "playStrategy" => "Use a legal Battle action to improve the visible Force comparison.",
        "equip" => "Attach support to an available Personality using the offered payment.",
        "discardProvince" => "Cycle remaining face-up cards after purchasing is complete.",
        "discardHand" => "Keep more useful combat cards while returning to the hand-size maximum.",
        "useAbility" => "Use a card ability the engine offers.",
        _ => "Continue the current legal action round."
    };
}
