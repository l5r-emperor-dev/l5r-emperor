namespace L5R.Core;

// Generalized Gold payment (issue #303). ONE place decides what an action costs in Gold.
//
// Before this file, an ability's cost was written down four times by hand (ValidateAbility's cost chain,
// AbilityActions' parallel per-candidate chain, each ability's own Pay(...) call in the execution switch,
// and the client re-deriving it for its panel) and they had drifted apart: an option could be offered with
// sources that ValidateAbility then rejected (Kharmic, the Invest cards), Noritada's Invest and a non-
// Strategy Discipline validated a payment and then never charged it, and only recruit/equip/Strategy ever
// got a payment panel. Now every ability carries a GoldCostRule (AbilityDefinition.Cost), this file
// interprets it into one AbilityCost, and validation, option generation, the GoldQuote shipped to the
// client and the payment itself all read that same AbilityCost. Purchase, recruitReserve, equip and
// playStrategy reuse the same GoldQuote shape so the client has exactly one payment panel.

/// <summary>How a Gold cost is computed, on the wire as GoldQuote.Kind.</summary>
public static class GoldCostKind
{
    /// <summary>Printed on the card or fixed in the ability text: the same whichever target is chosen.</summary>
    public const string Fixed = "fixed";
    /// <summary>Computed from the chosen target (another card's cost, a unit's grouped total, a recruit
    /// quote), plus a modifier and a floor. Each legal target has its own quote.</summary>
    public const string Derived = "derived";
    /// <summary>The player chooses the amount within MinAmount..MaxAmount (GameCommand.GoldAmount).</summary>
    public const string Variable = "variable";
}

/// <summary>What a rule's amount is computed from.</summary>
public static class GoldCostBasis
{
    /// <summary>A constant written into the ability (Kharmic 2, Invest 3/5, "Pay 2 Gold: Draw a card").</summary>
    public const string Fixed = "fixed";
    /// <summary>The ability's own source card's printed Gold Cost (a Strategy, a Discipline, Singh Remnants).</summary>
    public const string SourcePrinted = "source-printed";
    /// <summary>The targeted card's own printed Gold Cost (Equip (Battle), Soshi Neiru, Low Market).</summary>
    public const string TargetPrinted = "target-printed";
    /// <summary>The targeted card's own CURRENT Gold Cost alone (its printed cost and every Gold cost modifier and token), whatever is attached to it:
    /// Disgraceful Conduct's "the Personality" mode.</summary>
    public const string TargetCurrent = "target-current";
    /// <summary>The targeted card's grouped total: a Personality plus everything attached to it (its unit),
    /// or a single card's own cost when the target is not a Personality (Disgraceful Conduct, A Stain
    /// Cleansed).</summary>
    public const string TargetUnitTotal = "target-unit-total";
    /// <summary>The recruit quote of a face-up province Personality, or of one of the player's own dead Personalities (At Any Cost), so every
    /// recruit discount, surcharge and restriction QuotePurchase knows about applies (Your Clan Needs You!, Shinden Shorai).</summary>
    public const string TargetRecruit = "target-recruit";
    /// <summary>Chosen by the player between Amount (the minimum) and MaxAmount.</summary>
    public const string Variable = "variable";
    /// <summary>What Equip would charge to attach the ability's attachment (its target, or the source itself -- AttachSpec.Card) to the ability's
    /// wielder: the printed cost less every discount that applies to this attachment, this wielder and this player (EquipGoldCost), then Modifier
    /// and Floor. "Paying all costs" is Modifier 0; "paying 1 less Gold" is Modifier -1 (Moto Jun-Ni, Foothills Keep, Fading Shadows).</summary>
    public const string AttachCost = "attach-cost";
    /// <summary>"Pay Gold equal to [base | current] [stat] of [target | performer | source] +/- [z]" (issue #416): any printed number of a card, converted into Gold and paid through the
    /// ordinary Gold pathway. Which card, which stat and whether the printed (base) or the current value is read are on the rule (GoldCostRule.StatOf, Stat, StatBase).</summary>
    public const string Stat = "stat";
    /// <summary>"Pay Gold equal to the number of Personalities you control" (Necessary Evil), plus Modifier and raised to Floor ("or 4, whichever is higher").</summary>
    public const string OwnPersonalities = "own-personalities";
}

/// <summary>Whose stat a <see cref="GoldCostBasis.Stat"/> rule reads.</summary>
public static class CostStatSubject
{
    public const string Target = "target";
    public const string Performer = "performer";
    public const string Source = "source";
    /// <summary>Footsteps of Madness (issue #492): the Stronghold the action held in the Unimpeachable Name window is from.</summary>
    public const string HeldActionSource = EmperorRemainingAAbilities.HeldActionSource;
}

/// <summary>The printed numbers a Gold cost can be made of. "Strength" on a Stronghold is its Province Strength; a unit's is its Force.</summary>
public static class CostStat
{
    public const string Force = "Force";
    public const string Chi = "Chi";
    public const string GoldCost = "GoldCost";
    public const string PersonalHonor = "PersonalHonor";
    public const string Focus = "Focus";
    public const string ProvinceStrength = "ProvinceStrength";
    /// <summary>A Stronghold's printed Gold Production (Footsteps of Madness, issue #492).</summary>
    public const string GoldProduction = "GoldProduction";
}

