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
        CheckDeadline(memory);
        memory.Observe(view);
        if (memory.UsePlannerTriage) memory.NoteBattleActions(view); // issue #1185: what each performer has already used this turn
        if (memory.Has("read")) memory.NoteRead(view); // issue #1206: the Read in shadow mode (docs/bots/CONAN_STRATEGY_BRIEF.md section 4); it drives nothing
        var self = view.Players.Single(p => p.Id == view.ViewerId);
        var opponent = view.Players.Single(p => p.Id != view.ViewerId);
        var x = new Ctx(view, self, opponent, BotProfile.Assess(self, memory.DeckAveragePersonalHonor), memory, AllCards(view)) { Actions = actions, Odds = BattlePolicy.Assess(view) };
        memory.Consulted = memory.CountFires ? new HashSet<string>(StringComparer.Ordinal) : null; // issue #1206 (fire counters): the rules this decision consults
        if (Decide(x) is not { } decided) { memory.Consulted = null; return new(null, "No complete legal action was supplied for this decision."); }
        if (memory.CountFires) CountFires(x, memory, decided.Chosen.Command);
        var candidates = decided.Candidates; var chosen = decided.Chosen; x = decided.X;
        Remember(x, chosen.Command);
        if (chosen.Command.Type is "useAbility" or "playStrategy" or "equip") memory.NoteUse(view.TurnNumber, chosen.Command.AbilityId, chosen.Command.CardId);
        // issue #1206 (family A in shadow): with read on and readA off, the opportunity's attack or hold and its shape beside what was chosen
        if (memory.Has("read") && !memory.Has("readA") && view.ActivePlayerId == view.ViewerId && (view.Phase == Phase.AttackDeclaration || view.Phase == Phase.InfantryDeclaration && chosen.Command.Type == "attack") && ReadAnswers(x) is { } shadowA)
            memory.NoteAttack(view, shadowA, chosen.Command);
        // issue #1206 (family E in shadow): with read on and readE off, what the Fight would do at this Battle priority beside what was chosen
        if (memory.Has("read") && !memory.Has("readE") && ReadFight(x) is { } shadowE)
            memory.NoteFight(view, shadowE.Plan, FightKey(chosen.Command) is { } ck && shadowE.Options.FirstOrDefault(o => SameFightAction(o.Key, ck)) is { } same ? same.Key : chosen.Command.Type is "pass" or "passAllBattles" ? "pass" : "other:" + chosen.Command.Type + "/" + chosen.Command.AbilityId, shadowE.Position);
        // issue #1206 (family D in shadow): with read on and readD off, what the Read's defence would have assigned beside what was chosen
        if (memory.Has("read") && !memory.Has("readD") && view.Phase == Phase.DefenseAssignment && chosen.Command.Type is "assignDefense" or "pass" && candidates.FirstOrDefault(c => c.Action.Type == "assignDefense").Action is { } offer)
            memory.NoteDefence(view, ReadDefence(x, offer), chosen.Command);
        return new(chosen.Command, Explain(chosen.Action.Type));
    }

    /// <summary>The decision itself: the turn plan, every offered action prepared and scored, the best first, then the lookahead's say. Null when no action is complete and legal.</summary>
    private static (Ctx X, (ActionOption Action, GameCommand Command, double Score)[] Candidates, (ActionOption Action, GameCommand Command, double Score) Chosen)? Decide(Ctx x)
    {
        var memory = x.Memory; var view = x.View;
        if (memory.UsePlanner) x = x with { Plan = PlanFor(x) }; // issue #1185: the turn plan, rebuilt when the table changes
        var candidates = x.Actions.Where(a => a.Enabled && a.Type is not ("concede" or "wait"))
            .Select(a => (Action: a, Command: Prepare(x, a)))
            .Where(p => p.Command is not null && !memory.IsRejected(view.Version, p.Command))
            .Select(p => (p.Action, Command: p.Command!, Score: Score(x, p.Action, p.Command!)))
            .OrderByDescending(p => p.Score).ThenBy(p => p.Action.Type, StringComparer.Ordinal)
            .ThenBy(p => p.Command.CardId, StringComparer.Ordinal).ThenBy(p => p.Command.AbilityId, StringComparer.Ordinal).ToArray();
        if (candidates.Length == 0) return null;
        var chosen = candidates[0];
        if (LookAhead(x, candidates) is { } looked) chosen = (looked.Action, looked.Command, chosen.Score);
        return (x, candidates, chosen);
    }

    /// <summary>Issue #1206 (strategy brief step 1 item 1; the arena's L5R_TOURN_FIRES=1): the decision is made again once per rule it consulted, with that rule alone masked; a rule fires
    /// when the choice differs (its knob token fires when the default would have changed the choice). What a re-decision touches in memory (the plan, the planned attack, the Read's caches, the
    /// logs) is put back, so the real decision's state stands. The playout bots inside a search keep their full rule set; the game's deadline cuts the counting, never the game.</summary>
    private static void CountFires(Ctx x, BotContext memory, GameCommand chosen)
    {
        var consulted = memory.Consulted!.OrderBy(r => r, StringComparer.Ordinal).ToArray(); memory.Consulted = null;
        memory.CountedDecisions++;
        foreach (var rule in consulted) memory.Active[rule] = memory.Active.GetValueOrDefault(rule) + 1;
        var snapshot = memory.Save(); var want = System.Text.Json.JsonSerializer.Serialize(chosen);
        foreach (var rule in consulted)
        {
            if (memory.Deadline is { } cutoff && DateTime.UtcNow >= cutoff) { memory.FiresCut++; break; }
            memory.Masked = rule; memory.Restore(snapshot); memory.ForgetPlan(); memory.ReadAnswersCache = null; memory.ReadDefencePlan = null; memory.ReadFightPlan = null;
            try
            {
                if (Decide(x) is not { } again || System.Text.Json.JsonSerializer.Serialize(again.Chosen.Command) != want) memory.Fires[rule] = memory.Fires.GetValueOrDefault(rule) + 1;
            }
            catch (BotDeadlineException) { memory.FiresCut++; break; }
            finally { memory.Masked = null; }
        }
        memory.Restore(snapshot);
    }

    /// <summary>Issue #1206 (family E): the Fight's plan at the viewer's own Battle priority (no pending choice): the offered Battle actions it can read (an ability or Strategy with a Battle
    /// descriptor, aimed at an enemy Personality here or adding Force), computed once per view version. Null outside such a priority, or when nothing offered can be read.</summary>
    private static (FightPlan Plan, FightOption[] Options, string Position)? ReadFight(Ctx x)
    {
        var view = x.View;
        if (view.Phase != Phase.Battle || view.PendingChoice is not null || x.Odds is not { } odds || view.BattleStep is not { } step || step.DecisionPlayerId != x.Self.Id || step.BattlefieldProvince is not { } province) return null;
        if (x.Memory.ReadFightPlan is { } cached && cached.Version == view.Version) return (cached.Plan, FightOptions(x, province), FightPosition(x, odds));
        var read = x.Memory.CurrentRead is { } current && current.Key == Read.KeyOf(view) ? current : Read.Of(view);
        var options = FightOptions(x, province);
        if (options.Length == 0) return null;
        var viewerAttacks = odds.IsAttacker;
        var deciding = viewerAttacks ? x.Opponent.Provinces.Count(p => !p.Destroyed) <= 1 : x.Self.Provinces.Count(p => !p.Destroyed) <= 1;
        var plan = Fight.Of(read, province, viewerAttacks, step.ConsecutivePasses > 0, deciding, options, Rule(x, "reOpen"));
        x.Memory.ReadFightPlan = (view.Version, plan);
        return (plan, options, FightPosition(x, odds));
    }

    /// <summary>For the decision set (ConanPositions, L5R_POS_READ=1): the Fight's plan at this view with these offered actions, with every option's projection; null when the Fight does not act here.</summary>
    public static string? DescribeFight(MatchView view, ActionOption[] actions, BotContext memory)
    {
        var self = view.Players.Single(p => p.Id == view.ViewerId); var opponent = view.Players.Single(p => p.Id != view.ViewerId);
        var x = new Ctx(view, self, opponent, BotProfile.Assess(self, memory.DeckAveragePersonalHonor), memory, AllCards(view)) { Actions = actions, Odds = BattlePolicy.Assess(view) };
        if (ReadFight(x) is not { } fight) return null;
        return $"fight: {fight.Plan.Pick ?? "pass"} ({fight.Plan.Why}) | {fight.Position} | {fight.Plan.Standing}; {fight.Plan.Reach}\n    " + string.Join("\n    ", fight.Plan.Log)
            + (actions.Where(a => a.Enabled && a.Type is "useAbility" or "playStrategy" && a.Command is not null && !fight.Options.Any(o => o.Key == FightKey(a.Command))).Select(a => $"{a.Type}/{a.Command!.AbilityId} card={a.Command.CardId} target={a.Command.TargetId}").ToArray() is { Length: > 0 } unread ? "\n    not read: " + string.Join("; ", unread) : "");
    }

    private static string FightPosition(Ctx x, BattlePolicy.Odds odds) => $"{(odds.IsAttacker ? "attacking" : "defending")} {odds.Own}v{odds.Enemy} s{odds.Strength} hand {x.Self.Hand.Count()}/{x.Opponent.HandCount} gold {Ready(x)}";

    /// <summary>The offered Battle actions as Fight options. An action without a Battle descriptor, or aimed at something other than an enemy Personality at this battlefield, is not read (it keeps the Contender's score).</summary>
    private static FightOption[] FightOptions(Ctx x, int province)
    {
        var list = new List<FightOption>();
        foreach (var a in x.Actions.Where(a => a.Enabled && a.Type is "useAbility" or "playStrategy" && a.Command is not null))
        {
            var c = a.Command!;
            var card = x.Card(c.CardId);
            var d = a.Type == "useAbility" ? (c.AbilityId is { } id ? EffectDescriptors.Of(id) : null) : card is null ? null : EffectDescriptors.Of(card).FirstOrDefault(e => e.IsBattle);
            if (d is null || !d.IsBattle || d.IsReaction || d.Class == EffectClass.Unknown) continue;
            var fromHand = card is not null && x.Self.Hand.Any(h => h.Id == card.Id);
            var performer = x.Card(c.PerformerId) ?? (fromHand ? null : card);
            var source = c.AbilityId == "favor-battle" ? "Favor" : performer?.Definition?.Name ?? card?.Definition?.Name ?? c.CardId ?? c.AbilityId ?? "?";
            // the Fight chooses the target itself (PM 3.5 "target", "discard for Focus the card ... whose value suffices"): one option per legal target the engine lists, the command's own when it has one
            var targets = (a.TargetIds ?? []).Concat(c.TargetId is { } chosen ? [chosen] : []).Distinct(StringComparer.Ordinal).ToArray(); // every target the engine lists (the template's own may not be the completer's)
            if (d.Neutralises)
            {
                foreach (var tid in targets)
                {
                    var t = x.Card(tid);
                    if (t is null || t.OwnerId == x.Self.Id || t.Definition?.Type != CardType.Personality || t.ProvinceIndex != province || t.Zone != Zone.Battlefield) continue;
                    list.Add(new(FightKey(c with { TargetId = tid })!, new Answer(d, source, fromHand, a.Gold?.Cost ?? d.GoldCost), tid));
                }
            }
            else if (d.Class == EffectClass.ForceAdd && (d.MagnitudeVaries || d.Magnitude is null) && targets.Any(tid => x.Self.Hand.Any(h => h.Id == tid)))
            {
                // a Force add worth the discarded card's Focus (Tactical Advantage, "discard a card: +Force = its Focus Value"): one option per card, at that card's value
                foreach (var tid in targets.Where(tid => x.Self.Hand.Any(h => h.Id == tid)))
                    if (x.Card(tid) is { Definition: { } discarded }) list.Add(new(FightKey(c with { TargetId = tid })!, new Answer(d with { Magnitude = discarded.Focus, MagnitudeVaries = false }, source, fromHand, a.Gold?.Cost ?? d.GoldCost), null));
            }
            else if (d.Class == EffectClass.ForceAdd) list.Add(new(FightKey(c)!, new Answer(d, source, fromHand, a.Gold?.Cost ?? d.GoldCost), null));
        }
        return list.ToArray();
    }

    private static string? FightKey(GameCommand c) => c.Type is "useAbility" or "playStrategy" ? $"{c.Type}|{c.AbilityId}|{c.CardId}|{c.PerformerId}|{c.TargetId}" : null;

    /// <summary>The same Battle action: equal keys, or keys that differ only in the card or performer slot where one side has none (the engine's template leaves them to the completer).</summary>
    internal static bool SameFightAction(string? a, string? b)
    {
        if (a is null || b is null) return false;
        if (a == b) return true;
        var pa = a.Split('|'); var pb = b.Split('|');
        if (pa.Length != 5 || pb.Length != 5) return false;
        for (var i = 0; i < 5; i++) if (pa[i] != pb[i] && !(i is 2 or 3 && (pa[i].Length == 0 || pb[i].Length == 0))) return false; // the card and performer slots: the template may leave them to the completer
        return true;
    }

    /// <summary>Issue #1206 (family A): the Read's three answers for this decision, computed once per view version (BotContext.CurrentAnswers holds the shadow's own, taken at the same moments).</summary>
    private static Answers? ReadAnswers(Ctx x)
    {
        if (x.View.ActivePlayerId != x.Self.Id) return null;
        if (x.Memory.ReadAnswersCache is { } cached && cached.Version == x.View.Version) return cached.Answers;
        var read = x.Memory.CurrentRead is { } current && current.Key == Read.KeyOf(x.View) ? current : Read.Of(x.View);
        var answers = Questions.Of(read, x.Memory.TrendNow());
        x.Memory.ReadAnswersCache = (x.View.Version, answers);
        return answers;
    }

    /// <summary>Issue #1206 (family D): the Read's defence plan for this decision, computed once per view version. Their last Province is conceded when the opportunity razes it next turn.</summary>
    private static DefencePlan? ReadDefence(Ctx x, ActionOption action)
    {
        if (x.Memory.ReadDefencePlan is { } cached && cached.Version == x.View.Version) return cached.Plan;
        var read = x.Memory.CurrentRead is { } current && current.Key == Read.KeyOf(x.View) ? current : Read.Of(x.View);
        var provinces = action.Provinces ?? x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        var concede = x.Opponent.Provinces.Count(p => !p.Destroyed) == 1 && Questions.Of(read, x.Memory.TrendNow()).Opportunity.Best.ExpectedProvinces >= .5 * Questions.ProvinceWorth(1);
        var plan = Defence.Of(read, action.TargetIds, provinces, concede, concedeSpare: !Rule(x, "rdDefend"));
        x.Memory.ReadDefencePlan = (x.View.Version, plan);
        return plan;
    }

    /// <summary>Everything one decision reads, built once from the permitted view.</summary>
    private sealed record Ctx(MatchView View, PlayerView Self, PlayerView Opponent, BotProfile Profile, BotContext Memory, Dictionary<string, CardView> Cards)
    {
        /// <summary>Every action offered at this decision (issue #788: a rule may need to know whether a better one exists).</summary>
        public ActionOption[] Actions { get; init; } = [];
        /// <summary>The Force picture of the battle in progress, or null outside one.</summary>
        public BattlePolicy.Odds? Odds { get; init; }
        /// <summary>Issue #1185: the turn plan, null unless <see cref="BotContext.UsePlanner"/>.</summary>
        public TurnPlan? Plan { get; init; }
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
        var command = PrepareCore(x, action);
        // Issue #1206 (readD, RULE_FAMILIES.md family D): the defence assignment read from the Read; nobody is carried out by scoring the assignment below a pass (ScoreCore).
        if (command is not null && action.Type == "assignDefense" && Rule(x, "readD") && x.View.Phase == Phase.DefenseAssignment && ReadDefence(x, action) is { } readPlan && readPlan.Why != "nobody attacks") // no infantry yet: the Cavalry plans keep the decision
            return readPlan.Nobody ? (x.Actions.Any(a => a.Enabled && a.Type == "pass") ? null : command) : command with { Assignments = readPlan.Assignments }; // nobody: no assignment is offered, the pass wins
        // Issue #1185 tuning (presence): a defense the ordinary rules would not make, when one unit's presence lets the bot's removals save the Province.
        if (command is not null && action.Type == "assignDefense" && Rule(x, "presence") && PresencePlan(x, action) is { } presence && ScoreCore(x, action, command) < 95)
            return command with { Assignments = presence };
        // Issue #1185 tuning (deter): spare attackers stay home when they are enough to stop the opponent's whole army attacking next turn.
        if (command is { Assignments.Count: > 0 } && action.Type == "attack" && x.View.Phase == Phase.InfantryDeclaration && Rule(x, "deter") && DeterPlan(x, command.Assignments) is { } deter)
            return command with { Assignments = deter };
        // guard (block 7800's losses): units that must stay home to stop the counter-attack razing a last Province are taken out of the attack; what is left goes only where it still
        // beats the Province and every unbowed defender, else nobody goes.
        if (command is { Assignments.Count: > 0 } && action.Type == "attack" && Rule(x, "guard")
            && Guard(x, x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray(), x.Self.Army.Where(c => c.Definition?.Type == CardType.Personality).ToArray()) is { Length: > 0 } guard)
        {
            var home = guard.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
            var rest = command.Assignments.Where(a => !home.Contains(a.Key)).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal);
            var defenders = ArmyForce(x.Opponent.Home);
            bool Beats(IGrouping<int, KeyValuePair<string, int>> g) => x.Opponent.Provinces.FirstOrDefault(p => p.Index == g.Key) is { } target
                && g.Sum(a => x.Card(a.Key) is { } c ? UnitForceOf(x, c) : 0) + x.Self.Army.Where(c => c.ProvinceIndex == g.Key && c.Definition?.Type == CardType.Personality).Sum(c => UnitForceOf(x, c)) > target.Strength + defenders;
            return command with { Assignments = rest.GroupBy(a => a.Value).Where(Beats).SelectMany(g => g).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal) };
        }
        return command;
    }

    private static GameCommand? PrepareCore(Ctx x, ActionOption action)
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
            if (action.Type == "attack" && (Rule(x, "lean") || Rule(x, "lean2") || Rule(x, "lean3")) && !HonorRace(x))
            {
                // Issue #1185 tuning (lean): the smallest razing armies, everything else home. In the Cavalry segment, armies already razing get nobody more.
                if (x.View.Phase == Phase.CavalryDeclaration && AlreadyAssigned(x).Length > 0) { if (Rule(x, "lean") || Rule(x, "lean3") && LeanArmiesRaze(x) || Rule(x, "lean2") && LeanArmiesSuffice(x)) return null; }
                else if (LeanPlan(x, units, provinces) is { } lean) return command with { Assignments = lean };
                else if (Rule(x, "lean")) return null; // lean2 falls back to the ordinary attack
            }
            if (action.Type == "attack" && Rule(x, "readA") && x.View.Phase == Phase.InfantryDeclaration && AlreadyAssigned(x).Length == 0 && LethalPlan(x, units, provinces) is null && ReadAnswers(x) is { Opportunity.Attacks: true } answersA)
            {
                // issue #1206 (readA): the opportunity's shape, restricted to what the engine offers; the Cavalry waits for its own segment. raShape (formulation 2): only the shape, the Contender decides attack or hold
                var shape = OfferedOnly(answersA.Opportunity.Best.Assignment.Where(a => provinces.Contains(a.Value)).ToDictionary(a => a.Key, a => a.Value, StringComparer.Ordinal), units);
                if (shape.Count > 0) return command with { Assignments = HoldCavalry(x, shape) };
            }
            if (action.Type == "attack")
            {
                // Mendon's spec (issue #642): as many sub-armies as can each beat their province plus every unit that could defend it.
                var assigned = AlreadyAssigned(x);
                var plan = PlanAttack(x, units, provinces, assigned) is { } whole ? HoldCavalry(x, OfferedOnly(whole, units)) : null;
                if (LethalPlan(x, units, provinces) is { } lethal) return command with { Assignments = lethal }; // issue #1185 tuning (lethal)
                if (HonorAttackOf(x, units, provinces) is { } honorAttack) return command with { Assignments = HoldCavalry(x, honorAttack.Assignments) }; // issue #818 item 8
                if (!HonorRace(x) && PlannerAssignment(x, units, provinces) is { } planned) return command with { Assignments = planned }; // issue #1185: the plan's own attack
                if (Rule(x, "maxraze") && !HonorRace(x) && x.View.Phase == Phase.InfantryDeclaration && MaxRazePlan(x, units, provinces) is { } razing
                    && razing.Values.Distinct().Count() > (plan?.Values.Distinct().Count() ?? 0)) return command with { Assignments = razing }; // issue #1185 tuning: more Provinces this turn
                if (plan is not null && Rule(x, "favoraware") && !HonorRace(x) && FavorStopsAttack(x, plan)) return null; // issue #1185 tuning: the visible Favor sends the lone army's biggest unit home
                if (plan is not null) return command with { Assignments = plan };
                if ((Rule(x, "aggro") || Rule(x, "aggro2")) && AggroAttack(x, units, provinces) is { } aggro) return command with { Assignments = HoldCavalry(x, aggro) }; // issue #1185 tuning
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
                    .Where(p => !(Rule(x, "keepbody") && p.Card!.Definition!.Type == CardType.Personality && EffectiveCost(x, p.Card) <= ready)) // issue #1185 tuning: an affordable body is never cycled away
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
            // spreadwide (Mendon, grill 2026-11-09: "spread those out so that you can be more versatile"): once one unit beats the strongest Province alone, further attachments go to the
            // weakest unbowed Personality, so one bow or kill does not take the whole army.
            if (Rule(x, "spreadwide") && action.Type == "equip" && targetCards.Where(c => c.OwnerId == self.Id && !c.Bowed && c.Definition?.Type == CardType.Personality).ToArray() is { Length: > 1 } wide
                && wide.Max(c => UnitForce(x, c)) > opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(0).Max() + 2)
                ordered = targetCards.OrderBy(c => wide.Contains(c) ? 0 : 1).ThenBy(c => wide.Contains(c) ? UnitForce(x, c) : 0).ThenByDescending(c => c.Force);
            // spreadvwide (Mendon, 2026-11-09): a Personality with no attachment gets one before any Personality gets a second; a Kensai may carry two Weapons.
            if (Rule(x, "spreadvwide") && action.Type == "equip" && x.Card(command.CardId)?.Definition is { } gear
                && (!Rule(x, "svwgate") || self.Home.Concat(self.Army).Where(c => c.OwnerId == self.Id && c.Definition?.Type == CardType.Personality && !c.Bowed).Any(c => UnitForce(x, c) > opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(0).Max() + 2)))
            {
                var weapon = gear.Keywords?.Contains("Weapon", StringComparer.OrdinalIgnoreCase) == true;
                var cards = self.Home.Concat(self.Army).ToArray();
                int Load(CardView c)
                {
                    if (c.OwnerId != self.Id || c.Definition?.Type != CardType.Personality) return 99;
                    var on = cards.Where(a => a.AttachedTo == c.Id).ToArray();
                    var weapons = on.Count(a => a.Definition?.Keywords?.Contains("Weapon", StringComparer.OrdinalIgnoreCase) == true);
                    var kensai = c.Definition.Keywords?.Contains("Kensai", StringComparer.OrdinalIgnoreCase) == true;
                    return weapon && kensai && weapons < 2 && on.Length == weapons ? 0 : on.Length;
                }
                ordered = ordered.OrderBy(Load).ThenBy(c => c.Bowed).ThenByDescending(c => c.Force);
            }
            // Issue #1185 tuning (spread): a unit that already beats the strongest Province and every defender gains nothing more; the attachment goes to the next unit, so two armies can raze.
            if (Rule(x, "spread") && action.Type == "equip" && targetCards.Length > 1 && targetCards.Where(c => c.OwnerId == self.Id).ToArray() is { Length: > 1 } mine)
            {
                var cap = opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(0).Max() + ArmyForce(opponent.Home) + 2;
                var under = mine.Where(c => !c.Bowed && UnitForce(x, c) <= cap).OrderByDescending(c => UnitForce(x, c)).ToArray();
                if (under.Length > 0 && mine.Any(c => UnitForce(x, c) > cap)) ordered = under.Concat(targetCards.Except(under)).OrderBy(c => under.Contains(c) ? 0 : 1);
            }
            var bowStrategy = x.Memory.UseBattlePolicy && action.Type == "playStrategy" && x.Card(command.CardId)?.Definition?.Effect == "bow-small" ? PickBowTarget(x, targetCards.Select(c => c.Id).ToArray(), null) : null;
            var withTarget = WithTarget(command, action, bowStrategy?.Id ?? ordered.ThenBy(c => c.Id, StringComparer.Ordinal).First().Id);
            return withTarget is null ? null : Complete(x, action, withTarget);
        }
        return FightTarget(x, action, Complete(x, action, command));
    }

    /// <summary>Issue #1206 (readE): when the Fight picks this action with a target of its own choosing (the enemy to hit, the card to discard for Force), the prepared command takes that target.</summary>
    private static GameCommand? FightTarget(Ctx x, ActionOption action, GameCommand? command)
    {
        if (command is null || !Rule(x, "readE") || action.Type is not ("useAbility" or "playStrategy") || x.View.Phase != Phase.Battle || ReadFight(x) is not { Plan.Pick: { } pick }) return command;
        var target = pick.Split('|') is { Length: 5 } parts && SameFightAction(FightKey(command with { TargetId = parts[4] }), pick) ? parts[4] : "";
        return target.Length > 0 && target != command.TargetId && (action.TargetIds?.Contains(target) ?? false) ? command with { TargetId = target } : command;
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

    private static double Score(Ctx x, ActionOption action, GameCommand command) => Contender2Score(x, action, command, TriageScore(x, action, command, PlanScore(x, action, command, Adjust(x, action, command, ScoreCore(x, action, command)))));

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
                // issue #1206 (readA, RULE_FAMILIES.md family A): the Read's opportunity decides attack or hold; a lethal attack is declared as before
                if (Rule(x, "readA") && !Rule(x, "raShape") && LethalPlan(x, x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray(), x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray()) is null && ReadAnswers(x) is { } answersA)
                    return answersA.Opportunity.Attacks ? 150 : PassScore - 1;
                return DeclareScore(x);
            case "attack":
                if (command.Assignments is not { Count: > 0 }) return -100;
                // issue #1206 (readA): the opportunity's own shape is the attack; the old estimator does not judge it
                if (Rule(x, "readA") && x.View.Phase == Phase.InfantryDeclaration && ReadAnswers(x) is { Opportunity.Attacks: true } readAttack
                    && command.Assignments.All(a => readAttack.Opportunity.Best.Assignment.TryGetValue(a.Key, out var p) && p == a.Value)) return 150;
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
                if (Rule(x, "readD") && x.View.Phase == Phase.DefenseAssignment && ReadDefence(x, action) is { Why: not "nobody attacks" } readPlan) return readPlan.Nobody ? PassScore - 1 : 150; // issue #1206 (family D)
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
