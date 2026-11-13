using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

/// <summary>What an ability does to the board, in the classes a player reasons with (docs/bots/CONAN_STRATEGY_BRIEF.md section 4.1).</summary>
public enum EffectClass
{
    Unknown, SendHome, Bow, Destroy, ForceCut, Lock, Duel, Negate, // what neutralises a unit, or an action
    ForceAdd, Straighten, MoveIn, ProvinceStrength, Create, Attach, Recruit, Retreat, // what changes a battle for its user
    HonorTax, GoldTax, Dishonor, HonorGain, Draw, Grant, Keyword, GoldGain, Look, Other // the rest: Grant gives an ability, Keyword a keyword, Look reads or reorders a deck
}

/// <summary>Who an ability may target.</summary>
public enum TargetKind { None, Personality, Follower, UnitCard, Unit, Attachment, Holding, Province, Player, Army, Other }

/// <summary>A restriction on what the effect may touch: Kind is one of the strings in <see cref="EffectDescriptors.ConditionKinds"/>, Value its number (Force 3 or less: 3), Text its word (a keyword, or an "if" clause).</summary>
public sealed record EffectCondition(string Kind, int? Value = null, string? Text = null)
{
    public override string ToString() => Kind + (Value is { } v ? " " + v : "") + (Text is { } t ? " " + t : "");
}

/// <summary>
/// One ability as a card-agnostic descriptor: class, magnitude, who it may target and under what condition, cost, timing, performer, and whether its use is conditional.
/// Read from the ability's printed label, its target category and the structured fields the engine ships (Costs, IssuesDuel, Move, Attack, RequiredKeywords); never from a card's name.
/// <see cref="Secondary"/> lists the other effects in the same label, in printed order. An ability nothing here can read is <see cref="EffectClass.Unknown"/> with <see cref="Why"/> saying so.
/// </summary>
public sealed record EffectDescriptor(
    string Id, string Timing, string? Trigger,
    EffectClass Class, int? Magnitude, bool MagnitudeVaries,
    TargetSide Side, TargetKind Target, EffectCondition[] Conditions, EffectClass[] Secondary,
    string? PerformerKeyword, bool PerformerUnbowed, bool PerformerAtBattlefield,
    bool BowsSource, bool DestroysSource, bool DiscardsCard, int GoldCost, int HonorCost, bool Absent, bool Repeatable, int UsesPerTurn,
    bool Conditional, string? Why)
{
    public bool IsBattle => Timing is "Battle" or "BattleOrOpen";
    public bool IsOpen => Timing is "Open" or "BattleOrOpen";
    public bool IsReaction => Timing == "Reaction";
    /// <summary>Removes an enemy unit from the Force count or the battle: send home, bow, destroy, a Force cut, a lock or a duel it can win.</summary>
    public bool Neutralises => Side != TargetSide.Own && Class is EffectClass.SendHome or EffectClass.Bow or EffectClass.Destroy or EffectClass.ForceCut or EffectClass.Lock or EffectClass.Duel;
    public bool Has(EffectClass c) => Class == c || Secondary.Contains(c);
    public override string ToString() => $"{Class}{(Magnitude is { } m ? " " + m : "")}{(MagnitudeVaries ? " (varies)" : "")} {Side} {Target}{(Conditions.Length > 0 ? " [" + string.Join(", ", Conditions.Select(c => c.ToString())) + "]" : "")}{(Secondary.Length > 0 ? " +" + string.Join("+", Secondary) : "")}{(Conditional ? " if" : "")}";
}

public static class EffectDescriptors
{
    public static readonly string[] ConditionKinds =
    [
        "force-at-most", "force-less-than-performer", "force-less-than-source-unit", "force-at-most-province-strength", "chi-at-most-performer", "honor-at-most-performer", "gold-cost-at-most-performer",
        "without-attachments", "without-followers", "without-keyword", "with-keyword", "bowed", "unbowed", "dishonorable", "honorable", "attacking", "defending", "opposed", "at-home", "fewer-followers-than-performer", "if"
    ];

    private static readonly Dictionary<string, EffectDescriptor> Cache = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static EffectDescriptor Of(string abilityId)
    {
        lock (Gate) { if (Cache.TryGetValue(abilityId, out var cached)) return cached; }
        var d = AbilityCatalog.All.TryGetValue(abilityId, out var ability) ? Of(abilityId, ability) : Unknown(abilityId, "not in the ability catalogue");
        lock (Gate) { Cache[abilityId] = d; }
        return d;
    }