/// <summary>Data describing how one ability is priced. Interpreted only by MatchEngine.AbilityGoldCost.
/// amount = basis value + Modifier, raised to Floor when one is given, never below 0.</summary>
/// <param name="Amount">The constant (Fixed basis) or the minimum (Variable basis).</param>
/// <param name="AtResolution">True for a payment that is part of the action's RESOLUTION, not a cost paid when it is announced (Virtuous Charity: "you, then
/// the player, each pay zero or more Gold"). It is validated and quoted like any cost, but paid by the action's own effect, so an action that is negated whole
/// pays nothing; PayForAbility skips it.</param>
/// <param name="Explicit">False only for the rules GoldQuotes synthesizes for abilities that carry no rule
/// (a Strategy source paying its own printed cost): those charge nothing, and emit no payment event, when
/// that cost is 0.</param>
public sealed record GoldCostRule(string Basis, int Amount = 0, int Modifier = 0, int? Floor = null,
    int? MaxAmount = null, string? HonorWaiverClan = null, bool Explicit = true, bool AtResolution = false, bool MinusPerformerCost = false,
    string? StatOf = null, string? Stat = null, bool StatBase = false,
    // Black Pearl (Forgotten Legacy pass, issue #467): a recruit from ANY player's discard pile (AnyOwnerPile), "paying N less Gold if the Personality is
    // [keyword]" (DiscountKeyword/KeywordDiscount).
    bool AnyOwnerPile = false, string? DiscountKeyword = null, int KeywordDiscount = 0)
{
    public static GoldCostRule Fixed(int amount) => new(GoldCostBasis.Fixed, amount);
    public static GoldCostRule SourcePrinted() => new(GoldCostBasis.SourcePrinted);
    public static GoldCostRule TargetPrinted(int modifier = 0, int? floor = null) => new(GoldCostBasis.TargetPrinted, Modifier: modifier, Floor: floor);
    public static GoldCostRule TargetUnitTotal(int modifier = 0, int? floor = null) => new(GoldCostBasis.TargetUnitTotal, Modifier: modifier, Floor: floor);
    /// <summary>minusPerformerCost: "reducing his Gold cost to enter play by the performer's Gold Cost" (Goju Plot) -- the performer's current Gold Cost comes off the recruit cost, to no less than 0.</summary>
    public static GoldCostRule TargetRecruit(int modifier = 0, int? floor = null, string? honorWaiverClan = null, bool minusPerformerCost = false,
        bool anyOwnerPile = false, string? discountKeyword = null, int keywordDiscount = 0) =>
        new(GoldCostBasis.TargetRecruit, Modifier: modifier, Floor: floor, HonorWaiverClan: honorWaiverClan, MinusPerformerCost: minusPerformerCost,
            AnyOwnerPile: anyOwnerPile, DiscountKeyword: discountKeyword, KeywordDiscount: keywordDiscount);
    public static GoldCostRule TargetCurrent(int modifier = 0, int? floor = null) => new(GoldCostBasis.TargetCurrent, Modifier: modifier, Floor: floor);
    public static GoldCostRule Variable(int min = 0, int? max = null, bool atResolution = false) => new(GoldCostBasis.Variable, Amount: min, MaxAmount: max, AtResolution: atResolution);
    public static GoldCostRule AttachCost(int modifier = 0, int? floor = null) => new(GoldCostBasis.AttachCost, Modifier: modifier, Floor: floor);
    /// <summary>Gold equal to a printed number of a card, plus or minus a modifier (A Quiet Transaction: the target's BASE Personal Honor plus one). <paramref name="of"/> is a
    /// CostStatSubject, <paramref name="stat"/> a CostStat; <paramref name="basePrinted"/> reads the printed value, otherwise the current one (every modifier and token).</summary>
    public static GoldCostRule OwnPersonalityCount(int modifier = 0, int? floor = null) => new(GoldCostBasis.OwnPersonalities, Modifier: modifier, Floor: floor);
    public static GoldCostRule StatValue(string of, string stat, bool basePrinted, int modifier = 0, int? floor = null) =>
        new(GoldCostBasis.Stat, Modifier: modifier, Floor: floor, StatOf: of, Stat: stat, StatBase: basePrinted);

    public string Kind => Basis switch
    {
        GoldCostBasis.Variable => GoldCostKind.Variable,
        GoldCostBasis.TargetPrinted or GoldCostBasis.TargetCurrent or GoldCostBasis.TargetUnitTotal or GoldCostBasis.TargetRecruit or GoldCostBasis.AttachCost or GoldCostBasis.Stat => GoldCostKind.Derived,
        _ => GoldCostKind.Fixed
    };
}

public sealed partial class MatchEngine
{
    /// <summary>The most alternatives shipped per amount of a variable quote (a fixed or derived quote
    /// carries the ordinary PaymentOptions limit); bounds the wire size of "pay zero or more".</summary>
    private const int VariablePaymentOptionsPerAmount = 8;

    // Alternate Gold source (issue #338, Yoritomo Sakuma: "Reaction: When paying a Gold Cost, bow Sakuma: Produce 2 Gold."). The general
    // shape is "bow this card as a payment source", chosen INSIDE the payment like any Holding: the ability carries PaymentProduction, the
    // card counts as a Gold producer for every payment (the quote's suggestions, ValidatePayment, Pay, the HUD's total) while it is in play
    // and unbowed and its use is unspent this turn, and choosing it as a source is what bows it and spends the use. It is a Reaction
    // ("once a turn" like every other), never an action of its own. A source that also names a target to sacrifice (Zlkyt's "destroy your
    // target Mine or Farm: Produce Gold equal to its base Gold Cost") is a further extension this does not model.
    private (string AbilityId, int Amount)? AlternateGoldSource(CardInstance card, PaymentSubject? paying = null)
    {
        foreach (var id in AbilityIdsOf(card))
            if (AbilityCatalog.All.TryGetValue(id, out var ability) && (ability.PaymentProduction is not null || ability.PaymentDiscardsFocus) && !BarredByCherryBlossom(id) /* Cherry Blossom Festival (R4): a Gold-source Reaction is a Reaction */ && ability.PaymentWhen is null // a conditional reactive source is ReactiveGoldSource's (Library of Rebirth)
                && (!ability.PaymentActionsOnly || PaymentIsForAnAction(paying)) // Yasuki Tanimura (issue #477 batch 6): "When paying for an action" (Mendon's R4 ruling on recruits by an ability: PaymentIsForAnAction).
                && !(card.AbilityUses.TryGetValue(ability.UsageKey ?? id, out var used) && used == _state.TurnNumber)
                && !AbilityDisabled(card, id)) // Forgotten Legacy pass (issue #467), batch 1: "it may not be used"
                // Asahina Keigo (Emperor Edition Personality pass, issue #477): the Focus Value of the hand card named for him (0 until one is).
                return (id, ability.PaymentDiscardsFocus ? PaymentDiscardFocus(card) : ability.PaymentProduction!.Value);
        return null;
    }

