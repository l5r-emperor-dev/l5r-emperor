using L5R.Core;

namespace L5R.AI;

// Issue #642: the completer. The engine offers each action with whatever it could fill in itself (the command) and the choices a player still has to make (targets,
// slots, a payment, a cost option, a destination, a player). The bot answers every one of them from that metadata alone, never from a card's name.
public static partial class TrainingOpponent
{
    /// <summary>Fills in every choice an action still needs, once its target (if any) is known.</summary>
    private static GameCommand Complete(Ctx x, ActionOption a, GameCommand command)
    {
        var intent = AbilityIntent.Of(command.AbilityId);
        // Multi-target actions: one selection per up-front slot.
        // Issue #1185 tuning (provaim): a strength cut outside a battle names the opponent's Province.
        var cutsStrength = Rule(x, "provaim") && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var cutAbility) && LowersStrength.IsMatch(AbilityIntent.EffectText(cutAbility.Label ?? ""));
        if (a.Slots is { Length: > 0 } slots && command.Selections is null) command = command with { Selections = BuildSelections(x, slots, cutsStrength ? intent with { Rank = EffectRank.Penalize, Side = TargetSide.Enemy } : intent, cutsStrength) };
        // "Choose a player": a gain goes to the bot, a loss to its opponent, either way by its Honor profile.
        if (command.TargetPlayerId is null && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) && ability.Target == "player")
            command = command with { TargetPlayerId = ChoosePlayer(x, intent) };
        // Gold: the engine's own first payment for the quote; a variable cost pays its minimum.
        if (a.Gold is { Error: null } gold)
        {
            if (gold.MinAmount is int min && command.GoldAmount is null) command = command with { GoldAmount = min };
            if (gold.Cost > 0 && (command.SourceIds is null || command.SourceIds.Length == 0))
            {
                var options = gold.MinAmount is int m && gold.PaymentsByAmount is not null && gold.PaymentsByAmount.TryGetValue(m, out var byAmount) && byAmount.Length > 0 ? byAmount : gold.Payments;
                if (options.Length > 0) command = command with { SourceIds = options[0].SourceIds };
            }
        }
        if (a.AlternativeCost is { } alternative && command.CostOption is null) command = command with { CostOption = alternative.Id };
        if (a.Type == "useAbility" && a.Provinces is { Length: > 0 } provinces && command.Province is null)
            // Issue #788: units are only moved into a battlefield the enemy holds (the weakest opposition first); any other destination is a last resort.
            command = command with { Province = a.Movement is null || !x.Memory.UseBattlePolicy ? provinces[0] : provinces.OrderBy(p => EnemyForceAt(x, p) == 0).ThenBy(p => EnemyForceAt(x, p)).ThenBy(p => p).First() };
        // Issue #1185 tuning (provaim): outside a battle, a cut to a Province's strength goes to the opponent's weakest Province, the one the attack will then take.
        if (Rule(x, "provaim") && x.Odds is null && a.Type == "useAbility" && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var cut)
            && LowersStrength.IsMatch(AbilityIntent.EffectText(cut.Label ?? "")) && x.Opponent.Provinces.Where(p => !p.Destroyed).OrderBy(p => p.Strength).ThenBy(p => p.Index).FirstOrDefault() is { } weakest)
            command = command with { Province = weakest.Index, TargetPlayerId = x.Opponent.Id };
        // Issue #1003: a cut to a Province's strength goes to the Province under attack, whoever's it would otherwise be offered first.
        if (x.Odds is { IsAttacker: true } && AttackedProvince(x) is int attacked && LowersAttackedProvince(x, a, command)) command = command with { Province = attacked, TargetPlayerId = x.Opponent.Id };
        return command;
    }

    /// <summary>
    /// Mendon's rule (2026-10-18, Block Supply Lines): when an ability sends one of the bot's own units home to save it, the unit saved is the one whose printed Gold cost, the Personality plus everything attached,
    /// is highest. <paramref name="chosen"/> stays the pick on a tie and when no other own Personality is offered.
    /// </summary>
    private static CardView CostliestOwnUnit(Ctx x, string[] legal, CardView chosen)
    {
        var all = x.Cards.Values.ToArray();
        var units = legal.Select(x.Card).OfType<CardView>().Where(c => c.OwnerId == x.Self.Id && c.Definition?.Type == CardType.Personality).ToArray();
        return units.Length == 0 ? chosen : units.OrderByDescending(c => BattlePolicy.UnitGold(c, all)).ThenBy(c => c.Id == chosen.Id ? 0 : 1).ThenBy(c => c.Id, StringComparer.Ordinal).First();
    }

    private static int ChoosePlayer(Ctx x, AbilityIntent intent)
    {
        if (intent.HonorGain && intent.HonorLoss) return WantsOwnGain(x) ? x.Self.Id : x.Opponent.Id;
        if (intent.HonorGain) return x.Self.Id;
        if (intent.HonorLoss || intent.Side == TargetSide.Enemy || intent.Rank >= EffectRank.Penalize) return x.Opponent.Id;
        return x.Self.Id;
    }

    private static SlotSelection[] BuildSelections(Ctx x, TargetSlotOption[] slots, AbilityIntent intent, bool weakestEnemyProvince = false)
    {
        var chosen = new List<SlotSelection>();
        foreach (var slot in slots.Where(s => s.Timing == TargetSlotTimings.UpFront && s.Kind != TargetSlotKinds.FollowUp))
        {
            if (slot.Kind == TargetSlotKinds.Player)
            {
                var wanted = ChoosePlayer(x, intent);
                chosen.Add(new(slot.Slot, PlayerId: slot.PlayerIds.Contains(wanted) ? wanted : slot.PlayerIds.FirstOrDefault(wanted)));
                continue;
            }
            // A province slot (issue #699, Benediction): a harmful ability names the opponent's province, anything else the bot's own, the first the engine lists.
            if (slot.Kind == TargetSlotKinds.Province)
            {
                var wantEnemy = intent.Rank >= EffectRank.Penalize && !slot.Label.Contains("+", StringComparison.Ordinal) || intent.Side == TargetSide.Enemy;
                var owner = wantEnemy ? x.Opponent.Id : x.Self.Id;
                var choice = (weakestEnemyProvince ? slot.ProvinceChoices?.Where(p => p.Owner == owner).OrderBy(p => x.Opponent.Provinces.FirstOrDefault(q => q.Index == p.Index)?.Strength ?? 99).FirstOrDefault() : null)
                    ?? slot.ProvinceChoices?.FirstOrDefault(p => p.Owner == owner) ?? slot.ProvinceChoices?.FirstOrDefault();
                if (choice is not null) chosen.Add(new(slot.Slot, ProvinceOwner: choice.Owner, Province: choice.Index));
                continue;
            }
            // Take as many cards as the slot allows (at least what it requires, at most what is legal), best candidates first.
            var count = Math.Min(slot.CardIds.Length, Math.Max(slot.Min, Math.Min(slot.Max, slot.Min == 0 ? 1 : slot.Max)));
            var label = slot.Label.ToLowerInvariant();
            var side = label.Contains("enemy", StringComparison.Ordinal) || label.Contains("another player", StringComparison.Ordinal) || label.Contains("opponent", StringComparison.Ordinal) ? TargetSide.Enemy
                : label.Contains("your", StringComparison.Ordinal) ? TargetSide.Own : intent.Side;
            var picks = Rank(x, slot.CardIds, intent with { Side = side }, slot.Label).Take(count).Select(c => c.Id).ToList();
            foreach (var id in slot.CardIds) if (picks.Count < count && !picks.Contains(id)) picks.Add(id);
            chosen.Add(new(slot.Slot, picks.Take(count).ToArray()));
        }
        return chosen.ToArray();
    }

    /// <summary>The best card among the offered ids for what the ability does: its enemies when it hurts, its own when it helps.</summary>
    private static CardView? PickCard(Ctx x, string[] ids, AbilityIntent intent, string label) => Rank(x, ids, intent, label).FirstOrDefault();

    private static IEnumerable<CardView> Rank(Ctx x, string[] ids, AbilityIntent intent, string label)
    {
        var cards = ids.Select(id => x.Card(id)).OfType<CardView>().ToArray();
        var harm = intent.Rank >= EffectRank.Penalize || intent.Side == TargetSide.Enemy;
        var wantEnemy = intent.Side == TargetSide.Enemy || intent.Side != TargetSide.Own && harm;
        var preferred = cards.Where(c => (c.OwnerId != x.Self.Id) == wantEnemy).ToArray();
        if (preferred.Length == 0) preferred = cards;
        var straighten = label.Contains("straighten", StringComparison.OrdinalIgnoreCase);
        return preferred
            .OrderBy(c => wantEnemy ? c.Bowed : straighten ? !c.Bowed : c.Bowed)
            .ThenByDescending(c => wantEnemy && x.Memory.UsePlannerTriage && x.Memory.TriageTargets ? ThreatValue(x, c) : c.Definition?.Type == CardType.Personality ? 1 : 0) // issue #1185: what the card can still do, not just its type
            .ThenByDescending(c => c.Definition?.Type == CardType.Personality)
            .ThenByDescending(c => c.Force)
            .ThenBy(c => c.Id, StringComparer.Ordinal);
    }

    /// <summary>Mendon's attack rule (issue #642): see <see cref="AttackPlanner"/>. Units are the unbowed Personalities that may be assigned, each counted with its attached Followers.</summary>
    private static Dictionary<string, int>? PlanAttack(Ctx x, CardView[] units, int[] provinces, CardView[]? alreadyAssigned = null)
    {
        // Issue #946: the Personalities already sent in the Infantry Maneuvers Segment are part of the army the Cavalry joins.
        var unitForces = units.Concat(alreadyAssigned ?? []).Select(c => new AttackPlanner.Unit(c.Id, ArmyForce(x.Self.Home.Concat(x.Self.Army).Where(h => h.Id == c.Id || h.AttachedTo == c.Id)))).ToArray();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).Select(p => new AttackPlanner.Target(p.Index, p.Strength)).ToArray();
        // The opponent's Honor victory is coming (Mendon's rule, playtest 2026-10): the bot attacks as many Provinces as it can beat, whatever the enemy army could do in reply.
        var spec = x.Memory.UseSpec818;
        return AttackPlanner.Plan(unitForces, targets, HonorRace(x) ? 0 : ArmyForce(x.Opponent.Home), spec ? HandForceBonuses(x) : null, spec);
    }

    /// <summary>The opponent already holds the Honor it needs and will win it at the start of its next turn, so the bot cannot afford a cautious attack.</summary>
    // clock (issue #1185 tuning): race earlier, from clockH Honor, against an Honor deck that is near its win.
    private static bool HonorRace(Ctx x) => x.Opponent.Honor >= (Rule(x, "clock") ? Knob(x, "clockH", 34) : 40);

    /// <summary>When the attackers can destroy EVERY Province the bot has left, the battlefield where its defense is likeliest to save one (the largest Strength plus its own Force over the attackers'); otherwise null.</summary>
    private static int? LastStandProvince(Ctx x, int[] offered)
    {
        var remaining = x.Self.Provinces.Where(p => !p.Destroyed).ToArray();
        if (remaining.Length == 0) return null;
        int Attackers(int index) => ArmyForce(x.Opponent.Army.Where(c => c.ProvinceIndex == index));
        if (remaining.Any(p => Attackers(p.Index) <= p.Strength)) return null;
        var own = ArmyForce(x.Self.Home);
        return remaining.Where(p => offered.Contains(p.Index)).OrderByDescending(p => p.Strength + own - Attackers(p.Index)).ThenBy(p => p.Index).Select(p => (int?)p.Index).FirstOrDefault();
    }
}
