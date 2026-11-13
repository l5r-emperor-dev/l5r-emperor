using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #835: twelve cards Mendon's rulings of 2026-10-11 (third batch) unblocked (docs/decisions/2026-10-11-twelve-ruled-cards.md; rulings R-063..R-074 in docs/rules/RULINGS.md). Ability ids are "w835-".
//
//   A Cleansing Breath       "Battle/Open: Target your Personality at any location. He centers himself. Negate other players' cards' effects that prevent him assigning, moving, or performing actions. If he is
//                            a Kensai, straighten his unit."   "He centers himself." is flavor text and does nothing. A Prevention immunity for the turn; the Kensai's unit straightens.
//   Imperial Deployment      "Open: Choose your performing Personality: ... Negate other players' cards' effects that prevent him assigning. After the next time this turn an attack is declared, straighten
//                            his unit."   An AssignPrevention immunity (assigning only: movement stays barred) and a mandatory straighten when the attack is declared this turn.
//   Warded Premises          "Before this battle's resolution: Negate all Force bonuses from actions to cards at this battlefield. / Battle: Even if you control no units at the current battlefield, if there
//                            is no Terrain in play there: Put this card into play there."   Only the bonuses actions gave (not permanent ones, attachments' or tokens').
//   Duty Over All Things     "Battle, :gstar:: If he would be opposed, Recruit your target performing discarded Yojimbo, paying 3 less Gold if you control a Courtier or Shugenja."   A discarded (not dead)
//                            Yojimbo, recruited at the current battlefield when an enemy Personality stands there.
//   Pierce the Tapestry      "You may include up to seven copies of this card in your deck. / Battle: Choose your performing unbowed Monk and target an enemy Personality or Follower: Give it -3F. If it has no
//                            attachments and you have played two or more other Pierce the Tapestry this battle, destroy it. If this did not destroy it, you may take an additional Battle action to play a
//                            Pierce the Tapestry."   A per-player, per-battle count of plays and an additional action scoped to the ability. Oracle of the Void prints the name "Pierce the Tapestery".
//   The Heavens' Blessing    "Open: Until the game ends, a player must pay 2 Gold to draw a card from effects of his own Strategies during an Action Phase. / Open: No player can gain Honor in Action Phases (this
//                            turn)."   A game-long flag; every card a player's own Strategy would draw in an Action Phase is owed, and each owed card is its own 2 Gold payment (a window after the action).
//   Unbearable Weight        "Limited, :g4:: Discard the top card of a Fate deck. You may target a Personality and give him a Chi penalty equal to the card's Focus Value. If this destroyed the target, his
//                            controller puts this Strategy in his hand."   The card is controlled by whoever holds it and returns to its owner's discard pile when it leaves play.
//   Merchant Patronage       "Political Interrupt: If the action is on another player's Holding, you choose who gains or loses any Honor from it. If you control a Courtier, you may bow him to increase the gain or
//                            loss by 1."   A Reaction in the existing before-Honor-change window (EmperorRulingR4Honor.cs), on a change an action of another player's Holding makes.
//   Blood of Isawa's Tribe   "After this Spell attaches, choose an element keyword (Air, Earth, Fire, Water, Void) in this Shugenja's keywords or text. While this Spell remains attached, replace all
//                            instances of that keyword on this Shugenja with a different element keyword that you choose now."   A mandatory trait: a two-step decision after the Spell attaches (the Shugenja's
//                            element keywords, then the five elements; the same element asks for a confirmation), with no Reaction window and no pass. P10's ReplaceElementKeyword does the replacing.
//   Revelations              "After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one of them, show the top five cards of your Dynasty deck. Choose another player who may
//                            discard one of the cards, put the rest back in any order, and refill this Province face-up."   A province-reveal Event (P5): the five cards are shown to both players, the other
//                            player may discard one (the shown-choice panel), its owner arranges the rest (the look panel), and the Event's Province refills face up.
//   The Empire's Foe Exposed "Limited: Target two Unique Personalities in play, exactly one of which is yours. Your Personality challenges the other Personality. Destroy the duel's loser."   Mendon: an Event
//                            that opens a duel when it resolves in the Events Phase; it does nothing unless each player has a Unique Personality in play; a player with exactly one is not asked.
//   The Egg of P'an Ku - exp "Limited: Bow one of your target unbowed Personalities. Remove this Item from the game. Create a copy of a target Personality, ignoring costs and restrictions. Bow the second target.
//                            He will not straighten, attack, or defend until after your next turn begins. Remove the copy from the game after your next turn begins."   Mendon: the Egg's controller controls the
//                            copy, which enters play in his home; the copied Personality (the second target, whoever's he is) is bowed and may not straighten, attack or defend until then.
public static class Wave835Abilities
{
    public const string CleansingBreath = "w835-cleansing-breath", ImperialDeployment = "w835-imperial-deployment", WardedNegate = "w835-warded-negate", WardedPlay = "w835-warded-play",
        DutyDiscount = "w835-duty-discount", DutyFull = "w835-duty-full", Pierce = "w835-pierce", HeavensDraw = "w835-heavens-draw", HeavensHonor = "w835-heavens-honor", HeavensPay = "w835-heavens-pay",
        Unbearable = "w835-unbearable", PatronageRedirect = "w835-patronage", PatronageBow = "w835-patronage-bow";
    public const string EggCard = "oracle-l5r-10522", Egg = "w835-egg", EggBowSlot = "bow", EggCopySlot = "copy", OwnUnbowedPersonality = "w835-own-unbowed-personality";
    public const string RevelationsCard = "oracle-l5r-10262", RevelationsEffect = "w835-revelations", RevelationsShown = "w835-revelations-shown", RevelationsOrder = "w835-revelations-order";
    public const string FoeCard = "oracle-l5r-10423", FoeEffect = "w835-foe", FoeDuel = "w835-foe-duel", FoeWindow = "w835Foe", FoePick = "w835FoePick";
    public const int RevelationsCount = 5;
    public const string BloodCard = "oracle-l5r-10347", BloodEffect = "w835-blood", BloodWindow = "w835Blood", BloodFrom = "w835BloodFrom", BloodTo = "w835BloodTo", BloodConfirm = "w835BloodConfirm", BloodCancel = "w835BloodCancel", BloodBack = "w835BloodBack";
    public const string HeavensTrigger = "w835-heavens-pay", HeavensWindow = "w835HeavensPay", HeavensDeclineOne = "w835HeavensDecline", HeavensDeclineAll = "w835HeavensDeclineAll";
    public const string DeckSlot = "deck", WeightTargetSlot = "target";
    /// <summary>The Gold each card drawn from an own Strategy costs while the Heavens' Blessing is in force.</summary>
    public const int HeavensPrice = 2;
    public const string YojimboDiscard = "w835-discarded-yojimbo";
    public const string CleansingBreathCard = "oracle-l5r-10358", ImperialDeploymentCard = "oracle-l5r-10378", WardedPremisesCard = "oracle-l5r-10404", DutyCard = "oracle-l5r-10370", PierceCard = "oracle-l5r-10388", HeavensCard = "oracle-l5r-10558", UnbearableCard = "oracle-l5r-10550", PatronageCard = "oracle-l5r-10720";