    /// <summary>What a card can produce for a payment right now, for the client (CardView.GoldProduced): its printed production, or the
    /// amount of its alternate source. Null when it produces nothing or is not somewhere it could.</summary>
    private int? GoldSourceProduction(CardInstance card)
    {
        if (!ProducerZone(card, Definition(card))) return null; // issue #695: also an Attached card and an in-play Strategy
        if (Definition(card).GoldProduction > 0 || Definition(card).Effect == MoneylenderEffect) return GoldProduced(card.Id, LentBorrower(card) ?? card.OwnerId); // a lent Holding is priced for its borrower
        return IsVariableGoldSource(card) ? null : AlternateGoldSource(card)?.Amount;
    }

    /// <summary>The resolved price of one ability command. Amount is what must be paid now; Subject is the
    /// card named in the payment event; NeedsTarget means a target-derived rule has no target yet.</summary>
    /// <param name="LeadAmount">Discipline (issue #353): the part of Amount that is the Discipline cost, which is paid FIRST as its own payment (0 when
    /// the price is a single payment). Amount is always the total, so every existing reader of it still sees what the whole action costs.</param>
    private sealed record AbilityCost(GoldCostRule Rule, int Amount, string Basis, string? Error, bool NeedsTarget,
        CardInstance? Subject, int? Min = null, int? Max = null, int LeadAmount = 0);

    /// <summary>The rule for an ability that declares none. A Discipline ability (issue #331) has two prices
    /// decided by where its card is: played from a discard pile it pays the Discipline cost -- the card's
    /// published GoldCost, which for a Discipline Strategy holds the Discipline icon's amount because such a card
    /// prints no Gold Cost of its own -- and played from hand it is the ordinary Strategy play, free of it (never
    /// the Discipline cost). Otherwise a Strategy source pays its own printed cost. Everything else is free.
    /// A Discipline card whose action ALSO has its own Gold cost (an explicit <c>Cost</c> on the ability: Knowledge and Power, Two Steady Breaths --
    /// "Limited, 1 Gold") pays that too: from hand only the action's own cost, from a discard pile the Discipline cost PLUS the action's own, the
    /// Discipline cost first (Mendon's ruling, issue #353) -- see AbilityGoldCost.</summary>
    private static GoldCostRule? ImplicitGoldRule(AbilityDefinition ability, CardInstance? source)
    {
        if (source is null) return null;
        if (ability.Discipline) return DisciplineFromDiscard(ability, source) ? GoldCostRule.SourcePrinted() : null;
        if (Definition(source).Type == CardType.Strategy) return GoldCostRule.SourcePrinted() with { Explicit = false };
        return null;
    }

    private int UnitTotalGoldCost(CardInstance card, out string breakdown)
    {
        var parts = new List<int> { CardGoldCost(card) };
        if (Definition(card).Type == CardType.Personality)
            parts.AddRange(Attachments(card.Id).OrderBy(a => a.Id, StringComparer.Ordinal).Select(a => CardGoldCost(a)));
        var total = parts.Sum();
        breakdown = parts.Count > 1 ? $"{string.Join(" + ", parts)} = {total}" : total.ToString();
        return total;
    }

    private static string StatLabel(string stat) => stat switch
    {
        CostStat.GoldCost => "Gold Cost",
        CostStat.PersonalHonor => "Personal Honor",
        CostStat.ProvinceStrength => "Province Strength",
        _ => stat
    };

    /// <summary>A printed number of a card (issue #416): the printed value, or the current one with every modifier and token, of Force, Chi, Gold Cost, Personal Honor, Focus Value
    /// or (on a Stronghold) Province Strength. A stat a card does not have reads 0.</summary>
    internal int CostStatValue(CardInstance card, string stat, bool basePrinted)
    {
        var def = Definition(card);
        return stat switch
        {
            CostStat.GoldCost => basePrinted ? def.GoldCost : CardGoldCost(card),
            CostStat.Force => basePrinted ? def.Force : def.Type == CardType.Personality ? PersonalityForce(card) : def.Type == CardType.Follower ? FollowerForce(card) : Math.Max(0, def.Force + StatModifier(card, "Force")),
            CostStat.Chi => basePrinted ? def.Chi : def.Type == CardType.Personality ? PersonalityChi(card) : Math.Max(0, def.Chi + StatModifier(card, "Chi")),
            CostStat.PersonalHonor => basePrinted ? def.PersonalHonor : CardPersonalHonor(card),
            CostStat.Focus => basePrinted ? def.Focus : Math.Max(0, def.Focus + StatModifier(card, "Focus")),
            CostStat.ProvinceStrength => basePrinted ? def.ProvinceStrength : def.ProvinceStrength + StatModifier(card, "ProvinceStrength"),
            CostStat.GoldProduction => def.GoldProduction,
            _ => throw new InvalidOperationException($"Unknown cost stat '{stat}'.")
        };
    }

    private static string Modified(int modifier, int? floor) =>
        (modifier < 0 ? $" − {-modifier}" : modifier > 0 ? $" + {modifier}" : "") + (floor is int f ? $" (min {f})" : "");

    private static int Clamp(int raw, int? floor) => Math.Max(0, floor is int f ? Math.Max(f, raw) : raw);

