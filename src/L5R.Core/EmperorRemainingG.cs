namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group G: item-wave-15 (Armor of the Heavens, Blade of Perfection, Chrysanthemum Blossom, Clan Standard, Gift Armor, Kensai's Blade,
// Maga-yari, Modifications, Reserve Weapon, Sasumata, Tsuruchi Daikyu) and follower-wave-31 (Bandit Gang, Ikiryo, the six House Guards, Legion of Pain). Every card reuses an existing
// primitive where one exists; what is new is small and documented at its use:
//   * a Reaction to another player targeting the WIELDER with an action, whose "choose a player who gains or loses 2 Honor" is two actions sharing one use (Armor of the Heavens);
//   * an attachment that returns from the discard pile to a Personality of its owner's after an action destroyed it or its Personality (Blade of Perfection: AttachSpec on a card in the Fate
//     discard pile, CardInstance.LastHostId remembering the Personality it was destroyed on), and one that attaches from the hand after engaging (Kensai's Blade);
//   * an "after this Personality is assigned" Reaction window (Chrysanthemum Blossom), opened once the assignment command has finished and read through the after-action-resolved machinery;
//   * a static Force bonus for the controller's Clan Personalities at the wielder's battlefield (Clan Standard);
//   * Chi penalties negated whatever their source and the item's own destruction negated except with its Personality (Gift Armor);
//   * an Equip discount by wielder keyword and "will not be transferred" (Maga-yari);
//   * a focus-replacing duel stat bonus (Reserve Weapon: PendingDuel.StatBonuses, the Focus command's "reserve" choice);
//   * a pay-or-destroy trait after a battle's resolution, an Either/Or decided by the Follower's controller (Bandit Gang);
//   * an Attack whose strength is read off the Chi of the Personality a destroyed Follower was last attached to (Ikiryo), off the host's Personal Honor (Ikoma House Guard), and one compared
//     against the lower of Force and Gold Cost (Tsuruchi House Guard);
//   * a target category comparing the units' total Force (Kaiu House Guard), a target category without a Weapon or Armor (Sasumata) and a static bar on being targeted by cheap Personalities'
//     actions (Legion of Pain).
public static class EmperorRemainingGAbilities
{
    public const string ChrysanthemumTrigger = AfterActionReactionTriggers.AfterActionResolved;

    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingG(this Dictionary<string, AbilityDefinition> all)
    {
        // Armor of the Heavens (oracle-l5r-446): "Political Reaction: After another player targets this Personality with an action: Choose a player, who gains or loses 2 Honor." The player is the
        // command's TargetPlayerId; gaining and losing are two actions sharing one use (Horume's shape). "This Personality" is the wielder.
        all["armor-heavens-gain"] = new("Reaction", "Political Reaction: After another player targets this Personality with an action: choose a player, who gains 2 Honor", "player",
            Trigger: ReactionTriggers.TargetedByAction, ActionKeywords: ["Political"], UsageKey: "armor-heavens");
        all["armor-heavens-lose"] = new("Reaction", "Political Reaction: After another player targets this Personality with an action: choose a player, who loses 2 Honor", "player",
            Trigger: ReactionTriggers.TargetedByAction, ActionKeywords: ["Political"], UsageKey: "armor-heavens");
        // Blade of Perfection (oracle-l5r-1010, the scan's wording): "Reaction: After the resolution of another player's action that destroyed this card or its Personality, choose your performing
        // Personality: Attach this card to him, paying all costs." The card is in its owner's Fate discard pile (FromDiscard), attached by its own AttachSpec.
        all["blade-perfection-attach"] = new("Reaction", "After the resolution of another player's action that destroyed this card or its Personality, choose your performing Personality: Attach this card to him, paying all costs",
            "none", Trigger: AfterActionReactionTriggers.AfterActionResolved, FromDiscard: true, Performer: "own-personality", Cost: GoldCostRule.AttachCost(),
            Attach: new(AttachSource.Self, AttachPile.Own, AttachWielder.Performer));
        // Chrysanthemum Blossom (oracle-l5r-1340): "Reaction: After this Personality is assigned: Give him a +1F token. Gain 1 Honor."
        all["chrysanthemum-blossom-boost"] = new("Reaction", "After this Personality is assigned: Give him a +1F token. Gain 1 Honor", "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Kensai's Blade (oracle-l5r-4323): "Reaction: After engaging, choose your performing Personality at the current battlefield: Attach this card from your hand to him, paying all costs."
        all["kensais-blade-attach"] = new("Reaction", "After engaging, choose your performing Personality at the current battlefield: Attach this card from your hand to him, paying all costs", "none",
            Trigger: "after-engaging", Performer: "own-personality-explicitly-at-battle", Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Self, Wielder: AttachWielder.Performer));
        // Modifications (oracle-l5r-5214): "Battle: Draw a card."
        all["modifications-draw"] = new("Battle", "Draw a card", "none");
        // Sasumata (oracle-l5r-6478): "Battle: Bow this card and target an enemy card without a Weapon or Armor: Bow it."
        all["sasumata-bow"] = new("Battle", "Bow this card and target an enemy card without a Weapon or Armor: Bow it", "enemy-card-without-weapon-or-armor", Costs: ActionCost.BowSource);
        // Tsuruchi Daikyu (oracle-l5r-8823): "Battle: Bow this card: Ranged 5 Attack."
        all["tsuruchi-daikyu-attack"] = new("Battle", "Bow this card: Ranged 5 Attack", "attack", Costs: ActionCost.BowSource);
        // Ikiryo (oracle-l5r-3589): "Battle: Destroy this card: Ranged Attack, which may target a Personality with attached Followers, with strength equal to the Chi of the Personality to whom this card
        // was last attached. Lose Honor equal to that Personality's Chi and dishonor him."
        all["ikiryo-attack"] = new("Battle", "Destroy this card: Ranged Attack, which may target a Personality with attached Followers, with strength equal to the Chi of the Personality to whom this card was last attached. Lose Honor equal to that Personality's Chi and dishonor him",
            "attack", FollowerException: new("Ranged"), Costs: [new(CostVerbs.Sacrifice)]);
        // Ikoma House Guard (oracle-l5r-3611): "Battle: Melee Attack with strength equal to this Personality's Personal Honor plus two. If this Melee Attack destroyed a card, gain 1 Honor."
        all["ikoma-house-guard-attack"] = new("Battle", "Melee Attack with strength equal to this Personality's Personal Honor plus two. If this Melee Attack destroyed a card, gain 1 Honor", "attack");
        // Kaiu House Guard (oracle-l5r-4118): "Battle: Target an enemy card without attachments whose unit has lower total Force than this unit: Destroy the target. Gain 1 Honor."
        all["kaiu-house-guard-destroy"] = new("Battle", "Target an enemy card without attachments whose unit has lower total Force than this unit: Destroy the target. Gain 1 Honor",
            "enemy-card-without-attachments-unit-force-lt-source-unit");
        // Moto House Guard (oracle-l5r-5329): "Battle: If any enemy units are at the current battlefield: Move this Personality there. If he is now there, Ranged 4 Attack."
        all["moto-house-guard-attack"] = new("Battle", "If any enemy units are at the current battlefield: Move this Personality there. If he is now there, Ranged 4 Attack", "attack", MovesToBattle: true);
        // Tamori House Guard (oracle-l5r-7776): "Battle: Target an enemy Personality or Follower: It is encased in stone. Give it -4F. It may not perform actions."
        all["tamori-house-guard-stone"] = new("Battle", "Target an enemy Personality or Follower: It is encased in stone. Give it -4F. It may not perform actions", "enemy-force");
        // Tsuruchi House Guard (oracle-l5r-8837): "Battle: Ranged 5 Attack that you may compare against Gold Cost."
        all["tsuruchi-house-guard-attack"] = new("Battle", "Ranged 5 Attack that you may compare against Gold Cost", "attack");
        return all;
    }
}

/// <summary>Bandit Gang's after-resolution trait in progress (MatchState.PendingBanditGangs): the Followers still to be asked, and the resolution the battle carries on with once they are.</summary>
public sealed class PendingBanditGangs
{
    public List<string> FollowerIds { get; set; } = [];
    public PendingAfterResolution Facts { get; set; } = new();
}

/// <summary>The Either/Or spec Bandit Gang's trait raises.</summary>
public static class EmperorRemainingGSpecs
{
    public const string BanditGangPay = "bandit-gang-pay";
}

public sealed partial class MatchEngine
{
    // Effect keys (CardDefinition.Effect).
    internal const string ClanStandardEffect = "clan-standard";
    internal const string GiftArmorEffect = "gift-armor";
    internal const string MagaYariEffect = "maga-yari";
    internal const string BladeOfPerfectionEffect = "blade-perfection-rebound";
    internal const string BanditGangEffect = "bandit-gang";
    internal const string LegionOfPainEffect = "legion-of-pain-bar";
    internal const string ReserveWeaponEffect = "reserve-weapon";
    internal const string AssignedSentinel = "__assigned__";
    internal const string AssignedKind = "assigned";

    // Test seam (L5R.Core.Tests has InternalsVisibleTo): a Personality's current Chi.
    internal int CardChiForTest(string cardId) => PersonalityChi(Card(cardId));

    // ---- Attack tables (AttackStrength / AttackKind / AttackComparedValue hooks) ----

    private int? EmperorRemainingGAttackStrength(string abilityId, string? sourceId) => abilityId switch
    {
        "tsuruchi-daikyu-attack" or "tsuruchi-house-guard-attack" => 5,
        "moto-house-guard-attack" => 4,
        // "The Chi of the Personality to whom this card was last attached": remembered when the Follower was destroyed as the cost (CardInstance.LastHostId).
        "ikiryo-attack" => sourceId is not null && _state.Cards.TryGetValue(sourceId, out var ikiryo) && ikiryo.LastHostId is { } lastHost && _state.Cards.TryGetValue(lastHost, out var lastHostCard)
            ? PersonalityChi(lastHostCard) : 0,
        // "This Personality's Personal Honor plus two": the Follower's host.
        "ikoma-house-guard-attack" => sourceId is not null && _state.Cards.TryGetValue(sourceId, out var ikoma) && ikoma.AttachedTo is { } ikomaHost ? CardPersonalHonor(Card(ikomaHost)) + 2 : 0,
        _ => null
    };

    private static string RemainingGAttackKind(string abilityId) => abilityId switch
    {
        "tsuruchi-daikyu-attack" or "tsuruchi-house-guard-attack" or "moto-house-guard-attack" or "ikiryo-attack" => "Ranged",
        "ikoma-house-guard-attack" => "Melee",
        _ => ""
    };

    // "Ranged 5 Attack that you may compare against Gold Cost": the player chooses Force or Gold Cost (issue #492, ruling R5).
    private int AttackComparedValueRemainingG(string abilityId, CardInstance target) =>
        abilityId == "tsuruchi-house-guard-attack" && AttackCompareChoiceOf(abilityId) == "gold" ? CardGoldCost(target) : AttackComparedForce(target, abilityId);

    // Ikoma House Guard: "If this Melee Attack destroyed a card, gain 1 Honor."
    private void AfterEmperorRemainingGAttack(string abilityId, int playerId, string? targetId, bool targetInPlayBefore, string? sourceId)
    {
        if (abilityId != "ikoma-house-guard-attack" || targetId is null || !targetInPlayBefore || !_state.Cards.TryGetValue(targetId, out var target) || InPlay(target)) return;
        GainHonor(playerId, 1, "Ikoma House Guard", sourceId is not null && _state.Cards.TryGetValue(sourceId, out var guard) ? guard : null);
    }

    // ---- target categories ----

    // Sasumata: "an enemy card without a Weapon or Armor" -- a card in a unit (a Personality or an attachment) of the other player's, no attached Weapon or Armor.
    private bool EnemyCardWithoutWeaponOrArmor(CardInstance target, int playerId)
    {
        var type = Definition(target).Type;
        var inUnit = type == CardType.Personality || target.Zone == Zone.Attached && type is CardType.Follower or CardType.Item or CardType.Spell;
        return target.OwnerId != playerId && inUnit && !Attachments(target.Id).Any(a => Keyword(a, "Weapon") || Keyword(a, "Armor"));
    }

    // Kaiu House Guard: "an enemy card without attachments whose unit has lower total Force than this unit" -- a Follower (whose unit is its Personality's) or a bare Personality.
    private bool EnemyBareCardWhoseUnitHasLowerForce(CardInstance target, CardInstance source, int playerId)
    {
        var type = Definition(target).Type;
        if (target.OwnerId == playerId || !(type == CardType.Follower && target.Zone == Zone.Attached || type == CardType.Personality && !HasAttachments(target))) return false;
        return UnitTotalForce(Card(UnitHostId(target))) < UnitTotalForce(Card(UnitHostId(source)));
    }

    // ---- validation ----

    private void ValidateEmperorRemainingG(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        // Moto House Guard: "If any enemy units are at the current battlefield:" -- a condition before the colon gates the action; the Personality it moves is the Follower's host.
        if (id == "moto-house-guard-attack")
        {
            Require(_state.CurrentBattleProvince is int battlefield && Army(1 - player.Id, battlefield).Any(), "There must be enemy units at the current battlefield.");
            Require(source is { Zone: Zone.Attached, AttachedTo: not null }, "This Follower must be attached to a Personality.");
        }
    }

    // ---- execution ----

    // True when the action paused (an Attack's reaction window, an attach's entering-play window) and finishes from there.
    private bool RunEmperorRemainingGEffect(string id, GameCommand command, AbilityDefinition ability, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant)
    {
        switch (id)
        {
            // Armor of the Heavens: the chosen player gains or loses 2 Honor; the performer is the wielder (a dishonorable one rehonors instead of granting his own controller Honor).
            case "armor-heavens-gain":
            case "armor-heavens-lose":
                {
                    var wielder = source!.AttachedTo is { } hostId ? Card(hostId) : source;
                    ApplyPlayerHonorChoice(wielder, command.TargetPlayerId!.Value, id == "armor-heavens-gain" ? 2 : -2);
                    return false;
                }
            case "blade-perfection-attach":
            case "kensais-blade-attach":
                return ExecuteAttach(command, ability.Attach!, player, source, target);
            case "chrysanthemum-blossom-boost":
                {
                    var host = Card(source!.AttachedTo);
                    CreateToken(host, "Force", id, "Force", 1);
                    GainHonorFromPerformer(host, 1, "Chrysanthemum Blossom");
                    return false;
                }
            case "modifications-draw":
                Draw(player);
                return false;
            case "sasumata-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "tsuruchi-daikyu-attack":
            case "tsuruchi-house-guard-attack":
            case "ikoma-house-guard-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case "ikiryo-attack":
                {
                    // The Follower was destroyed as the cost; it remembers the Personality it was last attached to. "Lose Honor equal to that Personality's Chi and dishonor him."
                    if (source!.LastHostId is { } lastHostId && _state.Cards.TryGetValue(lastHostId, out var lastHost))
                    {
                        GainHonor(player.Id, -PersonalityChi(lastHost), "Ikiryo", source);
                        if (InPlay(lastHost) && Definition(lastHost).Type == CardType.Personality) Dishonor(lastHost, player.Id);
                    }
                    return ResolveOrDeferAttack(id, player, target?.Id, source.Id, consumedGrant);
                }
            case "kaiu-house-guard-destroy":
                DestroyTargetCard(target!, player.Id);
                GainHonor(player.Id, 1, "Kaiu House Guard", source);
                return false;
            case "tamori-house-guard-stone":
                ApplyModifier(target!, source!.Id, id, "Force", -4);
                LockActions(target!, source.Id);
                return false;
            default:
                throw new InvalidOperationException($"Unknown Emperor Edition group G ability '{id}'.");
        }
    }

    // ---- reaction triggers ----

    // Armor of the Heavens' four choices (issue #1240, Mendon): each option names only who gains or loses the Honor; the printed trigger text is already the card's.
    private static string? PlayerChoiceOptionLabel(string abilityId, bool you) => abilityId switch
    {
        "armor-heavens-gain" => you ? "You Gain 2 Honor" : "Opponent Gains 2 Honor",
        "armor-heavens-lose" => you ? "You Lose 2 Honor" : "Opponent Loses 2 Honor",
        _ => null,
    };

    // Armor of the Heavens (TargetedActionReactionApplies): another player's action targets the Personality this card is attached to.
    private bool ArmorOfTheHeavensApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && card.Zone == Zone.Attached && affected.Id == card.AttachedTo && Definition(affected).Type == CardType.Personality;

    // Blade of Perfection (AfterActionResolvedApplies): the card sits in its owner's Fate discard pile, and another player's action that has just resolved destroyed it, or the Personality it was
    // last attached to (the record of the destruction of a Personality is made only for an action's own effects, never a battle's casualty sweep).
    private bool BladeOfPerfectionApplies(CardInstance card, int playerId, ResolvedActionFacts facts) =>
        card.Zone == Zone.FateDiscard && facts.PlayerId != playerId
        && facts.Affected.Any(a => a.Kind == DestroyedKind && (a.CardId == card.Id || card.LastHostId is not null && a.CardId == card.LastHostId));

    // Chrysanthemum Blossom (AfterActionResolvedApplies): the assignment that just finished assigned the Personality it is attached to.
    private bool ChrysanthemumBlossomApplies(CardInstance card, int playerId, ResolvedActionFacts facts) =>
        facts.AbilityId == AssignedSentinel && facts.PlayerId == playerId && card is { Zone: Zone.Attached, AttachedTo: { } hostId } && facts.Affected.Any(a => a.Kind == AssignedKind && a.CardId == hostId);

    // ---- "after this Personality is assigned" window ----

    // Assign() records each Personality it places; once the assignment command has finished (the phase has moved on, no choice such as Brash is still open) a Reaction window opens if
    // anyone holds a Reaction to it.
    private void OpenAssignedReactionWindow()
    {
        if (_state.AssignedThisCommand.Count == 0 || _state.PendingChoice is not null || _state.PendingDuel is not null || _state.WinnerId is not null) return;
        var assigned = _state.AssignedThisCommand.ToArray();
        var playerId = Card(assigned[0]).OwnerId;
        _state.AssignedThisCommand = [];
        foreach (var id in assigned) // P6 (AfterFacts.cs): "after a Personality is assigned to attack"
            if (_state.Cards.TryGetValue(id, out var attacker) && attacker.Zone == Zone.Battlefield && attacker.OwnerId == _state.ActivePlayerId) RecordFact(FactKinds.AssignedToAttack, attacker.OwnerId, id, null, attacker.ProvinceIndex);
        if (!assigned.Any(id => _state.Cards.TryGetValue(id, out var c) && c.Zone == Zone.Battlefield && Attachments(id).Any(a => AbilityIdsOf(a).Contains("chrysanthemum-blossom-boost"))) && !P3WatchesAssignment(assigned)) return; // + Utaku Tairu - exp (issue #503)
        _state.LastResolvedAction = new ResolvedActionFacts
        {
            AbilityId = AssignedSentinel,
            PlayerId = playerId,
            SourceId = null,
            PerformerId = null,
            Affected = assigned.Select(id => new AffectedCard(id, AssignedKind)).ToList()
        };
        var closesTo = _state.PriorityPlayerId;
        if (!OpenAfterActionResolvedWindow(closesTo)) _state.LastResolvedAction = null;
    }

    // ---- Force, Chi, destruction ----

    // Clan Standard: "Your Personalities with your Clan alignment at this card's battlefield have +1F, or +2F if they have any Followers." PersonalityForce's hook.
    private int EmperorRemainingGForce(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality || card.Zone != Zone.Battlefield) return 0;
        var force = 0;
        var clan = Player(card.OwnerId).Clan;
        foreach (var standard in _state.Cards.Values)
        {
            if (standard.Zone != Zone.Attached || standard.OwnerId != card.OwnerId || Definition(standard).Effect != ClanStandardEffect || standard.AttachedTo is not { } hostId) continue;
            var host = Card(hostId);
            if (host.Zone != Zone.Battlefield || host.ProvinceIndex != card.ProvinceIndex || !HasClanAffiliation(card, clan)) continue;
            force += Attachments(card.Id).Any(a => Definition(a).Type == CardType.Follower) ? 2 : 1;
        }
        return force;
    }

    // Gift Armor: "Negate all current and new Chi penalties on this Personality." ApplyModifier, CreateToken and AddPermanentModifier ask; the current ones are purged when it attaches.
    private bool GiftArmorNegatesChiPenalty(CardInstance card, string? stat, int amount) =>
        stat == "Chi" && amount < 0 && Attachments(card.Id).Any(a => Definition(a).Effect == GiftArmorEffect);

    private void PurgeChiPenalties(CardInstance card)
    {
        var removed = card.Modifiers.RemoveAll(m => m.Stat == "Chi" && m.Amount < 0);
        foreach (var token in card.Tokens.Where(t => t.Stat == "Chi" && t.Amount < 0).ToArray()) { DestroyToken(card, token); removed++; }
        foreach (var token in card.Tokens.Where(t => t.Stat2 == "Chi" && t.Amount2 < 0).ToArray()) { card.Tokens[card.Tokens.IndexOf(token)] = token with { Stat2 = null, Amount2 = 0 }; removed++; } // issue #503: a two-stat token keeps its other stat
        if (removed > 0) Emit("penaltiesNegated", $"{Definition(card).Name}: {removed} Chi penalt{(removed == 1 ? "y" : "ies")} negated (Gift Armor).", card.OwnerId, card.Id);
    }

    // "Negate any destruction of this card, except for its Personality being destroyed": DestroyAttachment is the destruction of the card itself; the destruction of its Personality
    // (DestroyUnits) takes it along and is not asked.
    // Ancient Armor of the Qamar, Six-Ring Sword (PromoEmperorItems.cs)
    private bool GiftArmorNegatesDestruction(CardInstance card) => Definition(card).Effect == GiftArmorEffect || PromoItemsNegateDestruction(card);

    // ---- Maga-yari ----

    // "Attaches to a Paragon or Kensai paying 3 less Gold."
    private int MagaYariDiscount(CardInstance card, CardInstance wielder) =>
        Definition(card).Effect == MagaYariEffect && (Keyword(wielder, "Paragon") || Keyword(wielder, "Kensai")) ? 3 : 0;

    // "This card will not be transferred": the Cultist Infiltrator shape, for an overlay's carry-over and every transfer.
    private bool WillNotBeTransferred(CardInstance attachment) => Definition(attachment).Effect == MagaYariEffect;

    // ---- Reserve Weapon ----

    // "Before this Personality focuses: You may give him +3 to his duel stat until the duel ends instead of focusing." The weapons on this side's duelling Personality that offer it.
    private IEnumerable<CardInstance> ReserveWeaponsFor(int playerId, PendingDuel duel) =>
        Attachments(playerId == duel.ChallengerPlayerId ? duel.ChallengerId : duel.ChallengedId).Where(a => Definition(a).Effect == ReserveWeaponEffect && a.OwnerId == playerId);

    private void ReserveWeaponFocus(GameCommand command)
    {
        var duel = _state.PendingDuel!;
        var usedList = command.PlayerId == duel.ChallengerPlayerId ? duel.ChallengerFocused : duel.ChallengedFocused;
        Require(usedList.Count + P2FocusSubstitutions(command.PlayerId) < 4, "You have already focused four times this duel and must Strike."); // core rules: an "instead of focusing" substitution counts against the cap of four
        var weapon = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var chosen) ? chosen : null;
        Require(weapon is not null && ReserveWeaponsFor(command.PlayerId, duel).Any(w => w.Id == weapon.Id), "Choose a Reserve Weapon on your dueling Personality.");
        var personalityId = command.PlayerId == duel.ChallengerPlayerId ? duel.ChallengerId : duel.ChallengedId;
        duel.StatBonuses[personalityId] = duel.StatBonuses.GetValueOrDefault(personalityId) + 3;
        P2.FocusSubstitutions[FocusSubstitutionKey(command.PlayerId)] = P2FocusSubstitutions(command.PlayerId) + 1; // each use consumes one of the four focus opportunities
        Emit("duelReserveWeapon", $"{Player(command.PlayerId).Name} gave {Definition(Card(personalityId)).Name} +3 to his duel stat instead of focusing ({Definition(weapon!).Name}).", command.PlayerId, weapon!.Id);
        duel.TurnPlayerId = 1 - command.PlayerId;
        _state.PriorityPlayerId = duel.TurnPlayerId;
    }


    // ---- Legion of Pain ----

    // "Actions performed by Personalities with a Gold Cost of 7 or lower may not target this Personality." RequireNotBarredFromTargeting's static half.
    private void RequireNotBarredByLegionOfPain(GameCommand command, AbilityDefinition ability)
    {
        if (!_state.Cards.Values.Any(c => c.Zone == Zone.Attached && Definition(c).Effect == LegionOfPainEffect)) return;
        if (PerformingCard(command, ability) is not { } actor || Definition(actor).Type != CardType.Personality || CardGoldCost(actor) > 7) return;
        foreach (var targetId in ActionTargetIds(command))
        {
            if (!_state.Cards.TryGetValue(targetId, out var target) || Definition(target).Type != CardType.Personality) continue;
            var legion = Attachments(target.Id).FirstOrDefault(a => Definition(a).Effect == LegionOfPainEffect);
            if (legion is not null) Require(false, $"{Definition(legion).Name}: actions performed by Personalities with a Gold Cost of 7 or lower may not target {Definition(target).Name}.");
        }
    }

    // ---- Blade of Perfection: "After this card bows: Straighten it" ----

    private void ApplyRemainingGAfterCommandTraits(CardInstance card, bool wasBowed)
    {
        if (Definition(card).Effect == BladeOfPerfectionEffect && card.Bowed && !wasBowed)
        {
            Straighten(card);
            Emit("bladeStraightened", $"{Definition(card).Name} straightened after bowing.", card.OwnerId, card.Id);
        }
    }

    // ---- Bandit Gang: "After a battle resolution ends, if this card is at the current battlefield: You may pay 2 Gold. If you do not, destroy this Follower." ----

    private static bool IsBanditGang(CardInstance card) => card.Zone == Zone.Attached && card.AttachedTo is not null;

    // A trait, so it resolves before any Reaction to the same moment (the after-resolution window). True when the battle's resolution is now in this trait's hands (it carries on from there).
    private bool BeginBanditGangTraits(PendingAfterResolution facts)
    {
        var gangs = _state.Cards.Values.Where(c => Definition(c).Effect == BanditGangEffect && IsBanditGang(c) && InPlay(c) && BanditGangAtBattlefield(c, facts.ProvinceIndex))
            .OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => c.Id).ToList();
        if (gangs.Count == 0) return false;
        _state.PendingBanditGangs = new PendingBanditGangs { FollowerIds = gangs, Facts = facts };
        RunBanditGangs();
        return true;
    }

    private bool BanditGangAtBattlefield(CardInstance gang, int provinceIndex) =>
        Card(gang.AttachedTo) is { Zone: Zone.Battlefield } host && host.ProvinceIndex == provinceIndex;

    private void RunBanditGangs()
    {
        var pending = _state.PendingBanditGangs!;
        while (pending.FollowerIds.Count > 0)
        {
            var gang = _state.Cards[pending.FollowerIds[0]];
            pending.FollowerIds.RemoveAt(0);
            if (!InPlay(gang) || !IsBanditGang(gang) || !BanditGangAtBattlefield(gang, pending.Facts.ProvinceIndex)) continue;
            if (OpenEitherOr(EmperorRemainingGSpecs.BanditGangPay, gang, gang.OwnerId, gang.OwnerId, null, "resolution")) return;
        }
        var facts = pending.Facts;
        _state.PendingBanditGangs = null;
        if (OpenAfterResolutionWindow(facts)) return;
        ContinueAfterResolution(facts.ProvinceIndex, facts.Attacker, facts.AttackerWon, facts.AttackerIds.Where(_state.Cards.ContainsKey).Select(Card).ToArray());
    }

    private string RemainingGEitherOrPrompt(PendingEitherOr p, string source) => p.SpecId switch
    {
        EmperorRemainingGSpecs.BanditGangPay => $"{source}: pay 2 Gold, or destroy this Follower.",
        _ => throw new RuleViolation("Unknown choice.")
    };

    private EitherOrOption[] RemainingGEitherOrOptions(PendingEitherOr p, CardInstance source, Func<int, bool> payable) => p.SpecId switch
    {
        EmperorRemainingGSpecs.BanditGangPay =>
        [
            new("pay", "Pay 2 Gold", "Nothing further happens.", EitherOrKind.Avoid, 2, payable(2), "You cannot produce 2 Gold."),
            new("destroy", "Do not pay", $"{Definition(source).Name} is destroyed.", EitherOrKind.Accept, 0, true, null)
        ],
        _ => throw new RuleViolation("Unknown choice.")
    };

    private bool ApplyRemainingGEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider, string[]? sourceIds)
    {
        switch (p.SpecId, option.Key)
        {
            case (EmperorRemainingGSpecs.BanditGangPay, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                Emit("eitherOrChosen", $"{decider.Name} paid 2 Gold ({Definition(source).Name}).", decider.Id, source.Id);
                return true;
            case (EmperorRemainingGSpecs.BanditGangPay, "destroy"):
                Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}): it is destroyed.", decider.Id, source.Id);
                if (InPlay(source) && source.Zone == Zone.Attached) DestroyAttachment(source);
                return true;
            default:
                return false;
        }
    }

    // FinishEitherOr's hook: Bandit Gang's answered decision hands the battle's resolution back.
    private bool FinishRemainingGEitherOr(PendingEitherOr p)
    {
        if (p.SpecId != EmperorRemainingGSpecs.BanditGangPay) return false;
        _state.PendingChoice = null;
        RunBanditGangs();
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingGHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingG", 1000, static abilities => abilities.AddEmperorRemainingG());
        EngineHooks.AttackStrengthBefore.Register("EmperorRemainingGAttackStrength", 300, static (engine, context) => engine.EmperorRemainingGAttackStrength(context.abilityId, context.sourceId));
        EngineHooks.AttackKind.Register("RemainingGAttackKind", 700, static context => NullIfEmpty(RemainingGAttackKind(context.abilityId)));
        EngineHooks.PersonalityForce.Register("EmperorRemainingGForce", 1400, static (engine, context) => engine.EmperorRemainingGForce(context.card));
        EngineHooks.ValidateLate.Register("ValidateEmperorRemainingG", 1000, static (engine, context) => engine.ValidateEmperorRemainingG(context.id, context.command, context.player, context.source));
        EngineHooks.EitherOrPrompt.Register("RemainingGEitherOrPrompt", 400, static (engine, p, source, affected) => p.SpecId == EmperorRemainingGSpecs.BanditGangPay ? engine.RemainingGEitherOrPrompt(p, source) : null);
        EngineHooks.EitherOrOptions.Register("RemainingGEitherOrOptions", 400, static (engine, p, affected, source, payable) => p.SpecId == EmperorRemainingGSpecs.BanditGangPay ? engine.RemainingGEitherOrOptions(p, source, payable) : null);
        EngineHooks.ApplyEitherOr.Register("ApplyRemainingGEitherOr", 400, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyRemainingGEitherOr(p, option, source, decider, sourceIds));
        EngineHooks.FinishEitherOr.Register("FinishRemainingGEitherOr", 300, static (engine, p) => engine.FinishRemainingGEitherOr(p));
    }
}