    /// <summary>The Gold Duty Over All Things takes off the recruit when its player controls a Courtier or a Shugenja.</summary>
    public const int DutyDiscountAmount = 3;
    /// <summary>"two or more other Pierce the Tapestry played this battle".</summary>
    public const int PierceOthersNeeded = 2;

    public static bool IsTerrainPlayAbility(string abilityId) => abilityId == WardedPlay;

    public static Dictionary<string, AbilityDefinition> AddWave835(this Dictionary<string, AbilityDefinition> all)
    {
        all[CleansingBreath] = new("BattleOrOpen", "Battle/Open: Target your Personality at any location. Negate other players' cards' effects that prevent him assigning, moving, or performing actions. If he is a Kensai, straighten his unit",
            Wave736Abilities.OwnPersonalityAnyLocation);
        all[ImperialDeployment] = new("Open", "Open: Choose your performing Personality: Negate other players' cards' effects that prevent him assigning. After the next time this turn an attack is declared, straighten his unit",
            "none", Performer: "own-personality");
        all[WardedNegate] = new("BeforeResolution", "Before this battle's resolution: Negate all Force bonuses from actions to cards at this battlefield", "none", Trigger: "before-battle-resolves");
        all[WardedPlay] = new("Battle", "Battle: Even if you control no units at the current battlefield, if there is no Terrain in play there: Put this card into play there", "none", Absent: true);
        all[DutyDiscount] = new("Battle", "Battle: If he would be opposed, Recruit your target performing discarded Yojimbo, paying 3 less Gold if you control a Courtier or Shugenja", YojimboDiscard,
            Cost: GoldCostRule.TargetRecruit(-DutyDiscountAmount, floor: 0));
        all[DutyFull] = new("Battle", "Battle: If he would be opposed, Recruit your target performing discarded Yojimbo", YojimboDiscard, Cost: GoldCostRule.TargetRecruit());
        all[Pierce] = new("Battle", "Battle: Choose your performing unbowed Monk and target an enemy Personality or Follower: Give it -3F. If it has no attachments and you have played two or more other Pierce the Tapestry this battle, destroy it. If this did not destroy it, you may take an additional Battle action to play a Pierce the Tapestry",
            "enemy-personality-or-follower", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"]);
        all[Egg] = new("Limited", "Limited: Bow one of your target unbowed Personalities. Remove this Item from the game. Create a copy of a target Personality, ignoring costs and restrictions. Bow the second target. He will not straighten, attack, or defend until after your next turn begins. Remove the copy from the game after your next turn begins",
            "slots", Slots: [new(EggBowSlot, OwnUnbowedPersonality, "Your unbowed Personality, to bow"), new(EggCopySlot, "personality", "The Personality to copy, bow and hold back")]);
        // The duel The Empire's Foe Exposed opens when it resolves: never on a card, only named by the Event. "Destroy the duel's loser" is its outcome (ApplyDuelOutcome).
        all[FoeDuel] = new("Limited", "Your Unique Personality challenges the other player's Unique Personality. Destroy the duel's loser", "none", IssuesDuel: true);
        all[HeavensDraw] = new("Open", "Open: Until the game ends, a player must pay 2 Gold to draw a card from effects of his own Strategies during an Action Phase", "none");
        all[HeavensHonor] = new("Open", "Open: No player can gain Honor in Action Phases (this turn)", "none");
        // The payment for one owed card: a player ability, offered only in the window the owed draws open, so the ordinary Gold payment (its sources, Sakuma, Koku...) pays it.
        all[HeavensPay] = new("Reaction", "The Heavens' Blessing: pay 2 Gold to draw a card from the effects of your own Strategy", "none", PlayerAbility: true, Trigger: HeavensTrigger, Cost: GoldCostRule.Fixed(HeavensPrice),
            UsesPerTurn: AbilityUseLimit.Unlimited);
        all[Unbearable] = new("Limited", "Limited: Discard the top card of a Fate deck. You may target a Personality and give him a Chi penalty equal to the card's Focus Value. If this destroyed the target, his controller puts this Strategy in his hand", "slots",
            Slots: [new(DeckSlot, "player", "A Fate deck: its owner's top card is discarded"), new(WeightTargetSlot, "personality", "A Personality to give a Chi penalty (optional)", 0, 1)]);
        all[PatronageRedirect] = new("Reaction", "Political Interrupt: If the action is on another player's Holding, you choose who gains or loses any Honor from it", "player", Trigger: HonorChangeReactions.Trigger, ActionKeywords: ["Political"]);
        all[PatronageBow] = new("Reaction", "Political Interrupt: If the action is on another player's Holding, you choose who gains or loses any Honor from it. Bow your performing Courtier to increase the gain or loss by 1", "player",
            Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Trigger: HonorChangeReactions.Trigger, Costs: ActionCost.BowPerformer, ActionKeywords: ["Political"]);
        return all;
    }
}

/// <summary>An Imperial Deployment waiting for the next attack declared this turn: his unit then straightens.</summary>
public sealed class DeploymentWatch
{
    public string EventId { get; set; } = "";
    public string PersonalityId { get; set; } = "";
    public int PlayerId { get; set; }
    public int Turn { get; set; }
}

/// <summary>A Merchant Patronage played on one Honor change: who takes it, and whether it is one larger (a Courtier bowed).</summary>
public sealed class PatronageMark
{
    public string ActionKey { get; set; } = "";
    public int Occurrence { get; set; }
    public int ToPlayer { get; set; }
    public int Bonus { get; set; }
}

/// <summary>An Unbearable Weight that destroyed its target: once it is in the discard pile, its target's controller puts it in his hand.</summary>
public sealed class WeightReturn
{
    public string CardId { get; set; } = "";
    public int ToPlayer { get; set; }
}

/// <summary>Blood of Isawa's Tribe's decision in progress: which Spell, who decides, the stage ("from", "to" or "confirm") and what has been chosen.</summary>
public sealed class BloodDecision
{
    public string SpellId { get; set; } = "";
    public int PlayerId { get; set; }
    public string Stage { get; set; } = "from";
    public string? From { get; set; }
    public string? To { get; set; }
}

/// <summary>Revelations in progress: the Event, its owner, its Province, and the ShownCard.Sequence of each of the cards shown from the top of the Dynasty deck, in deck order.</summary>
public sealed class RevelationsState
{
    public string EventId { get; set; } = "";
    public int OwnerId { get; set; }
    public int ProvinceIndex { get; set; }
    public List<int> Sequences { get; set; } = [];
}

/// <summary>The Empire's Foe Exposed in progress: the Event, its owner, the two Personalities (null until chosen), and whether the duel has been issued.</summary>
public sealed class FoeState
{
    public string EventId { get; set; } = "";
    public int OwnerId { get; set; }
    public string? ChallengerId { get; set; }
    public string? ChallengedId { get; set; }
    public bool DuelStarted { get; set; }
}

/// <summary>A Province that refills face up the next time it refills (Revelations).</summary>
public sealed class FaceUpRefill
{
    public int PlayerId { get; set; }
    public int ProvinceIndex { get; set; }
}

public sealed class Wave835State
{
    public RevelationsState? Revelations { get; set; }
    public FoeState? Foe { get; set; }
    public List<FaceUpRefill> FaceUpRefills { get; set; } = [];
    /// <summary>Blood of Isawa's Tribe Spells that attached and whose decision has not been asked yet, in the order they attached.</summary>
    public List<string> BloodQueue { get; set; } = [];
    public BloodDecision? Blood { get; set; }
    public List<DeploymentWatch> Deployments { get; set; } = [];
    /// <summary>The Heavens' Blessing has been played: until the game ends (never cleared).</summary>
    public bool HeavensActive { get; set; }
    /// <summary>Cards a player's own Strategy would have drawn in an Action Phase, each still to be paid for (player id to count).</summary>
    public Dictionary<int, int> HeavensOwed { get; set; } = [];
    public List<PatronageMark> Patronage { get; set; } = [];
    public List<WeightReturn> WeightReturns { get; set; } = [];
    /// <summary>The priority and pass count a window opened after a command took over, restored when it closes.</summary>
    public int? ResumePriority { get; set; }
    public int ResumePasses { get; set; }
}

public sealed partial class MatchState
{
    // 23600-23699 is reserved for issue #835.
    [JsonPropertyOrder(23600)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Wave835State? Wave835 { get; set; }
}

public sealed partial class MatchEngine
{
    private Wave835State W835 => _state.Wave835 ??= new();

