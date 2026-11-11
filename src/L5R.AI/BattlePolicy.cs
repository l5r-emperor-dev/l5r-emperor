using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

/// <summary>
/// Issue #788 (Mendon's bot changes): the pure, card-agnostic rules the practice bot follows inside a battle. Everything here reads only the permitted
/// <see cref="MatchView"/> data and printed catalogue facts, never a card's name, so a card wired tomorrow is covered the moment it exists.
/// </summary>
public static class BattlePolicy
{
    /// <summary>The Force picture of the battle the viewer is in. Strength is null at a battlefield that is not a Province (an extra battlefield).</summary>
    public sealed record Odds(bool IsAttacker, int Own, int Enemy, int? Strength)
    {
        /// <summary>The attacker wins with more Force; a tie goes to the defender.</summary>
        public bool Winning => IsAttacker ? Own > Enemy : Own >= Enemy;
        /// <summary>The condition Mendon's pass rule waits for: the defender wins, or the attacker wins AND the Province falls (the engine's own test: more than defenders plus Strength).</summary>
        public bool Satisfied => IsAttacker ? Strength is int s ? Own > Enemy + s : Own > Enemy : Own >= Enemy;
        /// <summary>How much of the bot's own Force it can lose while still meeting <see cref="Satisfied"/>; negative when not satisfied.</summary>
        public int Margin => IsAttacker ? (Strength is int s ? Own - Enemy - s - 1 : Own - Enemy - 1) : Own - Enemy;
        /// <summary>For a defender: whether the Province would fall if the bot's Force dropped by <paramref name="lost"/> (the attacker beats defenders plus Strength).</summary>
        public bool WouldFallAfterLosing(int lost) => !IsAttacker && Strength is int s && Enemy > Own - lost + s;

        /// <summary>
        /// Mendon's outcome ladder, worst to best, with <paramref name="gain"/> more Force on the bot's side. Defender: 0 loses and loses the Province, 1 loses, 2 ties, 3 wins.
        /// Attacker: 0 loses, 1 ties, 2 wins, 3 wins and destroys the Province (a battlefield with no Province tops out at winning). A tie is its own rung here, as it destroys both armies.
        /// </summary>
        public int Tier(int gain = 0)
        {
            var own = Own + gain;
            if (IsAttacker) return own < Enemy ? 0 : own == Enemy ? 1 : Strength is int s && own <= Enemy + s ? 2 : 3;
            return own > Enemy ? 3 : own == Enemy ? 2 : Strength is int p && Enemy > own + p ? 0 : 1;
        }
    }

    /// <summary>How many cards the bot will equip and use to change a battle's outcome (Mendon: "3 or fewer").</summary>
    public const int MaxEquipsToChangeOutcome = 3;

    /// <summary>
    /// The largest set of at most <see cref="MaxEquipsToChangeOutcome"/> attachments whose combined cost fits <paramref name="gold"/>, taking the biggest Force gains first.
    /// Returns the chosen ids; empty when none adds Force.
    /// </summary>
    public static string[] EquipPlan(IEnumerable<(string Id, int Gain, int Cost)> options, int gold)
    {
        var chosen = new List<string>();
        foreach (var o in options.Where(o => o.Gain > 0).OrderByDescending(o => o.Gain).ThenBy(o => o.Cost).ThenBy(o => o.Id, StringComparer.Ordinal))
        {
            if (chosen.Count == MaxEquipsToChangeOutcome || o.Cost > gold) continue;
            gold -= o.Cost; chosen.Add(o.Id);
        }
        return chosen.ToArray();
    }

    /// <summary>The battle the viewer is in, or null when there is none.</summary>
    public static Odds? Assess(MatchView view)
    {
        var province = view.BattleStep?.BattlefieldProvince ?? view.CurrentBattleProvince;
        if (province is not int index) return null;
        if (view.Phase is not (Phase.Battle or Phase.BattleSelection) && view.BattleStep is null) return null;
        var attackerId = view.BattleStep?.AttackerPlayerId ?? view.ActivePlayerId;
        var self = view.Players.SingleOrDefault(p => p.Id == view.ViewerId);
        var other = view.Players.SingleOrDefault(p => p.Id != view.ViewerId);
        if (self is null || other is null) return null;
        var isAttacker = attackerId == view.ViewerId;
        var defender = isAttacker ? other : self;
        int? strength = defender.Provinces.FirstOrDefault(p => p.Index == index && !p.Destroyed)?.Strength;
        return new(isAttacker, Force(self.Army.Where(c => c.ProvinceIndex == index)), Force(other.Army.Where(c => c.ProvinceIndex == index)), strength);
    }