    /// <summary>The single cost function for abilities. Null when the ability costs no Gold. Never throws
    /// on an unknown or partial command: a target-derived rule with no target reports NeedsTarget, and a
    /// rule that cannot be priced reports Error.</summary>
    private AbilityCost? AbilityGoldCost(GameCommand command)
    {
        if (MechEPaymentDeclined(command)) return null; // Greasing the Wheels (issue #501, EmperorMechE.cs): "You may pay ..." declined -- nothing is paid.
        var cost = AbilityCostWithTerms(command, BaseAbilityGoldCost(command));
        if (cost is not null && MomokoWaivesCost(command, MechAGoldVerb)) return null; // Isawa Momoko (issue #501): a Gold cost is one of the action's non-destruction costs
        if (cost is null || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability) || ability.Cost is null) return cost;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        // Discipline (Mendon's ruling, issue #353): played from a discard pile, the Discipline cost must be paid BEFORE the action's other costs can
        // be satisfied -- so it is a payment of its own, first, and the total is that plus the action's own cost. Played from hand only the action's
        // own cost applies, which is what the base price already is. (An action with NO cost of its own is the base price's implicit Discipline
        // rule, above.) A player-chosen variable cost has no combined form and no Discipline card prints one.
        if (!DisciplineFromDiscard(ability, source) || source is null) return cost;
        var lead = Definition(source).GoldCost;
        if (lead <= 0) return cost;
        if (cost.Rule.Basis == GoldCostBasis.Variable)
            return cost with { Error = "A Discipline play cannot be combined with a player-chosen Gold amount." };
        return cost with
        {
            Amount = cost.Amount + lead,
            LeadAmount = lead,
            Basis = $"{Definition(source).Name} Discipline cost {lead} (paid first), then {cost.Basis} = {cost.Amount + lead} in all"
        };
    }

    // Issue #682 (CostTerms.cs): the registered ability cost terms, applied to the rule's own price. A player-chosen amount and a price still waiting for a target are left alone.
    private AbilityCost? AbilityCostWithTerms(GameCommand command, AbilityCost? cost)
    {
        if (cost is null || cost.Error is not null || cost.NeedsTarget || cost.Rule.Basis == GoldCostBasis.Variable || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return cost;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        var terms = SumAbilityCostTerms(command.PlayerId, source, ability, command.AbilityId, command);
        if (terms.Adjustment == 0) return cost;
        return cost with { Amount = Math.Max(0, cost.Amount + terms.Adjustment), Basis = cost.Basis + terms.Basis + (cost.Amount + terms.Adjustment < 0 ? " (min 0)" : "") };
    }

    private AbilityCost? BaseAbilityGoldCost(GameCommand command)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return null;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        var target = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var t) ? t : null;
        var rule = PayGoldRule(ability, command) ?? (ability.CostOptions?.Any(o => o.Verb == CostVerbs.PayGold) == true ? null : ability.Cost ?? ImplicitGoldRule(ability, source)); // issue #682: a PayGold option is the price; a different option leaves it free
        if (rule is null) return null;
        switch (rule.Basis)
        {
            case GoldCostBasis.Fixed:
                return new(rule, rule.Amount, "Fixed by this ability", null, false, source ?? target);
            case GoldCostBasis.SourcePrinted:
                if (source is null) return new(rule, 0, "This ability has no source card to price.", "This ability has no source card to price.", false, null);
                // A Discipline play from a discard pile says so, so the payment panel names the Discipline cost
                // rather than implying a printed Gold cost the card does not have.
                {
                    var printedCost = Definition(source).GoldCost + PromoBSourceCostAdjustment(source); // Economic Manipulation (PromoCelestialB.cs): "This card has -1 Gold Cost if you control a Merchant."
                    return new(rule, printedCost, $"{Definition(source).Name} {(DisciplineFromDiscard(ability, source) ? "Discipline cost" : "cost")} {printedCost}", null, false, source);
                }
            case GoldCostBasis.Variable:
                {
                    var chosen = command.GoldAmount ?? rule.Amount;
                    var error = chosen < rule.Amount ? $"Choose at least {rule.Amount} Gold."
                        : rule.MaxAmount is int cap && chosen > cap ? $"Choose at most {cap} Gold." : null;
                    var basis = rule.MaxAmount is int max ? $"You choose the amount ({rule.Amount} to {max} Gold)"
                        : rule.Amount > 0 ? $"You choose the amount (at least {rule.Amount} Gold)" : "You choose the amount (zero or more Gold)";
                    return new(rule, chosen, basis, error, false, source ?? target, rule.Amount, rule.MaxAmount);
                }
            case GoldCostBasis.Stat:
                {
                    var statCard = rule.StatOf switch
                    {
                        CostStatSubject.Target => target,
                        CostStatSubject.Performer => command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performing) ? performing : null,
                        CostStatSubject.HeldActionSource => RemAHeldActionSourceCard(),
                        _ => source
                    };
                    if (statCard is null)
                        return new(rule, 0, rule.StatOf == CostStatSubject.Performer ? "Choose a performing card to see this cost." : "Choose a target to see this cost.", null, true, null);
                    var value = CostStatValue(statCard, rule.Stat!, rule.StatBase);
                    return new(rule, Clamp(value + rule.Modifier, rule.Floor),
                        $"{Definition(statCard).Name} {(rule.StatBase ? "base" : "current")} {StatLabel(rule.Stat!)} {value}{Modified(rule.Modifier, rule.Floor)}", null, false, statCard);
                }
            case GoldCostBasis.OwnPersonalities:
                {
                    var controlled = _state.Cards.Values.Count(c => c.OwnerId == command.PlayerId && InPlay(c) && Definition(c).Type == CardType.Personality);
                    return new(rule, Clamp(controlled + rule.Modifier, rule.Floor), $"the {controlled} Personalit{(controlled == 1 ? "y" : "ies")} you control{Modified(rule.Modifier, rule.Floor)}", null, false, source);
                }
            case GoldCostBasis.AttachCost:
                {
                    // The attachment is the target (a discard pile) or the source itself (in hand); the wielder is the source or the chosen
                    // performer. Until both are known there is nothing to price. Katagi's "from your hand" discount applies only from a hand.
                    if (ability.Attach is not { } attach) return new(rule, 0, "This ability attaches nothing.", "This ability attaches nothing.", false, null);
                    var attachment = attach.Card == AttachSource.Self ? source : target;
                    var wielder = WielderOfAttach(attach, command, source);
                    if (attachment is null || wielder is null)
                        return new(rule, 0, attachment is null ? "Choose a target to see this cost." : "Choose a performing card to see this cost.", null, true, null);
                    var (equipCost, equipBasis, _) = EquipGoldCost(command.PlayerId, attachment, wielder, fromHand: attachment.Zone == Zone.Hand);
                    return new(rule, Clamp(equipCost + rule.Modifier, rule.Floor), equipBasis + Modified(rule.Modifier, rule.Floor), null, false, attachment);
                }
        }
        // Every remaining basis is derived from the chosen target.
        if (target is null) return new(rule, 0, "Choose a target to see this cost.", null, true, null);
        var name = Definition(target).Name;
        switch (rule.Basis)
        {
            case GoldCostBasis.TargetPrinted:
                {
                    var printed = Definition(target).GoldCost;
                    // Extend the Soul's Boundaries (issue #453 batch 5): "This card enters play paying 1 less Gold for each Ring you control" -- a Spell enters play through Equip (Battle)'s
                    // target-printed price, so the count-scaled discount is read here (never below zero).
                    var ringDiscount = RingEntryDiscount(Definition(target), command.PlayerId);
                    // Purify the Soul (The Shadow's Embrace pass, issue #476 batch 5): "Attaches to an Air Shugenja paying 2 less Gold" -- through Equip (Battle) the Shugenja is the performer.
                    var purifyDiscount = command.AbilityId == "equip-battle" && command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var equipper) ? PurifyTheSoulDiscount(target, equipper) + MagaYariDiscount(target, equipper) : 0; // Maga-yari (issue #492 group G): the same kind of wielder discount
                                                                                                                                                                                                                                                                       // Quelsa (The Shadow's Embrace pass, issue #476, batch 2): "Quelsa attaches Spells paying 2 less Gold" -- a Spell attached to him through an ability that pays its printed cost.
                    var quelsaDiscount = QuelsaSpellDiscount(command, target);
                    // Whispering Archive (The Shadow's Embrace pass, issue #476 batch 6): "Your Spells enter play paying 1 less Gold" -- a Spell priced by its printed cost is one entering
                    // play (Equip (Battle), Soshi Neiru's equip), so its discount is a term of that price like the Ring discount.
                    var archiveDiscount = WhisperingArchiveDiscount(target, command.PlayerId);
                    if (ringDiscount + purifyDiscount + quelsaDiscount + archiveDiscount > 0)
                        return new(rule, Clamp(Math.Max(0, printed - ringDiscount - purifyDiscount - quelsaDiscount - archiveDiscount) + rule.Modifier, rule.Floor),
                            $"{name} cost {printed}{(ringDiscount > 0 ? $" − {ringDiscount} (Rings you control)" : "")}{(purifyDiscount > 0 ? $" − {purifyDiscount} (Air Shugenja)" : "")}{(quelsaDiscount > 0 ? $" − {quelsaDiscount} (Quelsa)" : "")}{(archiveDiscount > 0 ? $" − {archiveDiscount} (Whispering Archive)" : "")}{Modified(rule.Modifier, rule.Floor)}", null, false, target);
                    return new(rule, Clamp(printed + rule.Modifier, rule.Floor), $"{name} cost {printed}{Modified(rule.Modifier, rule.Floor)}", null, false, target);
                }
            case GoldCostBasis.TargetCurrent:
                {
                    var current = CardGoldCost(target);
                    return new(rule, Clamp(current + rule.Modifier, rule.Floor), $"{name} cost {current}{Modified(rule.Modifier, rule.Floor)}", null, false, target);
                }
            case GoldCostBasis.TargetUnitTotal:
                {
                    var total = UnitTotalGoldCost(target, out var breakdown);
                    var label = Definition(target).Type == CardType.Personality && breakdown.Contains('+') ? $"{name} unit total {breakdown}" : $"{name} total cost {total}";
                    return new(rule, Clamp(total + rule.Modifier, rule.Floor), label + Modified(rule.Modifier, rule.Floor), null, false, target);
                }
            case GoldCostBasis.TargetRecruit:
                {
                    var recruit = QuotePurchase(command.PlayerId, target.Id, ignoreHonorRequirementIfPersonalityClan: rule.HonorWaiverClan, fromDeadPile: target.Zone is Zone.Dead or Zone.DynastyDiscard,
                        anyOwnerPile: rule.AnyOwnerPile);
                    var derivation = recruit.Error is null && recruit.Cost != Definition(target).GoldCost && recruit.Basis is { } terms ? $" ({terms})" : "";
                    var performerCost = 0; var performerNote = "";
                    if (rule.MinusPerformerCost)
                    {
                        if (command.PerformerId is null || !_state.Cards.TryGetValue(command.PerformerId, out var performing)) return new(rule, 0, "Choose a performing card to see this cost.", null, true, null);
                        // A Yojimbo's Duty (Forgotten Legacy pass, issue #467 batch 3): "for less Gold equal to your Yojimbo's PRINTED Gold Cost plus 1" -- StatBase (the flag
                        // GoldCostRule.StatValue already uses for "the printed value") reads the performer's printed Gold Cost instead of his current one.
                        performerCost = rule.StatBase ? Definition(performing).GoldCost : CardGoldCost(performing);
                        performerNote = $" less {Definition(performing).Name}'s {(rule.StatBase ? "printed " : "")}Gold Cost {performerCost}";
                    }
                    // Black Pearl (issue #467): "paying 4 less Gold if the Personality is Nonhuman".
                    var keywordDiscount = rule.DiscountKeyword is { } discountKeyword && Keyword(target, discountKeyword) ? rule.KeywordDiscount : 0;
                    var keywordNote = keywordDiscount > 0 ? $" less {keywordDiscount} ({rule.DiscountKeyword})" : "";
                    return new(rule, Clamp(recruit.Cost + rule.Modifier - performerCost - keywordDiscount, rule.Floor), $"{name} recruit cost {recruit.Cost}{derivation}{performerNote}{keywordNote}{Modified(rule.Modifier, rule.Floor)}", recruit.Error, false, target);
                }
        }
        return new(rule, 0, "Unsupported Gold cost rule.", "Unsupported Gold cost rule.", false, null);
    }

    /// <summary>The Gold an ability's own resolution must charge for this command (0 when it is free). The
    /// effects of a variable-cost ability read the amount the player actually chose from here.</summary>
    private int PaidGoldAmount(GameCommand command) => AbilityGoldCost(command)?.Amount ?? 0;

    /// <summary>ValidateAbility's cost step: reject a rule that cannot be priced or a stray/out-of-range
    /// chosen amount, then require that the offered sources actually produce it.</summary>
    private void ValidateAbilityPayment(GameCommand command, int playerId)
    {
        UsePaymentDiscards(command);
        var cost = AbilityGoldCost(command);
        Require(command.GoldAmount is null || cost?.Rule.Basis == GoldCostBasis.Variable, "This action has no player-chosen Gold amount.");
        if (cost is not null) Require(cost.Error is null, cost.Error ?? "");
        var paying = PaymentSubjectFor(command, cost?.Subject);
        if (cost is { LeadAmount: > 0 }) ValidateSplitPayment(playerId, cost.LeadAmount, cost.Amount - cost.LeadAmount, command.SourceIds, paying);
        else ValidatePayment(playerId, cost?.Amount ?? 0, command.SourceIds, CostPaidCards(command), paying);
    }

    // ---- two-part payment: the Discipline cost first, then the action's own cost (issue #353) --------------------------------------------------
    // Gold does not carry over between payments, so the parts are separate: the sources are split into a set that covers the Discipline cost and
    // the rest, which must cover the action's own cost. A single source producing more than the Discipline cost loses the excess -- it cannot
    // also pay the second part -- exactly as if the two payments were made one after the other. The split is a property of the SET of sources
    // (any order is accepted); when several splits work the one with the least excess on the Discipline part is used, deterministically.
    private const int MaxSplitPaymentSources = 16;

    private bool TrySplitPayment(int playerId, int lead, int rest, string[] ids, out string[] leadIds, out string[] restIds, PaymentSubject? paying = null)
    {
        leadIds = []; restIds = [];
        if (ids.Length > MaxSplitPaymentSources) return false;
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var produced = sorted.Select(id => GoldProduced(id, playerId, paying)).ToArray();
        var total = produced.Sum();
        (int Excess, int Count, int Mask)? best = null;
        for (var mask = 0; mask < 1 << sorted.Length; mask++)
        {
            var first = 0; var count = 0;
            for (var i = 0; i < sorted.Length; i++) if ((mask >> i & 1) == 1) { first += produced[i]; count++; }
            if (first < lead || total - first < rest) continue;
            var candidate = (first - lead, count, mask);
            if (best is null || candidate.CompareTo(best.Value) < 0) best = candidate;
        }
        if (best is null) return false;
        var chosen = best.Value.Mask;
        leadIds = sorted.Where((_, i) => (chosen >> i & 1) == 1).ToArray();
        restIds = sorted.Where((_, i) => (chosen >> i & 1) == 0).ToArray();
        return true;
    }

    private void ValidateSplitPayment(int playerId, int lead, int rest, string[]? sourceIds, PaymentSubject? paying = null)
    {
        var ids = sourceIds ?? [];
        Require(ids.Distinct(StringComparer.Ordinal).Count() == ids.Length, "A Gold source cannot be used twice in one payment.");
        var producers = PaymentSourceIds(playerId, paying); // issue #501: the payment tokens too (EmperorMechE.cs)
        Require(ids.All(producers.Contains), "Every Gold source must be yours, in play, unbowed and able to produce Gold.");
        RequirePaymentTokens(ids, null);
        RequirePaymentTokenConditions(ids, playerId, paying); // issue #695
        RequirePaymentDiscards(playerId, ids.Where(id => !IsPaymentToken(id)).ToArray(), paying);
        Require(TrySplitPayment(playerId, lead, rest, ids, out _, out var restIds, paying) && (rest > 0 || restIds.Length == 0),
            $"This play needs its Discipline cost ({lead} Gold) paid first and then {rest} Gold for the action itself, each from its own sources (Gold does not carry over); the selected sources cannot do that.");
    }

    private void PaySplit(int playerId, CardInstance target, int lead, int rest, string[]? sourceIds, PaymentSubject? paying = null)
    {
        ValidateSplitPayment(playerId, lead, rest, sourceIds, paying);
        TrySplitPayment(playerId, lead, rest, sourceIds ?? [], out var leadIds, out var restIds, paying);
        Pay(playerId, target, lead, leadIds, "Discipline cost, paid first", paying: paying);
        if (rest > 0) Pay(playerId, target, rest, restIds, "the action's own cost", paying: paying);
    }

    /// <summary>Suggested sources for a two-part payment: every alternative for the Discipline cost, each completed from the sources it leaves for the
    /// action's own cost. Empty when either part cannot be paid (so the play is offered only when both are).</summary>
    private PaymentOption[] SplitPaymentOptions(int playerId, int lead, int rest, PaymentSubject? paying = null)
    {
        var options = new List<PaymentOption>();
        foreach (var first in PaymentOptions(playerId, lead, paying).Take(8))
            foreach (var second in PaymentOptionsExcluding(playerId, rest, first.SourceIds.ToHashSet(StringComparer.Ordinal), paying).Take(4))
                options.Add(new([.. first.SourceIds, .. second.SourceIds], first.Produced + second.Produced, lead + rest, first.Excess + second.Excess));
        return options.OrderBy(p => p.SourceIds.Count(id => IsPaymentToken(id) || Definition(Card(id)).GoldProduction == 0)).ThenBy(p => p.Excess).ThenBy(p => p.SourceIds.Length)
            .ThenBy(p => string.Join(",", p.SourceIds), StringComparer.Ordinal).Take(24).ToArray();
    }

    /// <summary>ExecuteAbilityEffects' payment step: charge exactly what validation priced, bowing exactly
    /// the sources the player chose. A free implicit rule (a 0-cost Strategy) charges nothing and, as it
    /// always has, leaves no payment event.</summary>
    private void PayForAbility(GameCommand command, PlayerState player, bool atResolution = false)
    {
        UsePaymentDiscards(command);
        if (AbilityGoldCost(command) is not { } cost || cost.Rule.AtResolution != atResolution) return;
        Require(cost.Error is null, cost.Error ?? "");
        if (!cost.Rule.Explicit && cost.Amount <= 0) return;
        var paying = PaymentSubjectFor(command, cost.Subject);
        if (cost.LeadAmount > 0) PaySplit(player.Id, cost.Subject ?? Card(player.StrongholdId), cost.LeadAmount, cost.Amount - cost.LeadAmount, command.SourceIds, paying);
        else Pay(player.Id, cost.Subject ?? Card(player.StrongholdId), cost.Amount, command.SourceIds, exclude: CostPaidCards(command), paying: paying);
    }

    /// <summary>The quote the client renders for an ability command (full or partial: a target-derived cost
    /// with no target yet reports an Error). Null when the ability costs no Gold. The acting player is
    /// command.PlayerId.</summary>
    public GoldQuote? QuoteAbilityGold(GameCommand command)
    {
        if (command.PlayerId is < 0 or > 1) return null;
        var cost = AbilityGoldCost(command);
        return cost is null ? null : BuildQuote(command.PlayerId, cost, CostPaidCards(command), PaymentSubjectFor(command, cost.Subject));
    }

    private GoldQuote BuildQuote(int playerId, AbilityCost cost, IReadOnlySet<string>? exclude, PaymentSubject? paying = null)
    {
        var error = cost.Error ?? (cost.NeedsTarget ? cost.Basis : null);
        if (cost.Rule.Basis != GoldCostBasis.Variable)
            return new(cost.Amount, cost.Rule.Kind, cost.Basis, error,
                error is not null ? [] : QueryMemo(("options", playerId, cost.Amount, cost.LeadAmount, ExcludeKey(exclude), paying),
                    () => cost.LeadAmount > 0 ? SplitPaymentOptions(playerId, cost.LeadAmount, cost.Amount - cost.LeadAmount, paying) : PaymentOptionsExcluding(playerId, cost.Amount, exclude, paying)),
                Sources: QueryMemo(("sources", playerId, ExcludeKey(exclude), paying), () => PaymentSourcesFor(playerId, paying, exclude))); // issue #501
        return VariableGoldQuote(playerId, cost.Min ?? 0, cost.Max, cost.Amount, cost.Basis, error, paying);
    }

    /// <summary>A "pay zero or more Gold" quote for whoever pays it: every amount from min up to what the player can produce (capped at cap),
    /// each with the engine's suggested sources, so the acting player's own variable cost and the opposing player's payment share one shape.</summary>
    private GoldQuote VariableGoldQuote(int playerId, int min, int? cap, int chosen, string basis, string? error = null, PaymentSubject? paying = null)
    {
        var production = Producers(playerId, paying).Sum(c => GoldProduced(c.Id, playerId, paying)) + MaxPaymentTokenProduction(playerId, paying); // issue #501: the best token of each card
        var max = Math.Max(min, Math.Min(cap ?? int.MaxValue, production));
        var byAmount = new Dictionary<int, PaymentOption[]>();
        for (var amount = min; amount <= max && error is null; amount++)
        {
            var options = PaymentOptions(playerId, amount, paying).Take(VariablePaymentOptionsPerAmount).ToArray();
            if (options.Length > 0) byAmount[amount] = options;
        }
        var payments = error is null && byAmount.TryGetValue(chosen, out var suggested) ? suggested : [];
        return new(chosen, GoldCostKind.Variable, basis, error, payments, min, max, byAmount, PaymentSourcesFor(playerId, paying));
    }

    /// <summary>Wraps a purchase/recruit quote in the general shape the payment panel renders, so the
    /// client has one panel for every Gold action. Recruiting is fixed: the card's printed cost adjusted by
    /// the chosen Clan discount/allegiance, all decided by QuotePurchase.</summary>
    private static GoldQuote ToGoldQuote(PurchaseQuote quote) =>
        new(quote.Cost, GoldCostKind.Fixed, quote.Basis ?? $"Cost {quote.Cost}", quote.Error, quote.Payments, Sources: quote.Sources); // issue #578: the recruit panel needs the payment tokens its suggestions may name

    // Equip (issue #303 folds Equip's own cost into the same quote shape): the attachment's printed cost
    // less every discount that applies to THIS attachment, THIS wielder and THIS player. Katagi's is the
    // one wielder-dependent term, so it alone makes an equip quote "derived"; the Clan discounts depend on
    // the equipping player only.
    // fromHand (issue #337): an attach from a discard pile prices like an Equip except for a discount printed for attachments "from your
    // hand" (Katagi's), which only an attach from hand earns.
    private (int Cost, string Basis, string Kind) EquipGoldCost(int playerId, CardInstance card, CardInstance target, bool fromHand = true)
    {
        var def = Definition(card); var player = Player(playerId);
        var basis = $"{def.Name} cost {def.GoldCost}";
        var raw = def.GoldCost; var kind = GoldCostKind.Fixed;
        // Mirumoto Katagi (issue #216): "Non-Unique Weapons attaching to Katagi from your hand enter
        // play paying 4 less Gold" -- a narrow, single-card discount (unlike Clan Discount/Allegiance,
        // which are printed-choice flags on every purchase), so it belongs on Equip's own cost
        // formula directly rather than as a new command flag.
        if (fromHand && Definition(target).Effect == "weapon-discount-non-unique" && Keyword(card, "Weapon") && !Keyword(card, "Unique"))
        { raw -= 4; kind = GoldCostKind.Derived; basis += $" − 4 ({Definition(target).Name}, non-Unique Weapon)"; }
        // Tempest of Flame (issue #566): "Attaches to a Fire or Void Shugenja paying 1 less Gold" -- the attaching card's OWN discount (unlike Katagi's, which is keyed
        // on the wielder), unconditional on source zone (no "from your hand" is printed).
        if (def.Effect == "tempest-of-flame" && (Keyword(target, "Fire") || Keyword(target, "Void")))
        { raw -= 1; kind = GoldCostKind.Derived; basis += " − 1 (Fire or Void Shugenja)"; }
        // Satoshi's Dual Warfans (issue #264): "Enters play paying 1 less Gold if you are a Lion
        // Clan player" -- the EQUIPPING PLAYER's own Clan, not the wielder's own trait (a different
        // axis from Katagi's own weapon-discount-non-unique above, so a separate term rather than
        // folded into that one).
        // Legion of the Bat (issue #279): "Enters play paying 1 less Gold if you are a Crane Clan
        // player" -- the exact same shape as Satoshi's Dual Warfans' own lion-clan-discount above,
        // just a different Clan/term.
        // Champion of Thunder (the general in-play unit-targeting Reaction primitive's own proof
        // case): "Enters play paying 1 less Gold if you are a Mantis Clan player" -- the same shape,
        // keyed off a different Clan.
        // Daigotsu Hotako exp3 (issue #336): the same term for a Personality, and for any N ("<clan>-clan-discount-N"): ClanEntryDiscount
        // reads all of them, here and in QuotePurchase, so an entry discount by Clan has one definition.
        if (ClanEntryDiscount(def, player) is { } clanEntry) { raw -= clanEntry.Amount; basis += $" − {clanEntry.Amount} ({clanEntry.Clan} Clan)"; }
        // Mukudori (Seeds of Decay, issue #453): "Your other Fudo cards enter play for 1 less Gold" -- the same term QuotePurchase applies (StaticKeywordRules.cs).
        if (KeywordEntryDiscount(card, playerId) is > 0 and var fudoDiscount) { raw -= fudoDiscount; basis += $" − {fudoDiscount} (Fudo, Mukudori)"; }
        // Purify the Soul (The Shadow's Embrace pass, issue #476 batch 5): "Attaches to an Air Shugenja paying 2 less Gold" -- a wielder-dependent term, like Katagi's.
        if (PurifyTheSoulDiscount(card, target) is > 0 and var purifyDiscount) { raw -= purifyDiscount; kind = GoldCostKind.Derived; basis += $" − {purifyDiscount} (Air Shugenja)"; }
        // Maga-yari (Emperor Edition remaining-cards pass, issue #492 group G): "Attaches to a Paragon or Kensai paying 3 less Gold."
        if (MagaYariDiscount(card, target) is > 0 and var magaYariDiscount) { raw -= magaYariDiscount; kind = GoldCostKind.Derived; basis += $" − {magaYariDiscount} (Paragon or Kensai)"; }
        // Record of Failure (issue #622, IvoryPolitical.cs): "Attaches to a Courtier paying 2 less Gold."
        if (IvoryPoliticalAttachDiscount(card, target) is > 0 and var recordDiscount) { raw -= recordDiscount; kind = GoldCostKind.Derived; basis += $" − {recordDiscount} (Courtier)"; }
        // Quelsa (The Shadow's Embrace pass, issue #476, batch 2): "Quelsa attaches Spells paying 2 less Gold."
        if (QuelsaAttachDiscount(card, target) is > 0 and var quelsaAttach) { raw -= quelsaAttach; kind = GoldCostKind.Derived; basis += $" − {quelsaAttach} (Quelsa)"; }
        // Whispering Archive (The Shadow's Embrace pass, issue #476 batch 6): "Your Spells enter play paying 1 less Gold."
        if (WhisperingArchiveDiscount(card, playerId) is > 0 and var archiveDiscount) { raw -= archiveDiscount; basis += $" − {archiveDiscount} (Whispering Archive)"; }
        // Palace of the Breaking Dawn (Emperor Edition Stronghold pass, issue #486 batch 2): "Your Ronin cards enter play paying 1 less Gold."
        if (RoninEntryDiscount(card, playerId) is > 0 and var roninDiscount) { raw -= roninDiscount; basis += $" − {roninDiscount} (Palace of the Breaking Dawn)"; }
        // Daigotsu Misaki (Emperor Edition Personality pass, issue #477): "Followers attaching to Misaki enter play paying 2 less Gold" -- any attach, from a hand or a discard pile.
        if (FollowerAttachDiscount(card, target) is > 0 and var misakiDiscount) { raw -= misakiDiscount; kind = GoldCostKind.Derived; basis += $" − {misakiDiscount} ({Definition(target).Name})"; }
        // Promotional-Emperor Champion items (PromoEmperorItems.cs): "Attaches to a [X] Champion ignoring costs."
        if (PromoItemsAttachesFree(card, target)) { raw = 0; kind = GoldCostKind.Derived; basis += " (attaches to a Champion ignoring costs)"; }
        // Hyobuko's Heart of Fudo and The Topaz Armor - exp (issue #667, SearchPlayFudo.cs): "ignoring Gold cost" / "ignoring costs" on the wielder they name.
        if (SearchPlayAttachesFree(card, target)) { raw = 0; kind = GoldCostKind.Derived; basis += " (attaches ignoring Gold cost)"; }
        // Promotional-Celestial group C (PromoCelestialC.cs): Fudo Cultist "ignoring Gold cost" on a Nonhuman Fudo Personality; Yotsu Ueda's Chosen "paying 2 less Gold" on Yotsu Ueda.
        if (PromoCelestialCAttachDiscount(card, target, raw) is { Amount: > 0 } promoC) { raw -= promoC.Amount; kind = GoldCostKind.Derived; basis += promoC.Basis; }
        // Registered entry cost terms (issue #682, CostTerms.cs): the generic place a card prints "enters play ... less Gold" / "attaches ... paying N less Gold" / "ignoring Gold Cost".
        var entryTerms = SumEntryCostTerms(card, playerId, EntryCostKind.Equip, target, fromHand);
        if (entryTerms.WielderDependent) kind = GoldCostKind.Derived;
        if (entryTerms.Ignore) raw = 0; else raw += entryTerms.Adjustment;
        basis += entryTerms.Basis;
        if (raw < 0) basis += " (min 0)";
        return (Math.Max(0, raw), basis, kind);
    }

    private GoldQuote QuoteEquipGold(int playerId, CardInstance card, CardInstance target)
    {
        var (cost, basis, kind) = EquipGoldCost(playerId, card, target);
        var paying = new PaymentSubject(card, PaymentKind.Equip, Wielder: target); // issue #501: the wielder (Moto Rani - exp)
        return new(cost, kind, basis, null, PaymentOptions(playerId, cost, paying), Sources: PaymentSourcesFor(playerId, paying));
    }

    /// <summary>A Region attached by a deck search (issue #312, Ikoma Shizuka: "attach it to one of your provinces, paying
    /// all costs") pays its own printed Gold cost: one Fixed quote, and the engine's Pay() charges it. Null when free
    /// (every published Region costs 0, so no payment panel appears for them).</summary>
    private GoldQuote? QuoteRegionGold(int playerId, CardInstance region)
    {
        var def = Definition(region);
        return def.GoldCost > 0 ? new(def.GoldCost, GoldCostKind.Fixed, $"{def.Name} cost {def.GoldCost}", null, PaymentOptions(playerId, def.GoldCost), Sources: PaymentSourcesFor(playerId, null)) : null;
    }

    /// <summary>A Strategy played with the playStrategy command pays its own printed cost. Null when free.</summary>
    private GoldQuote? QuoteStrategyGold(int playerId, CardInstance card)
    {
        var def = Definition(card);
        var (playCost, playBasis) = StrategyPlayCost(playerId, card); // issue #682: the registered ability cost terms
        return def.GoldCost > 0 ? new(playCost, GoldCostKind.Fixed, playBasis, null, PaymentOptions(playerId, playCost, new PaymentSubject(card, PaymentKind.Play)),
            Sources: PaymentSourcesFor(playerId, new PaymentSubject(card, PaymentKind.Play))) : null; // issue #501
    }
}