    // ---- targets ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // "Your discarded (not dead) Yojimbo": a Personality of the player's in his Dynasty or Fate discard pile (Zone.Dead is the dead pile: Dark Virtue's At Any Cost takes those).
    private bool Wave835YojimboCandidate(int playerId, CardInstance c) =>
        c.OwnerId == playerId && Definition(c).Type == CardType.Personality && Keyword(c, "Yojimbo") && c.Zone is Zone.DynastyDiscard or Zone.FateDiscard;

    internal static bool IsWave835OutOfPlayTarget(string category) => category == Wave835Abilities.YojimboDiscard;

    private string?[]? Wave835Candidates(string category, int playerId) => category != Wave835Abilities.YojimboDiscard ? null
        : _state.Cards.Values.Where(c => Wave835YojimboCandidate(playerId, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    private bool ControlsCourtierOrShugenja(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && (Keyword(c, "Courtier") || Keyword(c, "Shugenja")));

    // ---- validation ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave835Validate(ValidationHookContext context)
    {
        var (id, command, player) = (context.id, context.command, context.player);
        switch (id)
        {
            // "If he would be opposed": an enemy Personality stands at the battlefield the Yojimbo would enter (Mendon: judged when the card is played).
            case Wave835Abilities.DutyDiscount:
            case Wave835Abilities.DutyFull:
                Require(_state.CurrentBattleProvince is int battlefield && _state.Cards.Values.Any(c => c.OwnerId != player.Id && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && Definition(c).Type == CardType.Personality),
                    "No enemy Personality is at the current battlefield, so he would not be opposed.");
                // The same printed action, in two prices: three less Gold when the player controls a Courtier or Shugenja, the plain recruit otherwise.
                Require((id == Wave835Abilities.DutyDiscount) == ControlsCourtierOrShugenja(player.Id), id == Wave835Abilities.DutyDiscount ? "You control no Courtier or Shugenja: the recruit is not discounted." : "You control a Courtier or Shugenja: the recruit is three Gold cheaper.");
                if (command.TargetId is { } yojimboId && _state.Cards.TryGetValue(yojimboId, out var yojimbo))
                {
                    Require(Wave835YojimboCandidate(player.Id, yojimbo), "Choose a Yojimbo Personality of yours in your discard pile (not dead).");
                    var quote = QuotePurchase(player.Id, yojimbo.Id, command.ClanDiscount, command.ClanAllegiance, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                }
                break;
            // One owed card: only in the window the owed draws opened, for the player they are owed by.
            case Wave835Abilities.HeavensPay:
                Require(_state.PendingChoice is { Type: Wave835Abilities.HeavensWindow } window && window.PlayerId == player.Id && W835.HeavensOwed.GetValueOrDefault(player.Id) > 0, "No card drawn from your own Strategy is waiting to be paid for.");
                break;
            // Merchant Patronage: judged against the Honor change it is played on (the window's own held change).
            case Wave835Abilities.PatronageRedirect:
            case Wave835Abilities.PatronageBow:
                Require(_state.PendingHonorChange is { } change && Wave835PatronageApplies(player.Id, change), "Merchant Patronage is played on an Honor change an action of another player's Holding makes.");
                Require(command.TargetPlayerId is 0 or 1, "Choose who gains or loses the Honor.");
                break;
            // Warded Premises' second ability: "if there is no Terrain in play there", from the hand.
            case Wave835Abilities.WardedPlay:
                Require(context.source is { Zone: Zone.Hand }, "Warded Premises is put into play from your hand.");
                Require(!TerrainAtCurrentBattlefield() || MarshallingFieldsWaiverAvailable(player.Id), "There is already a Terrain in play at the current battlefield.");
                break;
        }
    }

    // ---- effects -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // TargetAllows: "one of your target unbowed Personalities".
    private bool Wave835TargetAllows(TargetAllowsContext context) =>
        context.ability.Target == Wave835Abilities.OwnUnbowedPersonality && InPlay(context.target) && Definition(context.target).Type == CardType.Personality && context.target.OwnerId == context.player.Id && !context.target.Bowed;

    private bool? RunWave835(RunHookContext c)
    {
        var (id, command, player, source, target) = (c.id, c.command, c.player, c.source, c.target);
        switch (id)
        {
            // "He centers himself." is flavor text (Mendon). The negation is a Prevention immunity until the end of the turn; "If he is a Kensai, straighten his unit."
            case Wave835Abilities.CleansingBreath:
                GrantImmunity(target!, ImmunityKind.Prevention);
                Emit("immunityGranted", $"{Definition(target!).Name}: other players' cards' effects that prevent him assigning, moving or performing actions are negated this turn.", player.Id, target!.Id);
                if (Keyword(target!, "Kensai")) StraightenUnit(target!);
                return false;
            // Only the effects that stop him assigning are negated (he may still be barred from moving); the straightening is a mandatory watch fired when an attack is declared this turn.
            case Wave835Abilities.ImperialDeployment:
                {
                    var deployed = Card(command.PerformerId!);
                    GrantImmunity(deployed, ImmunityKind.AssignPrevention);
                    W835.Deployments.RemoveAll(w => w.Turn != _state.TurnNumber);
                    W835.Deployments.Add(new DeploymentWatch { EventId = source!.Id, PersonalityId = deployed.Id, PlayerId = player.Id, Turn = _state.TurnNumber });
                    Emit("immunityGranted", $"{Definition(deployed).Name}: other players' cards' effects that prevent him assigning are negated this turn; his unit straightens after the next attack is declared.", player.Id, deployed.Id);
                    return false;
                }
            case Wave835Abilities.WardedPlay:
                if (TerrainAtCurrentBattlefield()) SpendMarshallingFieldsWaiver(player.Id, source!);
                PutTerrainIntoPlay(player, source!);
                return false;
            case Wave835Abilities.DutyDiscount:
            case Wave835Abilities.DutyFull:
                {
                    var yojimbo = target!;
                    var battlefield = _state.CurrentBattleProvince!.Value;
                    var quote = QuotePurchase(player.Id, yojimbo.Id, command.ClanDiscount, command.ClanAllegiance, fromDeadPile: true);
                    Require(quote.Error is null && Definition(yojimbo).Type == CardType.Personality, quote.Error ?? "");
                    SpendHandPlayedCard(player, source);
                    return BringDeadPersonalityIntoPlay(player, yojimbo, Definition(source!).Name, battlefield);
                }
            case Wave835Abilities.Pierce:
                {
                    // "you have played two or more OTHER Pierce the Tapestry this battle": the count of this player's plays before this one.
                    var key = "w835-pierce:" + player.Id;
                    var others = PeriodCountOf(key, Period.Battle);
                    AddPeriodCount(key, Period.Battle);
                    ApplyModifier(target!, source!.Id, id, "Force", -3);
                    var destroyed = false;
                    if (InPlay(target!) && others >= Wave835Abilities.PierceOthersNeeded && !Attachments(target!.Id).Any())
                    {
                        if (Definition(target).Type == CardType.Follower) DestroyAttachment(target);
                        else DestroyUnits([target], false, player.Id);
                        destroyed = !InPlay(target);
                    }
                    if (!destroyed) GrantAdditionalAction(player.Id, null, scopeAbilityIds: [Wave835Abilities.Pierce]);
                    return false;
                }
            // The Egg: the first target bows; the Item leaves the game; a copy of the second target is created for the Egg's controller (his home, no costs or restrictions; it leaves after his next turn begins);
            // the second target bows and neither straightens, attacks nor defends until then, whoever controls him.
            case Wave835Abilities.Egg:
                {
                    var bowed = SlotCards(command, Wave835Abilities.EggBowSlot).First();
                    var original = SlotCards(command, Wave835Abilities.EggCopySlot).First();
                    var name = Definition(source!).Name;
                    if (InPlay(bowed) && !StalwartNegatesBow(bowed, player.Id)) bowed.Bowed = true;
                    RemoveFromGame(source!);
                    Emit("eggHatches", $"{name} is removed from the game: {player.Name} creates a copy of {Definition(original).Name}.", player.Id, original.Id);
                    var copy = new CreatedCardSpec(CardType.Personality, CopyOfTarget: true) { RemoveWhen = CreatedRemoval.NextTurnBegins };
                    return ExecuteCreate(c.ability, copy, command with { TargetId = original.Id }, player, beforeWindow: () => Wave835EggHold(original, player.Id, name));
                }
            case Wave835Abilities.HeavensDraw:
                W835.HeavensActive = true;
                Emit("heavensBlessing", $"{Definition(source!).Name}: until the game ends, a player must pay 2 Gold to draw a card from effects of his own Strategies during an Action Phase.", player.Id, source!.Id);
                return false;
            // "No player can gain Honor in Action Phases (this turn)": the P14 no-gain modifier with the Action-Phase filter, for either player.
            case Wave835Abilities.HeavensHonor:
                AddHonorModifier(new HonorModifier { OwnerPlayerId = player.Id, AffectedPlayerId = null, Kind = HonorModifierKinds.NoGain, Filters = [HonorModifierFilters.ActionPhaseOnly], UntilTurn = _state.TurnNumber, SourceCardId = source!.Id });
                Emit("heavensBlessing", $"{Definition(source!).Name}: no player can gain Honor in Action Phases this turn.", player.Id, source!.Id);
                return false;
            // One owed card, paid for (the Gold was paid as the ability's cost): it is drawn now, outside the interception.
            case Wave835Abilities.HeavensPay:
                {
                    W835.HeavensOwed[player.Id] = Math.Max(0, W835.HeavensOwed.GetValueOrDefault(player.Id) - 1);
                    _w835Paying = true;
                    try { Draw(player); } finally { _w835Paying = false; }
                    return false;
                }
            case Wave835Abilities.Unbearable:
                {
                    var deckOwner = Player(SlotPlayer(command, Wave835Abilities.DeckSlot)!.Value);
                    var focus = DiscardTopOfFateDeck(deckOwner);
                    if (focus is int penalty && penalty > 0 && SlotCards(command, Wave835Abilities.WeightTargetSlot).FirstOrDefault() is { } victim && InPlay(victim))
                    {
                        var controller = victim.OwnerId;
                        ApplyModifier(victim, source!.Id, id, "Chi", -penalty);
                        if (!InPlay(victim) && !W835.WeightReturns.Any(r => r.CardId == source.Id))
                        {
                            W835.WeightReturns.Add(new WeightReturn { CardId = source.Id, ToPlayer = controller });
                            Emit("weightDestroyed", $"{Definition(source).Name} destroyed {Definition(victim).Name}: {Player(controller).Name} puts it in his hand.", player.Id, source.Id);
                        }
                    }
                    return false;
                }
            // Merchant Patronage: the player who takes this Honor change instead of the one it was for (or the same player), and a Courtier bowed for one more.
            case Wave835Abilities.PatronageRedirect:
            case Wave835Abilities.PatronageBow:
                {
                    var held = _state.PendingHonorChange!;
                    var chosen = command.TargetPlayerId!.Value;
                    W835.Patronage.RemoveAll(m => m.ActionKey == held.ActionKey && m.Occurrence == held.Occurrence);
                    W835.Patronage.Add(new PatronageMark { ActionKey = held.ActionKey, Occurrence = held.Occurrence, ToPlayer = chosen, Bonus = id == Wave835Abilities.PatronageBow ? 1 : 0 });
                    Emit("patronageQueued", $"{Definition(source!).Name}: {Player(chosen).Name} will {(held.Amount > 0 ? "gain" : "lose")} the Honor{(id == Wave835Abilities.PatronageBow ? ", increased by 1" : "")}.", player.Id, source!.Id);
                    return false;
                }
            default:
                return null;
        }
    }

    // The Egg's second half: the copied Personality bows and may not straighten, attack or defend until after the Egg's controller's next turn begins (a straighten hold through his Straighten Phase, as
    // Aramasu's Legacy's, and attack and defend restrictions that end as his next turn begins).
    private void Wave835EggHold(CardInstance original, int ownerId, string name)
    {
        if (!InPlay(original)) return;
        if (!StalwartNegatesBow(original, ownerId)) original.Bowed = true;
        AddStraightenHold(original, name, ownerId, StraightenPoint.StraightenPhaseEnds);
        AddCardRestriction(RestrictionKind.NoAttack, original, RestrictionUntil.OwnersNextTurnBegins, ownerId: ownerId);
        AddCardRestriction(RestrictionKind.NoDefend, original, RestrictionUntil.OwnersNextTurnBegins, ownerId: ownerId);
        Emit("eggHold", $"{Definition(original).Name} is bowed and will not straighten, attack or defend until after {Player(ownerId).Name}'s next turn begins ({name}).", ownerId, original.Id);
    }

    // ---- Merchant Patronage ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The window's judgement (HonorChangeReactionApplies): the change is made by an action of a Holding that another player controls than the one playing the Reaction.
    private bool Wave835PatronageApplies(int reactorId, PendingHonorChange change)
    {
        var parts = change.ActionKey.Split('|');
        return parts.Length >= 3 && _state.Cards.TryGetValue(parts[2], out var holding) && Definition(holding).Type == CardType.Holding && holding.OwnerId != reactorId;
    }

    private bool? Wave835HonorChangeApplies(CardInstance card, string abilityId, PendingHonorChange change)
    {
        if (abilityId is not (Wave835Abilities.PatronageRedirect or Wave835Abilities.PatronageBow)) return null;
        if (!Wave835PatronageApplies(card.OwnerId, change)) return false;
        return abilityId == Wave835Abilities.PatronageRedirect || BeforeActionReactionPerformers(card.OwnerId, AbilityCatalog.All[abilityId]).Any();
    }

    // The HonorChange hook, at the stage where the change lands: the marked change goes to the chosen player and is one larger when a Courtier was bowed. A mark belongs to its action and its occurrence.
    private HonorChangeResult? Wave835PatronageApply(HonorChangeContext context)
    {
        if (context.Stage != HonorChangeStage.Applied || context.ActionKey is not { } key || _state.Wave835 is not { Patronage.Count: > 0 } w) return null;
        var mark = w.Patronage.FirstOrDefault(m => m.ActionKey == key && m.Occurrence == _honorChangeOccurrence - 1);
        if (mark is null) return null;
        w.Patronage.Remove(mark);
        var amount = context.Amount + Math.Sign(context.Amount) * mark.Bonus;
        return new HonorChangeResult(amount, mark.ToPlayer != context.PlayerId ? mark.ToPlayer : null);
    }

    // ---- The Heavens' Blessing: the owed draws ------------------------------------------------------------------------------------------------------------------------------------------

    private bool _w835Paying;

    // Draw's first step: once the Blessing is in force, a card a player's own Strategy draws in an Action Phase is not drawn but owed (Mendon: each card is its own instance and its own 2 Gold payment). The
    // other player's draw an own Strategy grants him, a Holding's draw (Traveling Peddler), a Focus Effect's and the rulebook's are not "effects of his own Strategies" and are untouched.
    private bool Wave835DefersDraw(PlayerState player)
    {
        if (_state.Wave835 is not { HeavensActive: true } w || _w835Paying || _state.Phase != Phase.Action) return false;
        if (!CurrentActionFacts(out var key, out var actorId) || actorId != player.Id) return false;
        var parts = key.Split('|');
        if (parts.Length < 3 || !_state.Cards.TryGetValue(parts[2], out var source) || Definition(source).Type != CardType.Strategy) return false;
        w.HeavensOwed[player.Id] = w.HeavensOwed.GetValueOrDefault(player.Id) + 1;
        Emit("drawOwed", $"{player.Name}: the Heavens' Blessing means a card drawn from {Definition(source).Name} costs 2 Gold.", player.Id, source.Id);
        return true;
    }

    // ---- Revelations -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // P5's resolve: the five cards are shown to both players (the public shown-cards record), the other player may discard one (the shown-choice panel); what is left its owner arranges (the look panel), and the
    // Event is discarded and its Province refills face up. It is called once its Event is face up in a Province after the owner's reveal.
    private RevealEventAsk? Wave835RevelationsResolve(RevealedEvent revealed)
    {
        var card = revealed.Card; var owner = Player(card.OwnerId);
        var shown = owner.DynastyDeck.Take(Wave835Abilities.RevelationsCount).Select(Card).ToArray();
        if (shown.Length == 0)
        {
            Emit("revelations", $"{Definition(card).Name}: {owner.Name}'s Dynasty deck is empty; there is nothing to show.", owner.Id, card.Id);
            Wave835FinishRevelations(card, owner.Id, revealed.Province.Index);
            return null;
        }
        var sequences = new List<int>();
        foreach (var top in shown) { RecordShown(owner.Id, top, card, SearchDeck.Dynasty, "shows"); sequences.Add(_state.ShownCards.Count); }
        W835.Revelations = new RevelationsState { EventId = card.Id, OwnerId = owner.Id, ProvinceIndex = revealed.Province.Index, Sequences = sequences };
        OpenShownChoice(Wave835Abilities.RevelationsShown, card, owner.Id, 1 - owner.Id, sequences, 0, 1,
            $"{Definition(card).Name}: {owner.Name} shows the top {shown.Length} cards of their Dynasty deck. You may discard one of them.");
        return null;
    }

    // What the other player decided: the chosen card (if any) is discarded from the deck, then its owner arranges the others, or the Event ends at once when there is nothing to arrange.
    private bool Wave835ShownApply(PendingShownChoice choice)
    {
        if (choice.SpecId != Wave835Abilities.RevelationsShown) return false;
        var r = W835.Revelations ?? throw new RuleViolation("No Revelations is open.");
        var owner = Player(r.OwnerId); var card = Card(r.EventId);
        var remaining = r.Sequences.Count;
        if (choice.Chosen.Count > 0)
        {
            var index = r.Sequences.IndexOf(choice.Chosen[0]);
            if (index >= 0 && index < owner.DynastyDeck.Count)
            {
                var discarded = Card(owner.DynastyDeck[index]);
                owner.DynastyDeck.RemoveAt(index);
                discarded.Zone = Zone.DynastyDiscard; discarded.FaceUp = true;
                Emit("cardDiscarded", $"{Player(choice.DeciderPlayerId).Name} had {owner.Name} discard {Definition(discarded).Name} from the top of the Dynasty deck ({Definition(card).Name}).", choice.DeciderPlayerId, discarded.Id);
                remaining--;
            }
        }
        else Emit("revelations", $"{Player(choice.DeciderPlayerId).Name} chose not to discard any of the cards.", choice.DeciderPlayerId, card.Id);
        W835.Revelations = null;
        if (remaining >= 2)
        {
            W835.FaceUpRefills.Add(new FaceUpRefill { PlayerId = owner.Id, ProvinceIndex = r.ProvinceIndex });
            BeginLook(LookById(Wave835Abilities.RevelationsOrder), card, owner.Id, "event", count: remaining);
        }
        else Wave835FinishRevelations(card, owner.Id, r.ProvinceIndex);
        return true;
    }

    private static LookSpec LookById(string id) => LookCatalog.All[id];

    // Nothing to arrange (or nothing to show): the Event is discarded, its Province refills face up, and the Events Phase goes on.
    private void Wave835FinishRevelations(CardInstance card, int ownerId, int provinceIndex)
    {
        W835.FaceUpRefills.Add(new FaceUpRefill { PlayerId = ownerId, ProvinceIndex = provinceIndex });
        _state.PendingChoice = null;
        ResumeEventAfterDeferral(ownerId, card.Id);
    }

    private bool? Wave835RefillFaceUp(RefillContext context)
    {
        if (_state.Wave835 is not { FaceUpRefills.Count: > 0 } w) return null;
        var mark = w.FaceUpRefills.FirstOrDefault(m => m.PlayerId == context.Player.Id && m.ProvinceIndex == context.Province.Index);
        if (mark is null) return null;
        w.FaceUpRefills.Remove(mark);
        return true;
    }

    // ---- The Empire's Foe Exposed -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private CardInstance[] Wave835Uniques(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Unique")).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();

    // ResolveEvent's hook (true: a decision or the duel is open and the Events Phase waits; null: not this Event, or it resolves with no effect and is discarded as any Event is).
    private bool? ResolveWave835Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (Definition(card).Effect != Wave835Abilities.FoeEffect) return null;
        var mine = Wave835Uniques(player.Id); var theirs = Wave835Uniques(1 - player.Id);
        if (mine.Length == 0 || theirs.Length == 0)
        {
            Emit("foeFizzles", $"{Definition(card).Name}: {(mine.Length == 0 ? player.Name : Player(1 - player.Id).Name)} has no Unique Personality in play; it is discarded without effect.", player.Id, card.Id);
            return null;
        }
        W835.Foe = new FoeState { EventId = card.Id, OwnerId = player.Id, ChallengerId = mine.Length == 1 ? mine[0].Id : null, ChallengedId = theirs.Length == 1 ? theirs[0].Id : null };
        return Wave835FoeAdvance();
    }

    // Asks whichever side still has a choice to make, or starts the duel. True while something is open.
    private bool Wave835FoeAdvance()
    {
        var foe = W835.Foe!;
        var card = Card(foe.EventId);
        if (foe.ChallengerId is null) { OpenFoePick(foe, foe.OwnerId); return true; }
        if (foe.ChallengedId is null) { OpenFoePick(foe, 1 - foe.OwnerId); return true; }
        foe.DuelStarted = true;
        Emit("foeDuel", $"{Definition(card).Name}: {Definition(Card(foe.ChallengerId)).Name} challenges {Definition(Card(foe.ChallengedId)).Name} to a duel.", foe.OwnerId, card.Id);
        IssueChallenge(Wave835Abilities.FoeDuel, foe.OwnerId, foe.ChallengerId, foe.ChallengedId, "Chi", false, card);
        // A challenge that was not issued ("will not challenge") leaves no duel: the Event is over.
        if (_state.PendingDuel is null && _state.PendingChoice is null) { W835.Foe = null; _state.PendingChoice = null; ResumeEventAfterDeferral(foe.OwnerId, card.Id); }
        return true;
    }

    private void OpenFoePick(FoeState foe, int playerId)
    {
        var card = Card(foe.EventId);
        _state.PendingChoice = new(Wave835Abilities.FoeWindow, playerId, card.Id,
            $"{Definition(card).Name}: choose your Unique Personality for the duel{(playerId == foe.OwnerId ? " (yours challenges)" : " (you are challenged)")}.");
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
    }

    private bool HandleWave835Foe(string type, GameCommand command)
    {
        if (type != Wave835Abilities.FoeWindow || W835.Foe is not { } foe) return false;
        var pending = _state.PendingChoice!;
        Require(command.Type == Wave835Abilities.FoePick && command.PlayerId == pending.PlayerId, "Choose one of your Unique Personalities.");
        Require(command.TargetId is not null && Wave835Uniques(pending.PlayerId).Any(c => c.Id == command.TargetId), "Choose one of your Unique Personalities in play.");
        if (pending.PlayerId == foe.OwnerId) foe.ChallengerId = command.TargetId; else foe.ChallengedId = command.TargetId;
        _state.PendingChoice = null;
        Wave835FoeAdvance();
        return true;
    }

    private IEnumerable<ActionOption>? Wave835FoeActions(PendingActionsContext context)
    {
        if (context.Choice.Type != Wave835Abilities.FoeWindow) return null;
        if (context.PlayerId != context.Choice.PlayerId) return [];
        return Wave835Uniques(context.PlayerId).Select(c => new ActionOption(Wave835Abilities.FoePick, $"{Definition(c).Name}", true, null,
            new GameCommand(Wave835Abilities.FoePick, context.PlayerId, _state.Version, TargetId: c.Id), c.Id, DecisionCandidateIds: [c.Id],
            DecisionLabel: context.PlayerId == W835.Foe?.OwnerId ? $"{Definition(c).Name} challenges" : $"{Definition(c).Name} is challenged")).ToArray();
    }

    // NextAction's first step: the Foe duel has ended (and every window it opened has closed), so the Event is discarded, its Province refills and the Events Phase goes on instead of an Action Phase action.
    private bool Wave835FoeDuelEnded()
    {
        if (_state.Wave835?.Foe is not { DuelStarted: true } foe || _state.PendingDuel is not null) return false;
        W835.Foe = null;
        ResumeEventAfterDeferral(foe.OwnerId, foe.EventId);
        return true;
    }

    // ---- Blood of Isawa's Tribe -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // ApplyMandatoryEntryEffects' hook: the Spell attached (by any path), so its decision is owed. It opens when the command is done and nothing else is open (no Reaction window of its own).
    private void ApplyWave835EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect == Wave835Abilities.BloodEffect && card.Zone == Zone.Attached && !W835.BloodQueue.Contains(card.Id)) W835.BloodQueue.Add(card.Id);
    }

