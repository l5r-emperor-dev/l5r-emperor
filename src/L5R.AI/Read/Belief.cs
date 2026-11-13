using L5R.Core;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.3): what the opponent's hand probably holds, by effect class. A prior over the classes a Fate hand holds, updated by every card of theirs
// a player at the table has seen (their discard pile, cards removed from the game, attachments in play, cards they showed). Likely is the expected count in the hand now; Worst an upper bound.
// Uses only what a human could see: never the hand, the deck or the seed.
public sealed record Belief(int HandCount, IReadOnlyDictionary<EffectClass, double> Share, int Seen, EffectCondition[][] ConditionsSeen, int AnswersSeen = 0)
{
    /// <summary>The prior share of a hand before anything is shown: Battle-timed answers are about four cards in ten of a typical Fate hand (the Contender's own UnseenStrategyShare), spread over the classes.</summary>
    public static readonly IReadOnlyDictionary<EffectClass, double> Prior = new Dictionary<EffectClass, double>
    {
        [EffectClass.SendHome] = .09,
        [EffectClass.Bow] = .10,
        [EffectClass.Destroy] = .08,
        [EffectClass.ForceCut] = .06,
        [EffectClass.Negate] = .04,
        [EffectClass.ForceAdd] = .10,
        [EffectClass.Duel] = .02,
        [EffectClass.Lock] = .01
    };
    public const double PriorWeight = 6; // the prior counts as six seen cards

    public static readonly EffectClass[] Classes = [EffectClass.SendHome, EffectClass.Bow, EffectClass.Destroy, EffectClass.ForceCut, EffectClass.Negate, EffectClass.ForceAdd, EffectClass.Duel, EffectClass.Lock];

    /// <summary>The belief about <paramref name="them"/>'s hand from what <paramref name="view"/> shows of their Fate cards.</summary>
    public static Belief Of(MatchView view, PlayerView them)
    {
        var fate = them.Discard.Concat(them.RemovedFromGame).Concat(them.Home.Concat(them.Army).Where(c => c.AttachedTo is not null))
            .Where(c => c.Definition is { } d && d.Type is CardType.Strategy or CardType.Follower or CardType.Item or CardType.Spell or CardType.Ring).Select(c => c.Definition!)
            .Concat((view.ShownCards ?? []).Where(s => s.ShownByPlayerId == them.Id && s.Definition.Type is CardType.Strategy or CardType.Follower or CardType.Item or CardType.Spell).Select(s => s.Definition)).ToArray();
        var counts = Classes.ToDictionary(c => c, _ => 0.0); var conditions = new List<EffectCondition[]>(); var answers = 0;
        foreach (var def in fate)
        {
            var d = (def.Abilities ?? []).Select(EffectDescriptors.Of).FirstOrDefault(x => x.IsBattle && Classes.Contains(x.Class));
            if (d is null) continue;
            counts[d.Class] += 1; if (d.Neutralises) { answers++; if (d.Conditions.Length > 0) conditions.Add(d.Conditions); }
        }
        var share = Classes.ToDictionary(c => c, c => (Prior[c] * PriorWeight + counts[c]) / (PriorWeight + fate.Length));
        return new(them.HandCount, share, fate.Length, conditions.ToArray(), answers);
    }

    public int Likely(EffectClass c) => (int)Math.Round(HandCount * Share.GetValueOrDefault(c));
    public int Worst(EffectClass c) => Math.Min(HandCount, (int)Math.Ceiling(HandCount * Share.GetValueOrDefault(c)) + (HandCount > 0 ? 1 : 0));

    /// <summary>Synthetic answers standing for the hand: <paramref name="worst"/> takes the upper bound of each class. Each costs the two Gold a typical Strategy does, so Gold caps them.</summary>
    public IEnumerable<Answer> Answers(bool worst)
    {
        foreach (var c in Classes.Where(c => c != EffectClass.ForceAdd))
            for (var i = 0; i < (worst ? Worst(c) : Likely(c)); i++)
                yield return new(new("belief-" + c, "Battle", null, c, c == EffectClass.ForceCut ? 3 : c == EffectClass.Destroy ? 4 : null, false, TargetSide.Enemy, TargetKind.UnitCard,
                    c == EffectClass.Destroy ? [new("force-at-most", 4), new("without-followers")] : [], [], null, false, false, false, false, false, 0, 0, false, false, 1, false, null), "hand?", true, 2, true);
        for (var i = 0; i < (worst ? Worst(EffectClass.ForceAdd) : Likely(EffectClass.ForceAdd)); i++)
            yield return new(new("belief-ForceAdd", "Battle", null, EffectClass.ForceAdd, 3, false, TargetSide.Own, TargetKind.Personality, [], [], null, false, false, false, false, false, 0, 0, false, false, 1, false, null), "hand?", true, 1, true);
    }

    /// <summary>Every neutraliser they have shown carries a condition (PLAYER_MODEL.md 3.1, "immunity beats numbers"): a unit that fails all of them attacks alone.</summary>
    public bool AllAnswersConditional => AnswersSeen > 0 && AnswersSeen == ConditionsSeen.Length;

    public override string ToString() => $"hand {HandCount}, seen {Seen}: " + string.Join(" ", Classes.Select(c => $"{c}={Likely(c)}/{Worst(c)}"));
}
