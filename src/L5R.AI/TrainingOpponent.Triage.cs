using L5R.Core;

namespace L5R.AI;

// Issue #1185, slice 3 (Mendon's interview, round 6): how a player spends abilities and chooses what to hit. Behind BotContext.UsePlannerTriage (off: the champion, unchanged).
//   1. Printed abilities already in play are used before hand cards (they come back every turn if the Personality survives).
//   2. A target is worth what it can still DO: its Force, and the printed abilities it has not used this turn. A spent ability is discounted; a Tactician that has not used its tactical ability
//      is worth up to four more Force while its owner holds cards; a keyword that carries a battle ability matters more than a descriptive one (Samurai, Courtier).
//   3. Resources (Gold, a card from hand) are not spent on a battle that is already comfortably won against units with nothing left to spring. A free printed ability is still used.
// What a card has spent this turn is not in the view. The bot remembers it from MatchView.BattleActions (each performer and ability of the battle, public): see BotContext.NoteBattleActions.
public static partial class TrainingOpponent
{
    private const double SpentDiscount = 0.2;    // an ability already used this turn is worth a fifth of an unused one
    private const int TacticianPotential = 4;    // "up to four more Force" while the opponent holds cards and the tactical ability is unspent
    private const int ComfortableMargin = 2;     // Force to spare before the bot stops spending resources on a won battle

    private static readonly HashSet<string> BattleKeywords = new(StringComparer.OrdinalIgnoreCase) { "Duelist", "Tattooed", "Magistrate", "Paragon", "Scout", "Elite", "Kiho" };
    private static readonly HashSet<string> TimedAbilities = new(StringComparer.Ordinal) { "Battle", "BattleOrOpen", "Reaction", "Open", "Limited" };

    /// <summary>What a card can still do to the bot, for choosing what to bow, destroy or send home.</summary>
    private static double ThreatValue(Ctx x, CardView card)
    {
        if (card.Definition is not { } def) return 1;
        var force = Math.Max(0, card.Force);
        var turn = x.View.TurnNumber;
        if (def.Type != CardType.Personality) return def.Type == CardType.Follower ? force * 0.6 + AbilityWeight(x, card, turn) * 0.5 : AbilityWeight(x, card, turn) * 0.5;
        var value = force + AbilityWeight(x, card, turn);
        var owner = x.View.Players.FirstOrDefault(p => p.Id == card.OwnerId);
        if (def.Keywords?.Contains("Tactician", StringComparer.OrdinalIgnoreCase) == true && owner is { HandCount: > 0 } && !x.Memory.SpentThisTurn(turn, def.Name, "tactical-advantage")) value += TacticianPotential;
        if (def.Keywords?.Any(BattleKeywords.Contains) == true) value += 1;
        else if (def.Keywords is { Length: > 0 }) value += 0.25; // a descriptive keyword (Samurai, Courtier) is a tiebreak, below any printed battle ability
        return card.Bowed ? value * 0.1 : value;
    }

    /// <summary>What a card's printed abilities add: more when they harm the bot, less when they only help their owner, a fifth of it for each already used this turn.</summary>
    private static double AbilityWeight(Ctx x, CardView card, int turn)
    {
        var total = 0.0;
        foreach (var (id, ability, intent) in BattlePolicy.Printed(card))
        {
            if (!TimedAbilities.Contains(ability.Timing ?? "")) continue;
            var weight = BattlePolicy.IsThreatening(id, ability, intent) ? 6 : intent.Rank >= EffectRank.Penalize ? 4 : intent.Rank >= EffectRank.Help ? 2 : 1;
            total += x.Memory.SpentThisTurn(turn, card.Definition!.Name, id) ? weight * SpentDiscount : weight;
        }
        return total;
    }

    /// <summary>Enemy units at the battle that can still spring something: an unbowed Personality with an unused threatening ability, or an unspent Tactician while its owner holds cards.</summary>
    private static bool EnemyCanStillSpring(Ctx x)
    {
        var turn = x.View.TurnNumber;
        return EnemyArmyAt(x).Any(c => c.Definition?.Type == CardType.Personality && !c.Bowed && !c.ActionLocked &&
            (BattlePolicy.Printed(c).Any(p => TimedAbilities.Contains(p.Ability.Timing ?? "") && BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent) && !x.Memory.SpentThisTurn(turn, c.Definition!.Name, p.Id))
            || c.Definition!.Keywords?.Contains("Tactician", StringComparer.OrdinalIgnoreCase) == true && x.Opponent.HandCount > 0 && !x.Memory.SpentThisTurn(turn, c.Definition.Name, "tactical-advantage")));
    }

    /// <summary>The triage rules over an action's score (see the header). Only battle actions are affected.</summary>
    private static double TriageScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (!x.Memory.UsePlannerTriage || x.Odds is not { } odds || action.Type is not ("useAbility" or "playStrategy" or "equip")) return score;
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        if (intent.Negates) return score;
        var source = x.Card(command.CardId);
        var inPlay = source is not null && source.OwnerId == x.Self.Id && source.Zone != Zone.Resolving && !x.Self.Hand.Any(h => h.Id == source.Id);
        var spends = (action.Gold?.Cost ?? 0) > 0 || !inPlay;
        // 3. A comfortably won battle against units with nothing left to spring: do not spend Gold or a card from hand on it. A free printed ability is still worth using.
        var changesForce = intent.Rank >= EffectRank.Penalize && intent.Side != TargetSide.Own || ForceBonusOf(x, command) > 0;
        if (x.Memory.TriageWonCap && spends && changesForce && !(x.Memory.TriageSpareKills && intent.Rank == EffectRank.Destroy) && odds.Satisfied && odds.Margin >= ComfortableMargin && !EnemyCanStillSpring(x)) return Math.Min(score, BarredScore);
        // 1. Printed abilities in play go before hand cards.
        if (x.Memory.TriagePrintedFirst && inPlay && !intent.IsReaction && intent.Rank > EffectRank.Neutral && score > PassScore) score += 12;
        return score;
    }

    private static int ForceBonusOf(Ctx x, GameCommand command) =>
        command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) ? ForceBonus(ability) : x.Card(command.CardId)?.Definition?.Effect == "force-three" ? 3 : 0;
}