    private string[] BloodElements(CardInstance host) => ElementKeywordList.Where(e => Keyword(host, e)).ToArray();

    private bool OpenNextBlood(Wave835State w)
    {
        while (w.BloodQueue.Count > 0)
        {
            var spellId = w.BloodQueue[0]; w.BloodQueue.RemoveAt(0);
            if (!_state.Cards.TryGetValue(spellId, out var spell) || spell.Zone != Zone.Attached || spell.AttachedTo is not { } hostId || !_state.Cards.TryGetValue(hostId, out var host) || !InPlay(host)) continue;
            var elements = BloodElements(host);
            if (elements.Length == 0)
            {
                Emit("bloodNothing", $"{Definition(spell).Name}: {Definition(host).Name} has no element keyword to replace.", spell.OwnerId, spell.Id);
                continue;
            }
            w.Blood = new BloodDecision { SpellId = spell.Id, PlayerId = spell.OwnerId, Stage = "from" };
            w.ResumePriority = _state.PriorityPlayerId; w.ResumePasses = _state.ConsecutivePasses;
            _state.PendingChoice = new(Wave835Abilities.BloodWindow, spell.OwnerId, spell.Id, BloodPrompt(w.Blood, host));
            _state.PriorityPlayerId = spell.OwnerId; _state.ConsecutivePasses = 0;
            return true;
        }
        return false;
    }

