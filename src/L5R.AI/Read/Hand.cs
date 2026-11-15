namespace L5R.AI;

// Issue #1206, strategy brief step 3, family I (docs/bots/strategy/RULE_FAMILIES.md I): the Favor, the opponent's turn and the hand. Three decisions read from the Read's exchange and
// descriptors, never from a card's name (PM 3.7): the Favor's Battle action is kept while the exchange at this battlefield already goes my way without it, spent when defending turns a
// Province that falls at likely into one that holds (or in a deciding battle, my last Province); an Event that replaces the hand is declined while the hand holds three or more answers;
// at the hand limit the cards with the fewest live uses against their units in play go first.

/// <summary>The Favor's reading at a battle: spend it or keep it, the exchange without it and (defending) with it.</summary>
public sealed record FavorPlan(bool Spend, string Why, ExchangeResult Without, ExchangeResult? With);

/// <summary>The hand-limit discard: the card ids to discard, in order, with each card's live uses.</summary>
public sealed record DiscardPlan(string[] CardIds, string Why, string[] Log);

public static class Hand
{
    /// <summary>The Favor's Battle action (one attacking Personality home) at <paramref name="province"/>. <paramref name="target"/>: the attacker it would send home (the Contender's pick).</summary>
    public static FavorPlan Favor(Read read, int province, bool viewerAttacks, LedgerUnit? target, bool deciding)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them;
        var mine = me.At(province).ToArray(); var theirs = them.At(province).ToArray();
        var strength = (viewerAttacks ? them : me).Provinces.FirstOrDefault(p => p.Index == province).Strength;
        var meWithout = me with { TableAnswers = me.TableAnswers.Where(a => a.Source != "Favor").ToArray() };
        var without = viewerAttacks
            ? Exchange.Run(meWithout, mine, them, theirs, strength, me.HandAnswers, read.Belief.Answers(false))
            : Exchange.Run(them, theirs, meWithout, mine, strength, read.Belief.Answers(false), me.HandAnswers);
        if (viewerAttacks) return new(false, "attacking: the Favor is kept for a defence (it moves an attacker home, with presence)", without, null);
        if (!without.Falls)
        {
            // a deciding battle (my last Province): the Favor goes when their worst hand would take it without the Favor and the attacker home saves it (PM 3.7: spent in the battle it decides)
            if (deciding && target is not null)
            {
                var worst = Exchange.Run(them, theirs, meWithout, mine, strength, read.Belief.Answers(true), me.HandAnswers);
                if (worst.Falls && !Exchange.Run(them, theirs.Where(u => u.Id != target.Id).ToArray(), meWithout, mine, strength, read.Belief.Answers(true), me.HandAnswers).Falls)
                    return new(true, $"my last Province holds at likely but falls under their worst hand ({worst.DefenceForce} v {worst.AttackForce}); {target.Name} home saves it: spent", without, worst);
            }
            return new(false, $"defending: the Province holds at likely without it ({without.DefenceForce} v {without.AttackForce})", without, null);
        }
        if (target is null) return new(false, "defending, the Province falls, but nothing to send home", without, null);
        var with = Exchange.Run(them, theirs.Where(u => u.Id != target.Id).ToArray(), meWithout, mine, strength, read.Belief.Answers(false), me.HandAnswers);
        if (!with.Falls) return new(true, $"defending: the Province falls at likely without it and holds with {target.Name} sent home ({with.DefenceForce} v {with.AttackForce})", without, with);
        if (deciding) return new(true, $"my last Province: the Favor goes even though {target.Name} home still loses it at likely ({with.DefenceForce} v {with.AttackForce})", without, with);
        return new(false, $"defending: the Province falls at likely with or without it ({with.DefenceForce} v {with.AttackForce} after {target.Name} home): kept for a battle it decides", without, with);
    }

    /// <summary>An Event that replaces the hand (discard it, draw): false (keep the hand) while it holds three or more answers; null: the Contender decides.</summary>
    public static bool? ReplaceHand(LedgerSide me) => me.HandAnswers.Length >= 3 ? false : null;

    /// <summary>Whether a pending choice's prompt is the hand-replacing one ("Choose whether to discard your hand.").</summary>
    public static bool ReplacesHandPrompt(string? prompt) => prompt is not null && System.Text.RegularExpressions.Regex.IsMatch(prompt, @"discard\s+(your|their|his|her)\s+hand", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>Whether an Event's text replaces the hand: it discards the hand and draws.</summary>
    public static bool ReplacesHand(string? effectText) =>
        effectText is not null && System.Text.RegularExpressions.Regex.IsMatch(effectText, @"discard\s+(your|their|his|her)\s+hand", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        && System.Text.RegularExpressions.Regex.IsMatch(effectText, @"\bdraw", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>The hand-limit discard: <paramref name="count"/> cards from <paramref name="hand"/> (id, name, descriptors, the Contender's own value for ties), the fewest live uses first.
    /// A card's live uses: how many of their units in play (straightened) its targeting effects admit now, a condition against the performer judged with my best unit for it; a card without a target has one use; a card with no Battle or Open effect has one.</summary>
    public static DiscardPlan Discard(Read read, IReadOnlyList<(string Id, string Name, EffectDescriptor[] Effects, double Value)> hand, int count)
    {
        var theirs = Threat.Straightened(read.Ledger.Them.Units).Select(u => new ExchangeUnit(u)).ToArray();
        var performers = Threat.Straightened(read.Ledger.Me.Units).Select(u => (ExchangeUnit?)new ExchangeUnit(u)).Append(null).ToArray(); // a condition against the performer is judged with my best unit for it
        var log = new List<string>();
        var scored = hand.Select(c =>
        {
            var effects = c.Effects.Where(e => e.IsBattle || e.IsOpen).ToArray();
            int uses;
            if (effects.Length == 0) uses = 1;
            else
            {
                var targeting = effects.Where(e => e.Side != TargetSide.Own && e.Target is TargetKind.Personality or TargetKind.Unit or TargetKind.UnitCard).ToArray();
                uses = targeting.Length == 0 ? 1 : targeting.Max(e => performers.Max(pf => theirs.Count(t => Exchange.Legal(e, t, pf))));
            }
            log.Add($"{c.Name}: {uses} live use(s), value {c.Value}");
            return (c.Id, c.Name, Uses: uses, c.Value);
        }).OrderBy(c => c.Uses).ThenBy(c => c.Value).ThenBy(c => c.Id, StringComparer.Ordinal).Take(Math.Max(0, count)).ToArray();
        return new(scored.Select(c => c.Id).ToArray(), scored.Length == 0 ? "nothing to discard" : $"the fewest live uses first: {string.Join(", ", scored.Select(c => $"{c.Name} ({c.Uses})"))}", log.ToArray());
    }
}