    /// <summary>The descriptors of every ability a card carries: printed, and granted by another card's effect.</summary>
    public static IEnumerable<EffectDescriptor> Of(CardView card)
    {
        foreach (var id in card.Definition?.Abilities ?? []) yield return Of(id);
        foreach (var g in card.GrantedAbilities ?? []) yield return Of(g.AbilityId);
    }

    private static EffectDescriptor Unknown(string id, string why) => new(id, "", null, EffectClass.Unknown, null, false, TargetSide.None, TargetKind.None, [], [], null, false, false, false, false, false, 0, 0, false, false, 1, false, why);

    // ---- the vocabulary: each regex reads one clause of an effect ------------------------------------------------------------------------------------------------
    private const RegexOptions I = RegexOptions.IgnoreCase;
    private static readonly Regex SendHome = new(@"\b(move|moves|send|sends|return|returns)\b[^.;·]*?\bhome\b", I);
    private static readonly Regex Destroy = new(@"(?<!\bnot\s)(?<!\bdo not\s)\bdestroy(?!ed\b|ing\b)(?!\s+(this|itself|your|one of your))", I);
    private static readonly Regex Bow = new(@"(?<!\b(?:it|he|she|they|time it)\s)\bbow(?!\s+(this|your|him unless|it unless|an unbowed Farm|one of your))s?\b(?![^.;·]*\bunless\b)", I);
    private static readonly Regex ForceCut = new(@"(?:[-−]\s?(\d+)\s?(?:F\b|Force)|Force\s+to\s+0|strength\s+penalty|suffers?\s+[-−]\s?(\d+)|loses?\s+(\d+)\s+Force|Force\s+penalty|reduces?\s+(?:his|her|its|their|the target's)\s+Force\s+by)", I);
    private static readonly Regex ForceAdd = new(@"(?:\+\s?(\d+)\s?(?:F\b|Force|total Force)|\+\s?Force|Force\s+bonus|gains?\s+(\d+)\s+Force|\bForce\s*=|\+(\d+)F\s+token)", I);
    private static readonly Regex Negate = new(@"\bnegate", I);
    private static readonly Regex Straighten = new(@"\bstraighten", I);
    private static readonly Regex MoveIn = new(@"\b(move|moves|bring)\b[^.;·]*?\b(to|into|at)\s+(the\s+|this\s+|that\s+)?(current\s+)?battlefield\b|\bmove\b[^.;·]*?\bthere\b", I);
    private static readonly Regex Province = new(@"(?:province|battlefield)[^.;·]*?\bstrength\b|\bstrength\b[^.;·]*?\bprovince\b|\bits strength\b", I);
    private static readonly Regex ProvinceAmount = new(@"([-−+]\s?\d+)\s+strength|strength\s+(?:by|bonus of|penalty of)\s+(\d+)|(?:increase|raise)[^.;·]*?\bby\s+(\d+)|strength\s+to\s+(0)", I);
    private static readonly Regex HonorTax = new(@"(?:controller|he|she|they|player|opponent|who)\s+loses?\s+(?:(\d+)|X|\w+)\s*Honor|\bloses?\s+Honor\s+equal", I);
    private static readonly Regex KeywordGrant = new(@"\b(give|gives|gains?)\b[^.;·]*?\b(Cavalry|Duelist|Tactician|Elite|Naval|Ninja|Kolat|Shadowlands|Scout|Paragon|Samurai|Courtier|Shugenja|Magistrate|Yojimbo|Kensai|Commander|Conqueror|Reconnaissance|Master token|[A-Z][a-z]+ token)\b|\bcop(?:y|ies)\b[^.;·]*\bkeyword|\bcopies\s+(Commander|Duelist|Kensai)|\bremove\s+[A-Z][a-z]+(\s+and\s+[A-Z][a-z]+)?\s+from\b", I);
    private static readonly Regex GoldGain = new(@"\bproduce\s+(\d+)\s+Gold\b|\bGold-producing\b", I);
    private static readonly Regex LookDeck = new(@"\blook at\b|\brefresh\b|\bsearch\b[^.;·]*\bdeck\b|\bname\s+\\""|\bturn\b[^.;·]*\bface-?down\b", I);
    private static readonly Regex OtherKnown = new(@"\bfocus effect\b|\badditional\s+(battle\s+)?action|\bfirst opportunity\b|\bReconnaissance\b|\btargets another of your cards instead\b|\bchoose a player\b|\bcopies?\s+one of its\b|\bput\b[^.;·]*\binto your hand\b|\bremove\b[^.;·]*\bfrom the game\b|\bstrength bonus\b|\bOffer bowing\b|\bdelay the effects\b|\bmay assign even if bowed\b|\bA lesson is taught\b", I);
    private static readonly Regex HonorGain = new(@"\bgains?\s+(\d+|X)\s+Honor", I);
    private static readonly Regex Dishonor = new(@"\bdishonor\b(?!able)", I);
    private static readonly Regex Lock = new(@"may not (perform|take|use|be used|straighten|assign|move)|cannot (straighten|move|perform|be assigned)|abilities[^.;·]*?may not be used", I);
    private static readonly Regex Draw = new(@"\bdraws?\b", I);
    private static readonly Regex Create = new(@"\bcreates?\b", I);
    private static readonly Regex Attach = new(@"\battach(?!ments?\b)", I);
    private static readonly Regex Recruit = new(@"\b(recruit|bring\b[^.;·]*\binto play|put\b[^.;·]*\binto play)", I);
    private static readonly Regex GoldTax = new(@"\bgold\b[^.;·]*\b(loses?|discard|pays?)|\bbow\b[^.;·]*\b(holding|gold)", I);
    private static readonly Regex Grant = new(@"\bgive\b[^.;·]*\b(the|an?)\s+(ability|action)\b|\bgains?\s+(the|an?)\s+(ability|action)\b|\\\""", I);
    private static readonly Regex Varies = new(@"\bequal to\b|\bequal\s+\w+\s+Force|= its|\bX\b|number of|for each", I);
    private static readonly Regex AttackX = new(@"(?:Ranged|Melee)\s+(?:Attack\s+)?(\d+)|(\d+)\s+(?:Ranged|Melee)|Attack\s+(\d+)", I);
    private static readonly Regex AttackWord = new(@"\b(Ranged|Melee)\b[^.;·]*\bAttack\b", I);
    private static readonly Regex IfClause = new(@"^\s*(if|unless|while|when)\b[^,:]*[,:]?", I);
    private static readonly Regex BowCost = new(@"\bbow\s+(this|your performing|him\b|her\b|it\b|[A-Z][a-z]+\b(?! unless))", RegexOptions.None);
    private static readonly Regex DestroyCost = new(@"\bdestroy\s+(this|your performing|one of your)", I);
    private static readonly Regex DiscardCost = new(@"\bdiscard\s+(a|one|two|three|\d+|the top|this card|it from your hand)\b", I);
    private static readonly Regex PayGold = new(@"\bpay\s+(\d+)\s+Gold", I);
    private static readonly Regex Unbowed = new(@"\bunbowed\b", I);
    private static readonly Regex AtBattle = new(@"at (the|this) (current )?battlefield", I);
    private static readonly Regex PerformerClause = new(@"\bperforming\s+(?:unbowed\s+)?(?:opposed\s+)?([A-Z][A-Za-z]+)", RegexOptions.None);
    // target-clause conditions, read from the text before the effect
    private static readonly Regex ForceAtMost = new(@"\bForce\s+(\d+)\s+or\s+(?:less|lower)|\b(\d+)\s+or\s+(?:less|lower)\s+Force", I);
    private static readonly Regex LowerForce = new(@"\b(lower|less)\s+Force\b|\bForce\s+lower\s+than\b", I);
    private static readonly Regex NoAttachments = new(@"\bwithout\s+attachments?\b|\bno\s+attachments?\b", I);
    private static readonly Regex NoFollowers = new(@"\bwithout\s+(a\s+)?followers?\b", I);
    private static readonly Regex Without = new(@"\bwithout\s+(an?\s+)?(Armor|Weapon|Spell|Item)s?\b", I);
    private static readonly Regex With = new(@"\bwith\s+(an?\s+)?(Armor|Weapon|Spell|Item|Follower|attachment|token)s?\b", I);
    private static readonly Regex Bowed = new(@"\bbowed\b", I);
    private static readonly Regex Dishonorable = new(@"\bdishonorable\b", I);

    public static EffectDescriptor Of(string id, AbilityDefinition a)
    {
        var label = a.Label ?? "";
        var effect = AbilityIntent.EffectText(label); var cost = CostText(label);
        var category = a.Target ?? "none";
        var (side, target, conditions) = ReadCategory(category, a);
        if (category is "none" or "hand" or "slots") foreach (var c in TextConditions(cost)) conditions.Add(c); // a Slots ability names its targets in the text
        if (category == "slots") { if (Regex.IsMatch(cost, @"target (an? |one |two |three |up to \w+ )?enemy\b", I)) side = TargetSide.Enemy; else if (Regex.IsMatch(cost, @"target (your|one of your)\b", I)) side = TargetSide.Own; if (Regex.IsMatch(cost, @"\bPersonality\b", I)) target = TargetKind.Personality; else if (Regex.IsMatch(cost, @"\battachments?\b", I)) target = TargetKind.Attachment; }
        else foreach (var c in TextConditions(cost).Where(c => conditions.All(x => x.Kind != c.Kind))) conditions.Add(c);
        var performer = PerformerClause.Match(label);
        var classes = new List<(EffectClass Class, int Pos, int? Magnitude, bool Varies)>();
        void Add(EffectClass c, int pos, int? m = null, bool v = false) { if (classes.All(x => x.Class != c)) classes.Add((c, pos, m, v)); }

        // the effect's own clauses, in printed order
        if (a.IssuesDuel) Add(EffectClass.Duel, 0);
        if (category == "attack" || a.Attack is not null || AttackWord.IsMatch(effect))
        {
            var m = AttackX.Match(effect); int? x = null;
            if (m.Success) foreach (var g in m.Groups.Cast<Group>().Skip(1)) if (g.Success) { x = int.Parse(g.Value); break; }
            var pos = AttackWord.Match(effect) is { Success: true } w ? w.Index : effect.Length;
            Add(EffectClass.Destroy, pos, x, x is null);
            if (side == TargetSide.None) { side = TargetSide.Enemy; target = TargetKind.UnitCard; }
            if (x is { } reach) conditions.Add(new("force-at-most", reach));
            if (!(a.Attack?.TargetScope.ToString().Contains("Follower", StringComparison.OrdinalIgnoreCase) ?? false) && !effect.Contains("attached Followers", StringComparison.OrdinalIgnoreCase)) conditions.Add(new("without-followers"));
        }
        if (SendHome.Match(effect) is { Success: true } sh)
            Add(side == TargetSide.Own || side == TargetSide.None && (target == TargetKind.None || Regex.IsMatch(sh.Value, @"\b(this|your|his unit|him)\b", I) && !Regex.IsMatch(cost, @"\btarget\b", I)) || Regex.IsMatch(sh.Value, @"\b(this Personality|this card|your performing)\b", I) ? EffectClass.Retreat : EffectClass.SendHome, sh.Index);
        if (Destroy.Match(effect) is { Success: true } de && side != TargetSide.Own && !(side == TargetSide.None && Regex.IsMatch(effect[de.Index..], @"^destroy\s+(his|her|its)\s+unit", I))) Add(EffectClass.Destroy, de.Index);
        if (Bow.Match(effect) is { Success: true } bo && side != TargetSide.Own && !(side == TargetSide.None && BowCost.IsMatch(effect))) Add(EffectClass.Bow, bo.Index);
        if (ForceCut.Match(effect) is { Success: true } fc)
        {
            int? n = null; foreach (var g in fc.Groups.Cast<Group>().Skip(1)) if (g.Success) { n = int.Parse(g.Value); break; }
            if (fc.Value.Contains("to 0", StringComparison.OrdinalIgnoreCase)) n = 99;
            if (side == TargetSide.Own || target == TargetKind.Province) Add(EffectClass.ProvinceStrength, fc.Index, n is { } k ? -k : null, n is null); else Add(EffectClass.ForceCut, fc.Index, n, n is null);
        }
        if (Lock.Match(effect) is { Success: true } lk && side != TargetSide.Own && !Regex.IsMatch(Sentence(effect, lk.Index), @"\bnegate", I)) Add(EffectClass.Lock, lk.Index);
        if (Negate.Match(effect) is { Success: true } ng) Add(EffectClass.Negate, ng.Index);
        if (ForceAdd.Match(effect) is { Success: true } fa && target != TargetKind.Province)
        {
            int? n = null; foreach (var g in fa.Groups.Cast<Group>().Skip(1)) if (g.Success) { n = int.Parse(g.Value); break; }
            Add(EffectClass.ForceAdd, fa.Index, n, n is null);
            if (side == TargetSide.None) side = TargetSide.Own;
        }
        if (Province.Match(effect) is { Success: true } pv)
        {
            var am = ProvinceAmount.Match(effect); int? n = null;
            if (am.Success) foreach (var g in am.Groups.Cast<Group>().Skip(1)) if (g.Success) { n = int.Parse(g.Value.Replace("−", "-").Replace(" ", "")); break; }
            if (n is { } k && (effect.Contains("penalty", StringComparison.OrdinalIgnoreCase) || effect.Contains("lower", StringComparison.OrdinalIgnoreCase) || effect.Contains("reduce", StringComparison.OrdinalIgnoreCase)) && k > 0) n = -k;
            Add(EffectClass.ProvinceStrength, pv.Index, n, n is null);
        }
        if (Straighten.Match(effect) is { Success: true } st && side != TargetSide.Enemy) Add(EffectClass.Straighten, st.Index);
        var mi = MoveIn.Match(effect); if (mi.Success || a.MovesToBattle) Add(EffectClass.MoveIn, mi.Success ? mi.Index : 0);
        if (Dishonor.Match(effect) is { Success: true } dh && side != TargetSide.Own) Add(EffectClass.Dishonor, dh.Index);
        if (HonorTax.Match(effect) is { Success: true } ht) { int? n = ht.Groups[1].Success ? int.Parse(ht.Groups[1].Value) : null; Add(EffectClass.HonorTax, ht.Index, n, n is null); }
        if (HonorGain.Match(effect) is { Success: true } hg) { int? n = int.TryParse(hg.Groups[1].Value, out var v) ? v : null; Add(EffectClass.HonorGain, hg.Index, n, n is null); }
        if (GoldTax.Match(effect) is { Success: true } gt && side == TargetSide.Enemy) Add(EffectClass.GoldTax, gt.Index);
        if (Grant.Match(effect) is { Success: true } gr) Add(EffectClass.Grant, gr.Index);
        var cr = Create.Match(effect); if (cr.Success || a.Creates is not null) Add(EffectClass.Create, cr.Success ? cr.Index : 0, a.Creates?.Force);
        var at = Attach.Match(effect); if (at.Success || a.Attach is not null) Add(EffectClass.Attach, at.Success ? at.Index : 0);
        if (Recruit.Match(effect) is { Success: true } rc) Add(EffectClass.Recruit, rc.Index);
        if (Draw.Match(effect) is { Success: true } dr) Add(EffectClass.Draw, dr.Index);
        if (KeywordGrant.Match(effect) is { Success: true } kg) Add(EffectClass.Keyword, kg.Index);
        if (GoldGain.Match(effect) is { Success: true } gg) { int? n = gg.Groups[1].Success ? int.Parse(gg.Groups[1].Value) : null; Add(EffectClass.GoldGain, gg.Index, n, n is null); }
        if (LookDeck.Match(effect) is { Success: true } ld) Add(EffectClass.Look, ld.Index);
        if (OtherKnown.Match(effect) is { Success: true } ok) Add(EffectClass.Other, ok.Index);
        if (id is "lobby" or "favor-cycle" || label.Contains("Favor", StringComparison.Ordinal) && classes.Count == 0) Add(EffectClass.Other, 0);

        // the primary class: the first neutraliser in printed order, else the first clause
        var ordered = classes.OrderBy(c => c.Pos).ToArray();
        var primary = ordered.FirstOrDefault(c => c.Class is EffectClass.SendHome or EffectClass.Bow or EffectClass.Destroy or EffectClass.ForceCut or EffectClass.Lock or EffectClass.Duel);
        if (primary.Class == EffectClass.Unknown) primary = ordered.FirstOrDefault();
        var secondary = ordered.Where(c => c.Class != primary.Class).Select(c => c.Class).ToArray();
        var conditional = IfClause.IsMatch(effect) || primary.Pos > 0 && Regex.IsMatch(effect[..primary.Pos], @"\b(if|unless)\b", I);
        if (conditional) conditions.Add(new("if", Text: Regex.Match(effect, @"\b(if|unless)\b[^,:.]*", I).Value.Trim()));

        var bowsSource = a.Costs?.Any(c => c.Verb == CostVerb.Bow) == true || BowCost.IsMatch(cost) || a.CostOptions?.Any(o => o.Verb == CostVerb.Bow) == true;
        var destroysSource = a.Costs?.Any(c => c.Verb == CostVerb.Destroy) == true || DestroyCost.IsMatch(cost) || a.CostOptions?.Any(o => o.Verb == CostVerb.Destroy) == true;
        var discards = a.Costs?.Any(c => c.Verb == CostVerbs.Discard) == true || a.CostOptions?.Any(o => o.Verb == CostVerbs.Discard) == true || DiscardCost.IsMatch(cost);
        var gold = a.Cost is { Explicit: true, Amount: > 0 } gc ? gc.Amount : PayGold.Match(cost) is { Success: true } pg ? int.Parse(pg.Groups[1].Value) : 0;
        var honor = AbilityIntent.SelfLoss(label).Amount;
        var unknown = primary.Class == EffectClass.Unknown;
        return new(id, a.Timing, a.Trigger, unknown ? EffectClass.Unknown : primary.Class, primary.Magnitude, primary.Varies || (primary.Magnitude is null && Varies.IsMatch(effect) && primary.Class is EffectClass.ForceAdd or EffectClass.ForceCut or EffectClass.Destroy),
            side, target, conditions.DistinctBy(c => c.ToString()).ToArray(), secondary,
            performer.Success ? performer.Groups[1].Value : a.RequiredKeywords?.FirstOrDefault(), Unbowed.IsMatch(cost) || (a.Performer?.Contains("unbowed") ?? false), AtBattle.IsMatch(cost) || (a.Performer?.Contains("at-battle") ?? false),
            bowsSource, destroysSource, discards, gold, honor, a.Absent, a.UsesPerTurn == AbilityUseLimit.Unlimited || a.UsesPerTurn > 1, a.UsesPerTurn, conditional, unknown ? "no clause matched: " + effect.Trim() : null);
    }

    private static string Sentence(string text, int at) { var start = text.LastIndexOf('.', Math.Max(0, at - 1)) + 1; var end = text.IndexOf('.', at); return text[start..(end < 0 ? text.Length : end)]; }

    private static string CostText(string label) { var i = label.LastIndexOf(':'); return i > 0 ? label[..i] : ""; }

    /// <summary>Side, kind and conditions from the engine's target category string ("enemy-card-without-attachments", "enemy-force-lt-self").</summary>
    private static (TargetSide Side, TargetKind Kind, List<EffectCondition> Conditions) ReadCategory(string category, AbilityDefinition a)
    {
        var c = category.Replace("rem-a-", "").Replace("remc-", "").Replace("rr2-", "").Replace("rd-", "").Replace("se2-", "");
        var tokens = c.Split('-');
        var side = c.StartsWith("enemy") || c.StartsWith("attacking") || c.StartsWith("defending") || c.Contains("enemy-") ? TargetSide.Enemy
            : c.StartsWith("own") || c.StartsWith("source") ? TargetSide.Own : c == "player" ? TargetSide.Player : c is "none" or "hand" or "attack" or "lobby" or "provinces" ? TargetSide.None : TargetSide.Any;
        var k0 = Regex.Replace(c, @"-?(without|with)-(attachments?|followers?|armor|weapons?|weapon-or-armor|unbowed-attachments|keyword|printed-keyword|token|attachment)|-in-(any-)?(discard|province|hand|army)|-at-(home|battle|any-location|current-battlefield|source-location)|-(force|chi|honor|gold-cost)-[a-z-]+", "");
        var kind = k0.Contains("-or-") && !(k0.Contains("personality-or-follower") || k0.Contains("follower-or-personality")) && !k0.Contains("holding") && !k0.Contains("region") ? TargetKind.UnitCard : k0.Contains("region") || k0.Contains("terrain") || k0.Contains("celestial") || k0.Contains("event") ? TargetKind.Other : k0.Contains("province") ? TargetKind.Province : c == "player" ? TargetKind.Player : c.Contains("holding") || c.Contains("stronghold") ? TargetKind.Holding
            : k0.Contains("attachment") || k0.Contains("weapon") || k0.Contains("spell") || k0.Contains("ring") ? TargetKind.Attachment
            : k0.Contains("personality") && !k0.Contains("or-follower") ? TargetKind.Personality : k0.Contains("follower") && !k0.Contains("personality") && !k0.Contains("fewer") ? TargetKind.Follower
            : k0.Contains("unit") && !k0.Contains("unit-card") ? TargetKind.Unit : k0.Contains("card") || k0.Contains("force") || k0.Contains("or-follower") || k0.Contains("attacking") || k0.Contains("defending") || k0.Contains("opposed") ? TargetKind.UnitCard
            : k0.Contains("army") || k0.Contains("personalities") ? TargetKind.Army : k0 is "none" or "hand" or "attack" or "lobby" ? TargetKind.None : TargetKind.UnitCard;
        var conditions = new List<EffectCondition>();
        if (c.Contains("without-attachments") || a.RequireNoAttachments) conditions.Add(new("without-attachments"));
        if (c.Contains("without-followers") || c.Contains("unattached")) conditions.Add(new("without-followers"));
        if (c.Contains("force-lt") || c.Contains("force-lower") || c.Contains("lower-force")) conditions.Add(new(c.Contains("unit") || c.Contains("source-unit") ? "force-less-than-source-unit" : "force-less-than-performer"));
        if (c.Contains("force-lte-province")) conditions.Add(new("force-at-most-province-strength"));
        if (c.Contains("chi-lt")) conditions.Add(new("chi-at-most-performer"));
        if (c.Contains("honor-lt") || c.Contains("lower-honor")) conditions.Add(new("honor-at-most-performer"));
        if (c.Contains("gold-cost-lte")) conditions.Add(new("gold-cost-at-most-performer"));
        if (c.Contains("fewer-or-equal-followers")) conditions.Add(new("fewer-followers-than-performer"));
        if (c.Contains("without-armor")) conditions.Add(new("without-keyword", Text: "Armor"));
        if (c.Contains("without-weapon")) conditions.Add(new("without-keyword", Text: "Weapon"));
        if (c.Contains("with-keyword") || c.Contains("printed-keyword")) conditions.Add(new("with-keyword", Text: a.RequiredKeywords is { Length: > 0 } k ? string.Join("/", k) : "?"));
        if (tokens.Contains("bowed")) conditions.Add(new("bowed"));
        if (tokens.Contains("unbowed")) conditions.Add(new("unbowed"));
        if (c.Contains("dishonorable") && !c.Contains("optional")) conditions.Add(new("dishonorable"));
        if (c.Contains("honorable") && !c.Contains("dishonorable")) conditions.Add(new("honorable"));
        if (c.Contains("attacking")) conditions.Add(new("attacking"));
        if (c.Contains("defending")) conditions.Add(new("defending"));
        if (c.Contains("opposed")) conditions.Add(new("opposed"));
        if (c.Contains("at-home")) conditions.Add(new("at-home"));
        if (a.TargetMustBe is { } must) { if (must.Dishonorable) conditions.Add(new("dishonorable")); if (must.Honorable) conditions.Add(new("honorable")); if (must.Keywords is { Length: > 0 } kw) conditions.Add(new("with-keyword", Text: string.Join("/", kw))); }
        return (side, kind, conditions);
    }

    /// <summary>Conditions written in the target clause of the label ("target an enemy card with Force 3 or less and no attachments").</summary>
    private static IEnumerable<EffectCondition> TextConditions(string clause)
    {
        if (ForceAtMost.Match(clause) is { Success: true } fm) yield return new("force-at-most", int.Parse(fm.Groups[1].Success ? fm.Groups[1].Value : fm.Groups[2].Value));
        else if (LowerForce.IsMatch(clause)) yield return new("force-less-than-performer");
        if (NoAttachments.IsMatch(clause)) yield return new("without-attachments");
        if (NoFollowers.IsMatch(clause)) yield return new("without-followers");
        if (Without.Match(clause) is { Success: true } wo) yield return new("without-keyword", Text: wo.Groups[2].Value);
        if (With.Match(clause) is { Success: true } wi && wi.Groups[2].Value is not ("attachment" or "token")) yield return new("with-keyword", Text: wi.Groups[2].Value);
        if (Bowed.IsMatch(clause) && !Unbowed.IsMatch(clause) && clause.Contains("target", StringComparison.OrdinalIgnoreCase)) yield return new("bowed");
        if (Dishonorable.IsMatch(clause) && clause.Contains("target", StringComparison.OrdinalIgnoreCase)) yield return new("dishonorable");
    }
}