    private string BloodPrompt(BloodDecision d, CardInstance host) => d.Stage switch
    {
        "from" => $"{Definition(Card(d.SpellId)).Name}: choose an element keyword of {Definition(host).Name} to replace.",
        "to" => $"{Definition(Card(d.SpellId)).Name}: choose the element keyword that replaces {d.From} on {Definition(host).Name} while the Spell remains attached.",
        _ => $"{Definition(Card(d.SpellId)).Name}: replace {d.From} with {d.From}? You chose the same keyword."
    };

    private bool HandleWave835Blood(string type, GameCommand command)
    {
        if (type != Wave835Abilities.BloodWindow || W835.Blood is not { } d) return false;
        Require(command.PlayerId == d.PlayerId, "Only the Spell's controller decides.");
        var spell = Card(d.SpellId); var host = Card(spell.AttachedTo!);
        var choice = command.Choice;
        switch (command.Type)
        {
            case Wave835Abilities.BloodFrom when d.Stage == "from":
                Require(choice is not null && BloodElements(host).Contains(choice), "Choose one of the Shugenja's element keywords.");
                d.From = choice; d.Stage = "to";
                break;
            case Wave835Abilities.BloodBack when d.Stage == "to":
                d.From = null; d.Stage = "from";
                break;
            case Wave835Abilities.BloodTo when d.Stage == "to":
                Require(choice is not null && ElementKeywordList.Contains(choice), "Choose one of the five elements.");
                d.To = choice;
                if (choice == d.From) { d.Stage = "confirm"; break; }
                Wave835FinishBlood(d, spell, host);
                return true;
            case Wave835Abilities.BloodConfirm when d.Stage == "confirm":
                Wave835FinishBlood(d, spell, host);
                return true;
            case Wave835Abilities.BloodCancel when d.Stage == "confirm":
                d.To = null; d.Stage = "to";
                break;
            default:
                throw new RuleViolation("Answer the Spell's question.");
        }
        _state.PendingChoice = _state.PendingChoice! with { Prompt = BloodPrompt(d, host) };
        _state.PriorityPlayerId = d.PlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    private void Wave835FinishBlood(BloodDecision d, CardInstance spell, CardInstance host)
    {
        if (d.To != d.From && d.From is { } from && d.To is { } to)
        {
            ReplaceElementKeyword(host.Id, from, to, spell.Id);
            Emit("elementReplaced", $"{Definition(spell).Name}: while it remains attached, {Definition(host).Name}'s {from} is {to}.", spell.OwnerId, host.Id);
        }
        else Emit("elementReplaced", $"{Definition(spell).Name}: {Definition(host).Name}'s {d.From} is replaced with {d.From}: nothing changes.", spell.OwnerId, host.Id);
        W835.Blood = null;
        _state.PendingChoice = null;
        Wave835Resume();
    }

    private IEnumerable<ActionOption>? Wave835BloodActions(PendingActionsContext context)
    {
        if (context.Choice.Type != Wave835Abilities.BloodWindow || W835.Blood is not { } d) return null;
        if (context.PlayerId != d.PlayerId) return [];
        var spell = Card(d.SpellId); var host = Card(spell.AttachedTo!);
        ActionOption Option(string type, string label, string? choice = null) => new(type, label, true, null, new GameCommand(type, context.PlayerId, _state.Version, Choice: choice), DecisionLabel: label);
        switch (d.Stage)
        {
            case "from":
                return BloodElements(host).Select(e => Option(Wave835Abilities.BloodFrom, $"Replace {e}", e)).ToArray();
            case "to":
                {
                    var options = ElementKeywordList.Select(e => Option(Wave835Abilities.BloodTo, e == d.From ? $"{e} (the same keyword)" : $"Replace {d.From} with {e}", e)).ToList();
                    if (BloodElements(host).Length > 1) options.Add(Option(Wave835Abilities.BloodBack, "Choose a different keyword to replace"));
                    return options;
                }
            default:
                return [Option(Wave835Abilities.BloodConfirm, $"Yes, replace {d.From} with {d.From}"), Option(Wave835Abilities.BloodCancel, "No, choose another element")];
        }
    }

    private bool Wave835WindowOpen(GameCommand command) =>
        _state.PendingChoice is { Type: Wave835Abilities.HeavensWindow } pending && command.PlayerId == pending.PlayerId;

    // Run after every command: a Strategy's put-back to its new controller's hand, and the first player with owed cards gets his window (nothing opens in the middle of an effect).
    private void Wave835AfterCommand()
    {
        if (_state.Wave835 is not { } w) return;
        foreach (var back in w.WeightReturns.ToArray())
        {
            if (!_state.Cards.TryGetValue(back.CardId, out var strategy) || strategy.Zone is Zone.Hand or Zone.RemovedFromGame) { w.WeightReturns.Remove(back); continue; }
            if (strategy.Zone != Zone.FateDiscard) continue; // not spent yet: it follows once the action is over
            w.WeightReturns.Remove(back);
            if (P2CardIntoHandBlocked(back.ToPlayer)) continue;
            strategy.OriginalOwnerId ??= strategy.OwnerId;
            strategy.OwnerId = back.ToPlayer;
            if (strategy.OriginalOwnerId == back.ToPlayer) strategy.OriginalOwnerId = null;
            strategy.Zone = Zone.Hand; strategy.FaceUp = false; ResetCardState(strategy); // a card in a hand is a fresh object: this play's once-per-turn stamp is not carried over
            Player(back.ToPlayer).Hand.Add(strategy.Id);
            NoteCardIntoHand(back.ToPlayer);
            Emit("strategyTaken", $"{Player(back.ToPlayer).Name} put {Definition(strategy).Name} in his hand.", back.ToPlayer, strategy.Id);
        }
        if (w.BloodQueue.Count > 0 && _state.WinnerId is null && _state.PendingChoice is null && _state.PendingDuel is null && _state.PendingAttack is null && _state.PendingBeforeBattleResolve is null
            && _state.PendingNegatableAbility is null && _state.PendingHonorChange is null && OpenNextBlood(w)) return;
        if (w.HeavensOwed.Count == 0 || _state.WinnerId is not null || _state.PendingChoice is not null || _state.PendingDuel is not null || _state.PendingAttack is not null
            || _state.PendingBeforeBattleResolve is not null || _state.PendingNegatableAbility is not null || _state.PendingHonorChange is not null) return;
        foreach (var (playerId, owed) in w.HeavensOwed.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key).ToArray())
        {
            var window = new PendingChoice(Wave835Abilities.HeavensWindow, playerId, null, Wave835Prompt(owed));
            if (!Wave835CanPay(playerId, window))
            {
                w.HeavensOwed.Remove(playerId);
                Emit("drawLost", $"{Player(playerId).Name} cannot pay 2 Gold: {owed} card{(owed == 1 ? "" : "s")} drawn from his own Strategy {(owed == 1 ? "is" : "are")} not drawn.", playerId);
                continue;
            }
            w.ResumePriority = _state.PriorityPlayerId; w.ResumePasses = _state.ConsecutivePasses;
            _state.PendingChoice = window;
            _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
            Emit("reactionWindow", "The Heavens' Blessing: a card drawn from your own Strategy may be paid for.", playerId);
            return;
        }
        w.HeavensOwed = w.HeavensOwed.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private bool Wave835CanPay(int playerId, PendingChoice window)
    {
        var saved = _state.PendingChoice;
        _state.PendingChoice = window;
        try { return AbilityActions(playerId).Any(o => o.Enabled && o.Command?.AbilityId == Wave835Abilities.HeavensPay); }
        finally { _state.PendingChoice = saved; }
    }

    private static string Wave835Prompt(int owed) =>
        $"The Heavens' Blessing: {owed} card{(owed == 1 ? "" : "s")} drawn from your own Strategy {(owed == 1 ? "costs" : "cost")} 2 Gold each. Pay for one now, or decline it.";

    private bool HandleWave835Window(string type, GameCommand command)
    {
        if (type != Wave835Abilities.HeavensWindow) return false;
        var pending = _state.PendingChoice!;
        Require(command.PlayerId == pending.PlayerId && command.Type is "useAbility" or "declineReaction" or "pass" or Wave835Abilities.HeavensDeclineAll, "Pay 2 Gold to draw the card, or decline it.");
        var playerId = pending.PlayerId;
        if (command.Type == "useAbility") UseAbility(command);
        else if (command.Type == Wave835Abilities.HeavensDeclineAll) { Emit("drawDeclined", $"{Player(playerId).Name} declined to pay for the cards drawn from his own Strategy.", playerId); W835.HeavensOwed.Remove(playerId); }
        else { W835.HeavensOwed[playerId] = Math.Max(0, W835.HeavensOwed.GetValueOrDefault(playerId) - 1); Emit("drawDeclined", $"{Player(playerId).Name} declined to pay 2 Gold: the card is not drawn.", playerId); }
        if (_state.PendingChoice is { Type: Wave835Abilities.HeavensWindow } open)
        {
            var owed = W835.HeavensOwed.GetValueOrDefault(playerId);
            // Another card is owed and can still be paid for (a payment is its own: Gold left over from one is not carried to the next): the window stays; otherwise the rest are lost.
            if (owed > 0 && Wave835CanPay(playerId, open))
            {
                _state.PendingChoice = open with { Prompt = Wave835Prompt(owed) };
                _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
            }
            else
            {
                if (owed > 0) Emit("drawLost", $"{Player(playerId).Name} cannot pay 2 Gold again: {owed} card{(owed == 1 ? "" : "s")} drawn from his own Strategy {(owed == 1 ? "is" : "are")} not drawn.", playerId);
                W835.HeavensOwed.Remove(playerId);
                _state.PendingChoice = null; Wave835Resume();
            }
        }
        return true;
    }

    private void Wave835Resume()
    {
        if (W835.ResumePriority is int priority) { _state.PriorityPlayerId = priority; _state.ConsecutivePasses = W835.ResumePasses; }
        W835.ResumePriority = null;
        W835.HeavensOwed = W835.HeavensOwed.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private IEnumerable<ActionOption>? Wave835WindowActions(PendingActionsContext context)
    {
        if (context.Choice.Type != Wave835Abilities.HeavensWindow) return null;
        if (context.PlayerId != context.Choice.PlayerId) return [];
        var options = AbilityActions(context.PlayerId).Where(o => o.Command?.AbilityId == Wave835Abilities.HeavensPay || o.Command is null).ToList();
        options.Add(new ActionOption("declineReaction", "Don't pay: the card is not drawn", true, null, new GameCommand("declineReaction", context.PlayerId, _state.Version)));
        if (W835.HeavensOwed.GetValueOrDefault(context.PlayerId) > 1)
            options.Add(new ActionOption(Wave835Abilities.HeavensDeclineAll, "Don't pay for any of them", true, null, new GameCommand(Wave835Abilities.HeavensDeclineAll, context.PlayerId, _state.Version)));
        return options;
    }

    // ---- Imperial Deployment: the mandatory straightening ----------------------------------------------------------------------------------------------------------------------------

    // Run when an attack is declared (the normal declaration and both additional Attack Phases): "After the next time this turn an attack is declared, straighten his unit." Mandatory, and refused only
    // by what refuses any straightening -- an effect that prevents his next straightening, or says he will not straighten (Straighten's own gate). Spent by the declaration whether it straightens or not.
    private void Wave835AttackDeclared()
    {
        if (_state.Wave835 is not { Deployments.Count: > 0 } w) return;
        foreach (var watch in w.Deployments.Where(d => d.Turn == _state.TurnNumber).ToArray())
        {
            w.Deployments.Remove(watch);
            if (!_state.Cards.TryGetValue(watch.PersonalityId, out var deployed) || !InPlay(deployed)) continue;
            Emit("deploymentStraightens", $"{Definition(deployed).Name}'s unit straightens: an attack was declared after Imperial Deployment.", watch.PlayerId, deployed.Id);
            StraightenBowedUnit(deployed);
        }
        w.Deployments.RemoveAll(d => d.Turn != _state.TurnNumber);
    }

    // ---- Warded Premises: the before-resolution trait -----------------------------------------------------------------------------------------------------------------------------

    // "Negate all Force bonuses from actions to cards at this battlefield." Mendon: only the bonuses actions gave -- a Permanent bonus, a bonus from an attachment and a bonus from a token stay, and so does a
    // bonus from a card or token that an action attached. The bonuses that are Modifiers of an action ability, and not permanent, are removed.
    private void Wave835WardedNegate(CardInstance terrain)
    {
        var battlefield = terrain.ProvinceIndex;
        var removed = 0;
        foreach (var card in _state.Cards.Values.Where(c => c.ProvinceIndex == battlefield && (c.Zone == Zone.Battlefield || c.Zone == Zone.Attached)).ToArray())
            removed += card.Modifiers.RemoveAll(m => m.Stat == "Force" && m.Amount > 0 && !m.Permanent && AbilityCatalog.All.TryGetValue(m.Effect, out var giving) && IsAction(giving));
        Emit("forceBonusesNegated", $"{Definition(terrain).Name}: the Force bonuses from actions to cards at this battlefield are negated ({removed} removed).", terrain.OwnerId, terrain.Id);
    }

    // ---- registration -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave835Hooks()
    {
        EngineHooks.Abilities.Register("Wave835Abilities", 13835, static abilities => abilities.AddWave835());
        EngineHooks.TargetAllows.Register("Wave835TargetAllows", 13835, static (engine, context) => engine.Wave835TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave835Validate", 13835, static (engine, context) => engine.Wave835Validate(context));
        EngineHooks.RunEffects.Register("Wave835RunEffects", 13835, static (engine, context) => engine.RunWave835(context));
        EngineHooks.HonorChange.Register("Wave835Patronage", 13835, static context => context.Engine.Wave835PatronageApply(context));
        EngineHooks.PendingChoice.Register("Wave835Window", 13835, static (engine, type, command) => engine.HandleWave835Window(type, command));
        EngineHooks.PendingActions.Register("Wave835WindowActions", 13835, static (engine, context) => engine.Wave835WindowActions(context));
        EngineHooks.RefillFaceUp.Register("Wave835RefillFaceUp", 13835, static context => context.Engine.Wave835RefillFaceUp(context));
        EngineHooks.ResolveEvent.Register("Wave835ResolveEvent", 13835, static (engine, context) => engine.ResolveWave835Event(context.Player, context.Province, context.Card));
        EngineHooks.ShownChoiceApply.Register("Wave835Shown", 13835, static (engine, choice) => engine.Wave835ShownApply(choice));
        EngineHooks.Looks.Register("Wave835Looks", 13835, static () => [new LookSpec(Wave835Abilities.RevelationsOrder, "Put the rest back in any order", [new LookTarget(SearchDeck.Dynasty)], Wave835Abilities.RevelationsCount - 1, [], LookDisposition.Top, Reorder: true)]);
        ProvinceRevealEvents.Register(Wave835Abilities.RevelationsEffect, static (engine, revealed) => engine.Wave835RevelationsResolve(revealed));
        EngineHooks.PendingChoice.Register("Wave835Foe", 13836, static (engine, type, command) => engine.HandleWave835Foe(type, command));
        EngineHooks.PendingActions.Register("Wave835FoeActions", 13836, static (engine, context) => engine.Wave835FoeActions(context));
        EngineHooks.PendingChoice.Register("Wave835Blood", 13836, static (engine, type, command) => engine.HandleWave835Blood(type, command));
        EngineHooks.PendingActions.Register("Wave835BloodActions", 13836, static (engine, context) => engine.Wave835BloodActions(context));
    }
}
