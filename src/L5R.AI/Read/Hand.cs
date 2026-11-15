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

    /// <summary>A hand card for the board-read discard: its descriptors, its type, the Force it adds when attached (Follower or Item), the Contender's own value for ties.</summary>
    public sealed record HandCard(string Id, string Name, EffectDescriptor[] Effects, bool Attachment, int ForceAdded, double Value);

    /// <summary>Formulation 3 (riBoard; PLAYER_MODEL.md 3.7, Mendon 2026-11-15: "an assessed probability that that card will shift the outcome of a pivotal encounter"). Each card's worth, in Provinces:
    /// defence = how much the chance of losing a Province on their next turn rises without it (the threat read again with the hand's answers less this card, Calibration.PLose x ProvinceWorth);
    /// attack = how much my next attack's expected Provinces fall without it (the opportunity on next turn's board, Questions.Of); development = a Follower's or Item's Force, worth more the quieter
    /// home is (DevelopmentPerForce a point, times the chance home holds). The hand's own Force cuts are applied before a card's targets are counted (a destroy for Force 2 or less is live when
    /// another card can cut a Personality to 2). The least worth goes first; the live count and the Contender's value break ties.</summary>
    /// <param name="attacksToWin">false for a deck whose route is Honor or Dishonor and never destruction (Mendon, 2026-11-15: "This deck never really attacks, so keeping defensive only cards is fine.
    /// It's based on the logic of the deck"): a card's attacking worth is nothing and every Battle card is worth only what it does when they attack.</param>
    public static DiscardPlan DiscardByRead(Read read, IReadOnlyList<HandCard> hand, int count, bool attacksToWin = true)
    {
        var me = read.Ledger.Me; var them = read.Ledger.Them; var log = new List<string>();
        var worth = Questions.ProvinceWorth(me.Provinces.Length);
        double PLose(Read r) { var t = r.ThreatNow(); return Calibration.PLose(t.Cell, t.Gap); }
        double Opportunity(Read r) { var o = Questions.Of(Purchase.NextTurn(r, null)).Opportunity; return o.Attacks ? o.Best.ExpectedProvinces : 0; }
        Read Without(HandCard c)
        {
            var answers = me.HandAnswers.ToList(); var i = answers.FindIndex(a => a.FromHand && a.Source == c.Name);
            if (i >= 0) answers.RemoveAt(i);
            return read with { Ledger = read.Ledger with { Me = me with { HandAnswers = answers.ToArray(), HandCount = Math.Max(0, me.HandCount - 1) } } };
        }
        var pLose = PLose(read); var opp = Opportunity(read);
        // the chance they attack at all next turn: about 1.7 times the chance they take a Province (threat records, blocks 11400-11500: attacked in 50% of turns, a Province lost in 29%)
        var pAttack = Math.Clamp(1.7 * pLose, .05, 1);
        log.Add($"home: lose a Province next turn {pLose:0.00} (worth {worth}); my next attack {opp:0.00} Provinces{(attacksToWin ? "" : "; the deck wins without attacking")}");
        // sequencing: the largest Force cut the hand holds, applied to their units before a card's force-at-most targets are counted
        var cuts = hand.SelectMany(c => c.Effects.Where(e => e.Class == EffectClass.ForceCut && e.Side != TargetSide.Own && e.Magnitude is > 0).Select(e => (c.Id, Cut: e.Magnitude!.Value))).ToArray();
        var theirs = Threat.Straightened(them.Units).ToArray();
        var performers = Threat.Straightened(me.Units).Select(u => (ExchangeUnit?)new ExchangeUnit(u)).Append(null).ToArray();
        int Live(HandCard c)
        {
            var targeting = c.Effects.Where(e => (e.IsBattle || e.IsOpen) && e.Side != TargetSide.Own && e.Target is TargetKind.Personality or TargetKind.Unit or TargetKind.UnitCard).ToArray();
            if (targeting.Length == 0) return 1;
            var cut = cuts.Where(k => k.Id != c.Id).Select(k => k.Cut).DefaultIfEmpty(0).Max();
            var units = theirs.Select(u => new ExchangeUnit(cut > 0 ? u with { Force = Math.Max(0, u.Force - cut) } : u)).ToArray();
            return targeting.Max(e => performers.Max(pf => units.Count(t => Exchange.Legal(e, t, pf))));
        }
        var scored = hand.Select(c =>
        {
            var answers = me.HandAnswers.Any(a => a.FromHand && a.Source == c.Name);
            double defence = 0, attack = 0, development = 0;
            if (answers)
            {
                var without = Without(c);
                defence = Math.Max(0, PLose(without) - pLose) * worth;
                attack = attacksToWin ? Math.Max(0, opp - Opportunity(without)) : 0;
            }
            if (c.Attachment) development = Math.Max(0, c.ForceAdded) * DevelopmentPerForce * (1 - pLose);
            var live = Live(c);
            if (live == 0) { defence = 0; attack = 0; } // nothing it can touch, even after the hand's own cuts
            // a residual worth for what the card could do beyond next turn: its live targets, a card that can only touch attackers counted at the chance they attack at all (Mendon's "situational":
            // a destroy that needs an attacking target is dead against an opponent who does not attack)
            var attackersOnly = c.Effects.Where(e => e.IsBattle && e.Side != TargetSide.Own).ToArray() is { Length: > 0 } battle && battle.All(e => e.Conditions.Any(k => k.Kind == "attacking"));
            var reach = c.Effects.Where(e => e.IsBattle && e.Side != TargetSide.Own).Select(e => e.Targets).DefaultIfEmpty(1).Max(); // a card that stops two attackers (Rocky Terrain's kind) counts twice
            var residual = ResidualPerTarget * Math.Min(live, 3) * reach * (attackersOnly || !attacksToWin ? pAttack : 1); // a deck that never attacks uses every Battle card on defence
            var total = defence + attack + development + residual;
            log.Add($"{c.Name}: defence {defence:0.00}, attack {attack:0.00}, development {development:0.00}, residual {residual:0.00}{(attackersOnly ? " (attackers only)" : "")}, live {live}, value {c.Value}");
            return (c.Id, c.Name, Total: total, Live: live, c.Value);
        }).OrderBy(c => c.Total).ThenBy(c => c.Live).ThenBy(c => c.Value).ThenBy(c => c.Id, StringComparer.Ordinal).Take(Math.Max(0, count)).ToArray();
        return new(scored.Select(c => c.Id).ToArray(), scored.Length == 0 ? "nothing to discard" : $"the least worth on this board first: {string.Join(", ", scored.Select(c => $"{c.Name} ({c.Total:0.00})"))}", log.ToArray());
    }

    /// <summary>Provinces a point of attached Force is worth over the quiet turns ahead: a 3-Force Follower is about a sixth of a Province when home is safe.</summary>
    public const double DevelopmentPerForce = .05;
    /// <summary>Provinces a live target is worth beyond next turn, up to three targets: small beside a card that turns next turn, enough to order the rest.</summary>
    public const double ResidualPerTarget = .02;

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