    public static int Force(IEnumerable<CardView> cards) => cards.Where(c => !c.Bowed && c.Definition?.Type is CardType.Personality or CardType.Follower).Sum(c => Math.Max(0, c.Force));

    // ---- reading abilities -------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly Regex SendHome = new(@"\b(send|sends|move|moves)\b[^.:]*\bhome\b", RegexOptions.IgnoreCase);
    private static readonly Regex Move = new(@"\bmove\b", RegexOptions.IgnoreCase);
    private static readonly Regex AttackX = new(@"(?:Ranged|Melee)\s+(?:Attack\s+)?(\d+)|(\d+)\s+(?:Ranged|Melee)|Attack\s+(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex Augment = new(@"\+\s?(\d+)", RegexOptions.IgnoreCase);

    private static string Effect(AbilityDefinition ability) => AbilityIntent.EffectText(ability.Label ?? "");

    public static bool SendsHome(string? abilityId) => abilityId is not null && AbilityCatalog.All.TryGetValue(abilityId, out var a) && SendHome.IsMatch(Effect(a));
    public static bool Moves(string? abilityId) => abilityId is not null && AbilityCatalog.All.TryGetValue(abilityId, out var a) && Move.IsMatch(Effect(a)) && !SendHome.IsMatch(Effect(a));

    /// <summary>The intents of every ability printed on a card (empty for a card with none).</summary>
    public static IEnumerable<(string Id, AbilityDefinition Ability, AbilityIntent Intent)> Printed(CardView card)
    {
        foreach (var id in card.Definition?.Abilities ?? [])
            if (AbilityCatalog.All.TryGetValue(id, out var ability)) yield return (id, ability, AbilityIntent.Of(ability));
    }

    /// <summary>Destroys, bows or moves cards: the abilities that threaten the bot's army.</summary>
    public static bool IsThreatening(string id, AbilityDefinition ability, AbilityIntent intent) =>
        intent.Rank is EffectRank.Destroy or EffectRank.Bow && intent.Side != TargetSide.Own || SendHome.IsMatch(Effect(ability)) && intent.Side != TargetSide.Own || Move.IsMatch(Effect(ability)) && intent.Side != TargetSide.Own;

    /// <summary>An enemy Personality that can destroy, bow or move cards in the bot's army: it is able to act (not bowed, not locked) and prints such an ability.</summary>
    public static bool IsThreat(CardView card) =>
        card.Definition?.Type == CardType.Personality && !card.Bowed && !card.ActionLocked && Printed(card).Any(p => IsThreatening(p.Id, p.Ability, p.Intent));

    /// <summary>A card that can still use at least one printed ability: unbowed (or an ability that works bowed is rare enough to ignore) and not action-locked.</summary>
    public static bool HasUsableAbility(CardView card) =>
        !card.Bowed && !card.ActionLocked && Printed(card).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen" or "Reaction" or "Open" or "Limited");

    /// <summary>The enemy is not a real threat this battle: nobody at the battlefield, or only bowed Personalities and Personalities with no usable printed ability, or a hand of at most two cards.</summary>
    public static bool Unthreatened(IEnumerable<CardView> enemyArmy, int enemyHandCount)
    {
        var personalities = enemyArmy.Where(c => c.Definition?.Type == CardType.Personality).ToArray();
        return enemyHandCount <= 2 || personalities.Length == 0 || personalities.All(c => c.Bowed || !HasUsableAbility(c));
    }

    // ---- attacks ------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Force an Attack ability can destroy up to (its printed X), or null when the label gives no number.</summary>
    public static int? AttackStrength(string? abilityId)
    {
        if (abilityId is null || !AbilityCatalog.All.TryGetValue(abilityId, out var ability) || ability.Target != "attack") return null;
        var m = AttackX.Match(ability.Label ?? "");
        if (!m.Success) return null;
        foreach (var g in m.Groups.Cast<Group>().Skip(1)) if (g.Success) return int.Parse(g.Value);
        return null;
    }

    /// <summary>The bonus an ability adds to a Ranged or Melee Attack ("+2" in an ability that names an Attack), or 0.</summary>
    public static int AttackAugment(AbilityDefinition ability)
    {
        var label = Effect(ability);
        if (!label.Contains("Attack", StringComparison.OrdinalIgnoreCase) || ability.Target == "attack") return 0;
        var m = Augment.Match(label);
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    /// <summary>A printed gold cost summed over a card and everything attached to it (Items, Spells, Followers): the "unit" total Mendon asks the bot to compare.</summary>
    public static int UnitGold(CardView card, IEnumerable<CardView> all) =>
        (card.Definition?.GoldCost ?? 0) + all.Where(c => c.AttachedTo == card.Id).Sum(c => c.Definition?.GoldCost ?? 0);

    /// <summary>
    /// Printed Battle actions the card could still use: its Battle (or Battle or Open) abilities that have not used up their uses this turn (the view's SpentAbilityIds), none when it is bowed or may not act.
    /// </summary>
    public static int UnusedBattleActions(CardView card) =>
        card.Bowed || card.ActionLocked ? 0 : Printed(card).Count(p => p.Ability.Timing is "Battle" or "BattleOrOpen" && card.SpentAbilityIds?.Contains(p.Id) != true);

    /// <summary>
    /// Issue #788, rule 2, as Mendon changed it on 2026-11-11: the Attack target ranking. The largest Force the Attack can destroy (Force at most <paramref name="strength"/>, which the caller raises
    /// by every Attack augment still available). A tie goes to the card with the most printed Battle actions it could still use, and a further tie to the Personality over the Follower. Targets above the
    /// strength rank below every destroyable one, weakest first, so a failed Attack at least aims at the card nearest to falling.
    /// </summary>
    public static double AttackTargetScore(CardView target, int strength, IEnumerable<CardView> all)
    {
        if (target.Force > strength) return 500 - target.Force;
        var battle = Math.Min(UnusedBattleActions(target), 9);
        return 1000 + target.Force * 100 + battle * 10 + (target.Definition?.Type == CardType.Personality ? 1 : 0);
    }

    // ---- trade attacks ------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Extra value (in Gold) of destroying or losing a Personality over a bare attachment: Personalities are the cards that carry a deck.</summary>
    public const int PersonalityBonus = 2;
    private const int MaxTradeUnits = 12;

    /// <summary>The worth of a unit in Gold: its own printed cost plus everything attached to it, and <see cref="PersonalityBonus"/> when it is a Personality.</summary>
    public static int UnitValue(CardView unit, IEnumerable<CardView> home) => UnitGold(unit, home) + (unit.Definition?.Type == CardType.Personality ? PersonalityBonus : 0);

    /// <summary>
    /// An attack that does not raze the Province but is still worth making. <paramref name="Net"/> is the Gold-worth of the defenders it destroys minus what it risks.
    /// A battle won outright (more Force than every possible defender) destroys the whole defending army and costs the attacker nothing; a tie destroys both armies,
    /// so it is worth making only when the defenders are worth clearly more than the attackers.
    /// </summary>
    public sealed record Trade(string[] UnitIds, int Sent, int Defence, int Kills, int Losses, bool Tie, int Net);

    /// <summary>
    /// Issue #788 (Mendon): "count a failed attack as useful if it destroys defenders' cards (Personalities most valuable, attachments also valuable), especially if the Gold
    /// traded is in the attacker's favour". The engine destroys the losing army and leaves the winner's untouched; a tie destroys both. <paramref name="enemyHome"/> is every
    /// card the opponent has at home; every unbowed Personality there could defend, so the whole of it is counted as the defence (the safe reading, as the attack planner does).
    /// Returns the best subset of <paramref name="ownUnits"/> to send, or null when no subset beats the defence or trades well. Pure and deterministic.
    /// </summary>
    public static Trade? PlanTrade(IReadOnlyList<CardView> ownUnits, IReadOnlyCollection<CardView> ownHome, IReadOnlyCollection<CardView> enemyHome)
    {
        var defenders = enemyHome.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var defence = Force(enemyHome);
        var kills = defenders.Sum(d => UnitValue(d, enemyHome));
        if (defence <= 0 || kills <= 0) return null;
        var candidates = ownUnits.Where(u => ForceOf(u, ownHome) > 0).OrderByDescending(u => ForceOf(u, ownHome)).ThenBy(u => u.Id, StringComparer.Ordinal).Take(MaxTradeUnits).ToArray();
        Trade? best = null;
        for (var mask = 1; mask < 1 << candidates.Length; mask++)
        {
            var subset = candidates.Where((_, i) => (mask >> i & 1) == 1).ToArray();
            if (Evaluate(subset, ownHome, defence, kills) is { } t && (best is null || Better(t, best))) best = t;
        }
        return best;

        static bool Better(Trade a, Trade b) =>
            a.Net != b.Net ? a.Net > b.Net : a.Losses != b.Losses ? a.Losses < b.Losses : a.Sent != b.Sent ? a.Sent < b.Sent
            : string.CompareOrdinal(string.Join(",", a.UnitIds), string.Join(",", b.UnitIds)) < 0;
    }

    /// <summary>The trade a particular set of attackers makes against a defence of <paramref name="defence"/> Force worth <paramref name="kills"/> Gold, or null when it is not worth making.</summary>
    public static Trade? Evaluate(IReadOnlyCollection<CardView> sentUnits, IReadOnlyCollection<CardView> ownHome, int defence, int kills)
    {
        var sent = sentUnits.Sum(u => ForceOf(u, ownHome));
        if (sent < defence || defence <= 0) return null;
        var losses = sentUnits.Sum(u => UnitValue(u, ownHome));
        var ids = sentUnits.Select(u => u.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        if (sent == defence)
        {
            var tieNet = kills - losses - 4; // a tie is fragile: one enemy Force boost turns it into a loss of the whole army
            return tieNet >= 3 ? new(ids, sent, defence, kills, losses, true, tieNet) : null;
        }
        // Won outright. One spare point of Force over the defence is a thin margin, so a subset with two or more is preferred by a small amount.
        return new(ids, sent, defence, kills, 0, false, kills - (sent < defence + 2 ? 2 : 0));
    }

    private static int ForceOf(CardView unit, IEnumerable<CardView> home) => Force(home.Where(h => h.Id == unit.Id || h.AttachedTo == unit.Id));

    // ---- bowing -------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Issue #788, rule 3: which card a bow ability should take, best first. When the ability may bow a card with attachments, the unbowed Personality with the most attachments
    /// (ties: the highest combined printed gold cost). Otherwise the ranking is: a Personality that destroys, an attachment that destroys, an attachment that bows, a Personality
    /// that bows, a Personality with another printed battle ability, any other Personality, then anything else (a melee or ranged Attack counts as destroying). Every tie falls to
    /// higher Force, then higher printed gold, then the id, so a replay always agrees.
    /// </summary>
    public static IOrderedEnumerable<CardView> BowOrder(IEnumerable<CardView> candidates, IReadOnlyCollection<CardView> all, bool mayHaveAttachments)
    {
        var pool = candidates.Where(c => !c.Bowed).ToArray();
        if (pool.Length == 0) pool = candidates.ToArray();
        var attachments = pool.ToDictionary(c => c.Id, c => all.Count(a => a.AttachedTo == c.Id));
        if (mayHaveAttachments && pool.Any(c => c.Definition?.Type == CardType.Personality && attachments[c.Id] > 0))
            return pool.OrderByDescending(c => c.Definition?.Type == CardType.Personality).ThenByDescending(c => attachments[c.Id])
                .ThenByDescending(c => UnitGold(c, all)).ThenBy(c => c.Id, StringComparer.Ordinal);
        return pool.OrderBy(Tier).ThenByDescending(c => c.Force).ThenByDescending(c => UnitGold(c, all)).ThenBy(c => c.Id, StringComparer.Ordinal);

        static int Tier(CardView c)
        {
            var person = c.Definition?.Type == CardType.Personality;
            var printed = Printed(c).ToArray();
            var destroys = printed.Any(p => p.Intent.Rank == EffectRank.Destroy && p.Intent.Side != TargetSide.Own);
            var bows = printed.Any(p => p.Intent.Rank == EffectRank.Bow && p.Intent.Side != TargetSide.Own);
            var battle = printed.Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen");
            if (person && destroys) return 0;
            if (!person && destroys) return 1;
            if (!person && bows) return 2;
            if (person && bows) return 3;
            if (person && battle) return 4;
            return person ? 5 : 6;
        }
    }
}
