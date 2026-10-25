using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace L5R.Core;

// Primitive P11 (issues #668/#684): a card-restriction store and generalised target bars.
//
//   * A CARD RESTRICTION says what a card (or a province, or a player) may not do or is allowed to do: "may not attack", "will not attach Followers or Items", "may not be assigned to the same
//     battlefield as that Personality", "cards will not enter play from this province", "may assign even if bowed". It is either STORED (a record in MatchState.Restrictions with a duration) or
//     STATIC (registered once for a card definition in CardRestrictionCatalog, or yielded live by an EngineHooks.CardRestrictions callback, and read while the card is in play).
//   * A TARGET-BAR RULE generalises TargetBar (TargetBars.cs): a bar on one card (or on the unit it heads) against the actions of an ACTOR described by an ActorSpec (a card, the unit of a card, its
//     controller, other players, an action keyword), with a duration; a FORCED rule says the matching actors MUST target the card when it is a legal target. Static bars come from
//     CardRestrictionCatalog the same way.
//
// "May not" removes the option (the offered lists and the validators read the same predicate); "will not" lets the action be taken and the effect fails (a challenge that is not issued, a card that is
// not dishonored). Every consult reads ONE predicate in this file, so the offered lists and the validators can never disagree.
//
// Consult points (one line each in the shared files): MatchEngine.Assign and the offered attack/defend units (RestrictionBarsAssignment, RequireRestrictionPairsMayAssign, MayAssignBowed), Dueling.IssueChallenge
// (RestrictionBarsChallenge), AttachmentAllowed/AttachmentLegalTarget (RestrictionRefusesAttachment), Dishonor (RestrictionRefusesDishonor), QuotePurchase/QuoteOverlay (ProvinceEntryLockError, which every
// province entry -- Recruit, Reserve, overlay, ability recruit -- reads), ResolveCelestial (RestrictionDelaysCelestial), StartTurn (RestrictionsTurnBegin), RequireNotBarredFromTargeting
// (RequireRestrictionRulesAllow) and the two performer switches of the ability validator/lister (WithActWhileBowed).

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RestrictionKind
{
    /// <summary>The card may not be assigned to attack.</summary>
    NoAttack,
    /// <summary>The card may not be assigned to defend.</summary>
    NoDefend,
    /// <summary>The card will not challenge: a duel it would issue is not issued.</summary>
    NoChallenge,
    /// <summary>The card (a host) will not attach cards matching Arg: "any", "type:Follower|Item", "keyword:Ninja", "not-keyword:Ninja".</summary>
    NoAttach,
    /// <summary>The card will not become dishonorable.</summary>
    NoDishonor,
    /// <summary>The card and OtherCardId may not be assigned to the same battlefield; with Arg "family", the card may not share a battlefield with another Personality of its family name.</summary>
    NoPair,
    /// <summary>Cards will not enter play from the province PlayerId's ProvinceIndex.</summary>
    NoEntryFromProvince,
    /// <summary>The card may perform actions that require an unbowed performer even while bowed (an action whose COST bows the performer still needs him unbowed).</summary>
    ActWhileBowed,
    /// <summary>The card may be assigned even if bowed.</summary>
    AssignWhileBowed,
    /// <summary>The card may perform at most Max Spell actions in the restriction's period (Count of them so far).</summary>
    SpellLimit,
    /// <summary>(Issue #821, ArmyRestrictions.cs) The card and OtherCardId will not be assigned to attack or defend in the SAME ARMY: the same side of one battlefield. They may stand opposed at it.
    /// A stored row names one direction; the pair is judged both ways, and a card may carry several (Isawa Ryuzo's pairs stack).</summary>
    NoSameArmy,
    /// <summary>(Issue #821, ArmyRestrictions.cs) The card will not be assigned into an army that holds another Personality of its side, and no other Personality of its side will be assigned into its army
    /// (Hida Kaiji - exp). The opposing army is never considered.</summary>
    SoleUnit
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RestrictionUntil
{
    /// <summary>The turn whose number is UntilTurn.</summary>
    Turn,
    /// <summary>The phase UntilPhase of the turn UntilTurn.</summary>
    Phase,
    /// <summary>For the rest of the game.</summary>
    Permanent,
    /// <summary>As long as CardId is in play.</summary>
    WhileInPlay,
    /// <summary>Until the next turn of the player named by PlayerId begins (issue #835, The Egg of P'an Ku: "will not attack or defend until after your next turn begins"): UntilTurn is the number of
    /// turns that player will have taken by then.</summary>
    OwnersNextTurnBegins
}

/// <summary>One stored restriction. CardId names the restricted card (null for a province lock), PlayerId/ProvinceIndex the province for NoEntryFromProvince.</summary>
public sealed record CardRestriction(string Id, RestrictionKind Kind, string? CardId = null, int? ProvinceIndex = null, int? PlayerId = null, string? Arg = null, string? OtherCardId = null,
    RestrictionUntil Until = RestrictionUntil.Turn, int UntilTurn = 0, Phase? UntilPhase = null, int Max = 0, int Count = 0,
    // Issue #835: the player whose action stored it (null: unknown, or a rule's own), so Imperial Deployment can negate the "may not attack/defend" another player's action made.
    int? ByPlayer = null);

/// <summary>Who is acting. (Issue #768: UnitHeadKeywordsAny requires the Personality heading the performing card's unit to have one of the keywords; UnitHeadOpposesCardId that he opposes that card: at the same battlefield, on the other side.) Players: "any", "controller" (the target's controller) or "others" (every other player). ActionKeyword restricts to actions carrying it ("Political"); CardId names the performing
/// card; UnitOfCardId matches the card or any card in the unit it heads (his attachments); UnitCardsOnly requires the performing card to be a Personality or an attachment (a Strategy, Holding or Ring is not "a card in a unit").</summary>
public sealed record ActorSpec(string Players = "any", string? ActionKeyword = null, string? CardId = null, string? UnitOfCardId = null, bool UnitCardsOnly = false,
    string[]? UnitHeadKeywordsAny = null, string? UnitHeadOpposesCardId = null);

/// <summary>A stored target bar (or, with Forced, a forced-target rule). The bar holds on TargetCardId, or on every card in the unit TargetCardId heads when TargetUnit is set.</summary>
public sealed record TargetBarRule(string Id, string TargetCardId, ActorSpec Actor, bool TargetUnit = false, bool Forced = false,
    RestrictionUntil Until = RestrictionUntil.Turn, int UntilTurn = 0, Phase? UntilPhase = null);

/// <summary>A Celestial whose entry from a province was delayed by a province lock (Sudden Blockade): it enters when the next turn begins and the province refills face-down.</summary>
public sealed record DelayedCelestialEntry(string CardId, int PlayerId, int ProvinceIndex);

public sealed class CardRestrictionState
{
    public List<CardRestriction> Items { get; set; } = [];
    public List<TargetBarRule> TargetBars { get; set; } = [];
    public List<DelayedCelestialEntry> DelayedCelestials { get; set; } = [];
    public int NextId { get; set; }
}

public sealed partial class MatchState
{
    // 1800-1899 is reserved for P11.
    [JsonPropertyOrder(1800)]
    public CardRestrictionState Restrictions { get; set; } = new();
}

/// <summary>A restriction a card carries while it is in play: the same kinds as the stored ones, without a duration.</summary>
public sealed record StaticRestriction(RestrictionKind Kind, string? Arg = null, int Max = 0);

/// <summary>A target bar a card carries while it is in play: actors matching Actor may not target the card (or, with TargetUnit, any card in the unit it heads).</summary>
public sealed record StaticTargetBar(ActorSpec Actor, bool TargetUnit = false);

/// <summary>Static restrictions and bars by card definition id. A card's feature file registers its printed ones once, from its module initializer; the engine reads them live while the card is in play.</summary>
public static class CardRestrictionCatalog
{
    private static readonly ConcurrentDictionary<string, StaticRestriction[]> Restrictions = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, StaticTargetBar[]> Bars = new(StringComparer.Ordinal);

    /// <summary>Registers (adding to any already registered) restrictions the card with this definition id has while in play.</summary>
    public static void Register(string definitionId, params StaticRestriction[] restrictions) =>
        Restrictions.AddOrUpdate(definitionId, restrictions, (_, old) => [.. old, .. restrictions]);

    /// <summary>Registers target bars the card with this definition id carries while in play.</summary>
    public static void RegisterTargetBars(string definitionId, params StaticTargetBar[] bars) =>
        Bars.AddOrUpdate(definitionId, bars, (_, old) => [.. old, .. bars]);

    /// <summary>Removes everything registered for a definition id (a test that registered a fixture restriction cleans up with it).</summary>
    public static void Clear(string definitionId) { Restrictions.TryRemove(definitionId, out _); Bars.TryRemove(definitionId, out _); }

    internal static StaticRestriction[] For(string definitionId) => Restrictions.TryGetValue(definitionId, out var found) ? found : [];
    internal static StaticTargetBar[] BarsFor(string definitionId) => Bars.TryGetValue(definitionId, out var found) ? found : [];
}

internal static partial class EngineHooks
{
    /// <summary>Live restrictions on a card that depend on the board (a Follower that stops its wielder attacking, a keyword that grants "act while bowed"): yielded for a card in play.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, CardInstance, IEnumerable<StaticRestriction>>> CardRestrictions = new();
    /// <summary>Live target bars on a card in play.</summary>
    internal static readonly OrderedHooks<Func<MatchEngine, CardInstance, IEnumerable<StaticTargetBar>>> CardTargetBars = new();
}

public sealed partial class MatchEngine
{
    private CardRestrictionState Restrictions => _state.Restrictions;

    /// <summary>A restriction in force on a card (stored or static): what a consult point reads.</summary>
    internal sealed record ActiveRestriction(RestrictionKind Kind, string? Arg, string? OtherCardId, int Max, string? StoredId, int? ByPlayer = null);

    // ---- Registration API (what a card's effect calls) ----------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Stores a restriction on a card. UntilTurn/UntilPhase are stamped from the current turn and phase.</summary>
    internal string AddCardRestriction(RestrictionKind kind, CardInstance card, RestrictionUntil until = RestrictionUntil.Turn, string? arg = null, string? otherCardId = null, int max = 0, string? note = null, int? ownerId = null)
    {
        var id = NextRestrictionId();
        var ownersNextTurn = until == RestrictionUntil.OwnersNextTurnBegins && ownerId is int ownerPlayer;
        Restrictions.Items.Add(new(id, kind, card.Id, null, ownersNextTurn ? ownerId : null, arg, otherCardId, until, ownersNextTurn ? Player(ownerId!.Value).TurnsTaken + 1 : _state.TurnNumber, until == RestrictionUntil.Phase ? _state.Phase : null, max, ByPlayer: (_runningCommand ?? _attackCommandInResolution)?.PlayerId));
        if (note is not null) Emit("restrictionAdded", note, card.OwnerId, card.Id);
        return id;
    }

    /// <summary>Two cards that may not be assigned to the same battlefield (Generational Gap).</summary>
    internal string AddPairRestriction(CardInstance a, CardInstance b, RestrictionUntil until = RestrictionUntil.Turn, string? note = null)
    {
        var id = AddCardRestriction(RestrictionKind.NoPair, a, until, null, b.Id);
        if (note is not null) Emit("restrictionAdded", note, a.OwnerId, a.Id);
        return id;
    }

    /// <summary>"Cards will not enter play from the province" (Sudden Blockade): the province of player ownerId, until the end of this turn (or as given).</summary>
    internal string AddProvinceEntryLock(int ownerId, int provinceIndex, RestrictionUntil until = RestrictionUntil.Turn, string? note = null)
    {
        var id = NextRestrictionId();
        Restrictions.Items.Add(new(id, RestrictionKind.NoEntryFromProvince, null, provinceIndex, ownerId, null, null, until, _state.TurnNumber, until == RestrictionUntil.Phase ? _state.Phase : null));
        if (note is not null) Emit("restrictionAdded", note, ownerId);
        return id;
    }

    /// <summary>A stored bar or forced-target rule.</summary>
    internal string AddTargetBarRule(CardInstance target, ActorSpec actor, bool targetUnit = false, bool forced = false, RestrictionUntil until = RestrictionUntil.Turn, string? note = null)
    {
        var id = NextRestrictionId();
        Restrictions.TargetBars.Add(new(id, target.Id, actor, targetUnit, forced, until, _state.TurnNumber, until == RestrictionUntil.Phase ? _state.Phase : null));
        if (note is not null) Emit(forced ? "targetForced" : "targetBarred", note, target.OwnerId, target.Id);
        return id;
    }

    internal void RemoveRestriction(string id)
    {
        Restrictions.Items.RemoveAll(r => r.Id == id);
        Restrictions.TargetBars.RemoveAll(r => r.Id == id);
    }

    private string NextRestrictionId() => "restriction-" + (++Restrictions.NextId);

    // ---- Reading ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool RestrictionHolds(RestrictionUntil until, string? cardId, int untilTurn, Phase? untilPhase, int? ownerId = null) => until switch
    {
        RestrictionUntil.OwnersNextTurnBegins => ownerId is int owner && Player(owner).TurnsTaken < untilTurn,
        RestrictionUntil.Turn => untilTurn == _state.TurnNumber,
        RestrictionUntil.Phase => untilTurn == _state.TurnNumber && untilPhase == _state.Phase,
        RestrictionUntil.WhileInPlay => cardId is null || _state.Cards.TryGetValue(cardId, out var held) && InPlay(held),
        _ => true
    };

    /// <summary>Every restriction on the card, stored ones that still hold and static ones while it is in play.</summary>
    internal IEnumerable<ActiveRestriction> RestrictionsOn(CardInstance card)
    {
        if (Restrictions.Items.Count > 0)
            foreach (var r in Restrictions.Items)
                if (r.CardId == card.Id && r.Kind != RestrictionKind.NoEntryFromProvince && RestrictionHolds(r.Until, r.CardId, r.UntilTurn, r.UntilPhase, r.PlayerId)) yield return new(r.Kind, r.Arg, r.OtherCardId, r.Max, r.Id, r.ByPlayer);
        if (!InPlay(card)) yield break;
        foreach (var s in CardRestrictionCatalog.For(Definition(card).Id)) yield return new(s.Kind, s.Arg, null, s.Max, null);
        foreach (var hook in EngineHooks.CardRestrictions.Items)
            foreach (var s in hook(this, card)) yield return new(s.Kind, s.Arg, null, s.Max, null);
    }

    internal bool HasRestriction(CardInstance card, RestrictionKind kind) => RestrictionsOn(card).Any(r => r.Kind == kind);

    // ---- Assignment: may not attack / defend, pairs, may assign even if bowed ------------------------------------------------------------------------------------------------------------------------

    /// <summary>"May not attack" / "may not defend": the card may not be assigned on that side. The one predicate Assign and the offered unit list both ask.</summary>
    private bool RestrictionBarsAssignment(CardInstance card, bool isDefender)
    {
        var kind = isDefender ? RestrictionKind.NoDefend : RestrictionKind.NoAttack;
        // Imperial Deployment (issue #835, AssignPrevention) and A Cleansing Breath (Prevention): another player's action's "may not attack/defend" does not bar the assignment; a rule's own restriction (no known source) still does.
        return RestrictionsOn(card).Any(r => r.Kind == kind && !(r.ByPlayer is int by && by != card.OwnerId && (IsImmune(card, ImmunityKind.AssignPrevention, by) || IsImmune(card, ImmunityKind.Prevention, by))));
    }

    private bool RestrictionAllowsAssignBowed(CardInstance card) => HasRestriction(card, RestrictionKind.AssignWhileBowed);

    /// <summary>Is there a pair restriction between these two cards (stored either way round, or two Personalities of one family name under a "family" restriction)?</summary>
    private bool RestrictionPairs(CardInstance a, CardInstance b)
    {
        foreach (var r in RestrictionsOn(a).Where(r => r.Kind == RestrictionKind.NoPair))
            if (r.OtherCardId == b.Id || r.Arg == "family" && PromoFamilyName(a) is { } family && PromoFamilyName(b) == family) return true;
        foreach (var r in RestrictionsOn(b).Where(r => r.Kind == RestrictionKind.NoPair))
            if (r.OtherCardId == a.Id || r.Arg == "family" && PromoFamilyName(b) is { } family && PromoFamilyName(a) == family) return true;
        return false;
    }

    /// <summary>Generational Gap's legality test: two different Personalities with the same family name (the Torn Asunder rule of thumb, PromoFamilyName).</summary>
    internal bool SameFamilyName(CardInstance a, CardInstance b) =>
        a.Id != b.Id && Definition(a).Type == CardType.Personality && Definition(b).Type == CardType.Personality && PromoFamilyName(a) is { } family && PromoFamilyName(b) == family;

    /// <summary>The cards that would stand at the battlefield with this card: those already there and the others this command assigns to it.</summary>
    private void RequireRestrictionPairsMayAssign(CardInstance card, int battlefield, IReadOnlyDictionary<string, int> assignments)
    {
        RequireArmyMayAssign(card, battlefield, assignments); // issue #821 (ArmyRestrictions.cs): same-army pairs and a sole unit
        var companions = _state.Cards.Values.Where(c => c.Id != card.Id && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && c.AttachedTo is null)
            .Concat(assignments.Where(a => a.Value == battlefield && a.Key != card.Id).Select(a => Card(a.Key)));
        foreach (var other in companions)
            Require(!RestrictionPairs(card, other), $"{Definition(card).Name} and {Definition(other).Name} may not be assigned to the same battlefield.");
    }

    // ---- Challenge, attach, dishonor ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool RestrictionBarsChallenge(CardInstance challenger) => HasRestriction(challenger, RestrictionKind.NoChallenge);

    private bool RestrictionRefusesDishonor(CardInstance card) => HasRestriction(card, RestrictionKind.NoDishonor);

    /// <summary>"Will not attach ...": judged on the attachment's printed type and keywords, which is all the offered-target filter (AttachmentLegalTarget) has.</summary>
    private bool RestrictionRefusesAttachment(CardDefinition attachment, CardInstance host)
    {
        foreach (var r in RestrictionsOn(host).Where(r => r.Kind == RestrictionKind.NoAttach))
            if (AttachRestrictionMatches(r.Arg, attachment)) return true;
        return false;
    }

    private static bool AttachRestrictionMatches(string? arg, CardDefinition attachment)
    {
        if (string.IsNullOrEmpty(arg) || arg == "any") return true;
        var colon = arg.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0) return false;
        var (form, list) = (arg[..colon], arg[(colon + 1)..].Split('|', StringSplitOptions.TrimEntries));
        var keywords = attachment.Keywords ?? [];
        return form switch
        {
            "type" => list.Any(t => string.Equals(attachment.Type.ToString(), t, StringComparison.OrdinalIgnoreCase)),
            "keyword" => list.Any(keywords.Contains),
            "not-keyword" => !list.Any(keywords.Contains),
            _ => false
        };
    }

    // ---- Province entry lock ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Is the province's entry locked now? The wave's province-entry effects (a Celestial, a card put into play) read this too.</summary>
    internal bool ProvinceEntryLocked(int ownerId, int provinceIndex) =>
        Restrictions.Items.Any(r => r.Kind == RestrictionKind.NoEntryFromProvince && r.PlayerId == ownerId && r.ProvinceIndex == provinceIndex && RestrictionHolds(r.Until, r.CardId, r.UntilTurn, r.UntilPhase));

    /// <summary>The reason a card in a province may not enter play, or null. Read by QuotePurchase and QuoteOverlay, so Recruit, Reserve, an overlay and an ability recruit all obey it.</summary>
    private string? ProvinceEntryLockError(CardInstance card) =>
        card.Zone == Zone.Province && card.ProvinceIndex is int index && ProvinceEntryLocked(card.OwnerId, index)
            ? $"Cards will not enter play from province {index + 1} this turn." : null;

    /// <summary>A Celestial revealed from a locked province does not enter now: it enters when the next turn begins, and the province refills face-down (revealed in the Events Phase). True when the entry was delayed.</summary>
    private bool RestrictionDelaysCelestial(PlayerState player, ProvinceState province, CardInstance card)
    {
        if (!ProvinceEntryLocked(player.Id, province.Index)) return false;
        Restrictions.DelayedCelestials.Add(new(card.Id, player.Id, province.Index));
        Emit("celestialEntryDelayed", $"{Definition(card).Name} will not enter play from province {province.Index + 1} this turn; it enters when the next turn begins.", player.Id, card.Id);
        return true;
    }

    // ---- Turn boundary ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The next turn begins: delayed Celestials enter (their provinces refill face-down) and restrictions that can no longer hold are dropped.</summary>
    private void RestrictionsTurnBegin()
    {
        foreach (var delayed in Restrictions.DelayedCelestials.ToArray())
        {
            Restrictions.DelayedCelestials.Remove(delayed);
            if (!_state.Cards.TryGetValue(delayed.CardId, out var card) || card.Zone != Zone.Province || card.ProvinceIndex != delayed.ProvinceIndex) continue;
            var owner = Player(delayed.PlayerId); var province = owner.Provinces[delayed.ProvinceIndex];
            if (province.CardId != card.Id) continue;
            province.CardId = null; card.ProvinceIndex = null;
            EnterCelestialPlay(owner, card);
            Refill(owner, province);
        }
        Restrictions.Items.RemoveAll(r => !RestrictionHolds(r.Until, r.CardId, r.UntilTurn, r.UntilPhase, r.PlayerId) && r.Until != RestrictionUntil.WhileInPlay);
        Restrictions.TargetBars.RemoveAll(r => !RestrictionHolds(r.Until, r.TargetCardId, r.UntilTurn, r.UntilPhase) && r.Until != RestrictionUntil.WhileInPlay);
    }

    // ---- Acting while bowed ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // A Performer-subject Bow cost is judged by the Performer category itself ("your performing unbowed X"): the category's unbowed test IS the cost's, so the exemption must not reach such an ability.
    private static bool BowsPerformerAsCost(AbilityDefinition ability) =>
        (ability.Costs ?? []).Any(c => c.Verb == CostVerb.Bow && c.Subject == CostSubject.Performer)
        || ability.CostOptions is { Length: > 0 } options && options.All(o => o.Verb == CostVerb.Bow && o.Subject == CostSubject.Performer);

    /// <summary>"May perform actions requiring an unbowed performer even if bowed": judge the performer category with the performer unbowed. Only the CATEGORY is judged this way; an action whose cost bows the performer
    /// is still paid against the real state, so a bowed performer cannot pay a "bow him" cost (Mendon's ruling for Kojinrue).</summary>
    private T WithActWhileBowed<T>(CardInstance performer, AbilityDefinition ability, Func<T> judge)
    {
        if (!performer.Bowed || BowsPerformerAsCost(ability) || !HasRestriction(performer, RestrictionKind.ActWhileBowed)) return judge();
        performer.Bowed = false;
        try { return judge(); }
        finally { performer.Bowed = true; }
    }

    /// <summary>The same for the offered performer list: every card of the player that may act while bowed counts as unbowed while the candidates are gathered.</summary>
    private T WithActWhileBowedUnbowed<T>(int playerId, AbilityDefinition ability, Func<T> gather)
    {
        if (BowsPerformerAsCost(ability)) return gather();
        List<CardInstance>? bowed = null;
        foreach (var card in _state.Cards.Values)
            if (card.OwnerId == playerId && card.Bowed && InPlay(card) && HasRestriction(card, RestrictionKind.ActWhileBowed)) (bowed ??= []).Add(card);
        if (bowed is null) return gather();
        foreach (var card in bowed) card.Bowed = false;
        try { return gather(); }
        finally { foreach (var card in bowed) card.Bowed = true; }
    }

    // ---- Spell limit ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private CardInstance? SpellPerformer(GameCommand command, AbilityDefinition ability)
    {
        var performer = PerformingCard(command, ability);
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        return source is not null && Definition(source).Type == CardType.Spell ? performer : null;
    }

    /// <summary>The Spell actions a card with a SpellLimit may still perform this period: each stored restriction counts the Spell actions it has performed (NoteRestrictionAction).</summary>
    private void RequireSpellLimitAllows(GameCommand command, AbilityDefinition ability)
    {
        if (SpellPerformer(command, ability) is not { } performer || Restrictions.Items.Count == 0) return;
        foreach (var r in Restrictions.Items.Where(r => r.Kind == RestrictionKind.SpellLimit && r.CardId == performer.Id && RestrictionHolds(r.Until, r.CardId, r.UntilTurn, r.UntilPhase)))
            Require(r.Count < r.Max, $"{Definition(performer).Name} may not use more than {r.Max} Spell action{(r.Max == 1 ? "" : "s")} {(r.Until == RestrictionUntil.Phase ? "this phase" : "this turn")}.");
    }

    /// <summary>Counts a performed Spell action against the performer's spell limits. Called where an action is taken.</summary>
    private void NoteRestrictionAction(AbilityDefinition ability, GameCommand command)
    {
        if (Restrictions.Items.Count == 0 || SpellPerformer(command, ability) is not { } performer) return;
        for (var i = 0; i < Restrictions.Items.Count; i++)
        {
            var r = Restrictions.Items[i];
            if (r.Kind == RestrictionKind.SpellLimit && r.CardId == performer.Id && RestrictionHolds(r.Until, r.CardId, r.UntilTurn, r.UntilPhase)) Restrictions.Items[i] = r with { Count = r.Count + 1 };
        }
    }

    // ---- Target bars and forced targets ---------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Personality that heads the unit a card is in: the card itself, or the Personality an attachment (a Follower, Item or Spell) is attached to.</summary>
    private CardInstance? UnitHeadOf(CardInstance card) =>
        Definition(card).Type == CardType.Personality ? card
        : card.Zone == Zone.Attached && card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && Definition(host).Type == CardType.Personality ? host : null;

    /// <summary>The unit-head half of an ActorSpec (issue #768): the Personality heading the actor's unit has one of the keywords and/or opposes the named card.</summary>
    private bool UnitHeadMatches(ActorSpec spec, CardInstance? actorCard)
    {
        if (actorCard is null || UnitHeadOf(actorCard) is not { } head) return false;
        if (spec.UnitHeadKeywordsAny is { } keywords && !keywords.Any(k => Keyword(head, k))) return false;
        if (spec.UnitHeadOpposesCardId is { } otherId)
            return _state.Cards.TryGetValue(otherId, out var other) && head.Zone == Zone.Battlefield && other.Zone == Zone.Battlefield && head.ProvinceIndex == other.ProvinceIndex && head.OwnerId != other.OwnerId;
        return true;
    }

    private bool InUnitOf(CardInstance card, string headId) => card.Id == headId || card.Zone == Zone.Attached && card.AttachedTo == headId;

    /// <summary>Does this actor match the spec, judged against the card being targeted? The ability and its source card are null when only the performing card is known (a redirect): the keyword test is then skipped.</summary>
    private bool ActorMatches(ActorSpec spec, CardInstance? actorCard, int actingPlayerId, AbilityDefinition? ability, CardInstance? source, CardInstance target)
    {
        if (spec.Players == "controller" && actingPlayerId != target.OwnerId) return false;
        if (spec.Players == "others" && actingPlayerId == target.OwnerId) return false;
        if (spec.ActionKeyword is not null && (ability is null || !ActionCarriesKeyword(ability, source, spec.ActionKeyword))) return false;
        if (spec.CardId is not null && actorCard?.Id != spec.CardId) return false;
        if (spec.UnitOfCardId is not null && (actorCard is null || !InUnitOf(actorCard, spec.UnitOfCardId))) return false;
        if (spec.UnitCardsOnly && (actorCard is null || !(Definition(actorCard).Type == CardType.Personality || actorCard.Zone == Zone.Attached && InPlay(actorCard)))) return false;
        if ((spec.UnitHeadKeywordsAny is not null || spec.UnitHeadOpposesCardId is not null) && !UnitHeadMatches(spec, actorCard)) return false; // issue #768 (Asako Kaitoko - exp)
        return true;
    }

    private IEnumerable<(ActorSpec Actor, bool TargetUnit, bool Forced, string Label)> BarRulesOn(CardInstance target)
    {
        foreach (var r in Restrictions.TargetBars)
            if (RestrictionHolds(r.Until, r.TargetCardId, r.UntilTurn, r.UntilPhase) && (r.TargetCardId == target.Id || r.TargetUnit && InUnitOf(target, r.TargetCardId)))
                yield return (r.Actor, r.TargetUnit, r.Forced, Definition(_state.Cards.TryGetValue(r.TargetCardId, out var carrier) ? carrier : target).Name);
        // Static bars on the card itself, or on a head whose unit the target is in.
        foreach (var carrier in _state.Cards.Values.Where(c => InPlay(c) && InUnitOf(target, c.Id)))
        {
            foreach (var bar in CardRestrictionCatalog.BarsFor(Definition(carrier).Id))
                if (carrier.Id == target.Id || bar.TargetUnit) yield return (bar.Actor, bar.TargetUnit, false, Definition(carrier).Name);
            foreach (var hook in EngineHooks.CardTargetBars.Items)
                foreach (var bar in hook(this, carrier))
                    if (carrier.Id == target.Id || bar.TargetUnit) yield return (bar.Actor, bar.TargetUnit, false, Definition(carrier).Name);
        }
    }

    /// <summary>The announce-time half of the bars: every card the action targets is judged against the generalised bars, then the forced-target rules.</summary>
    private void RequireRestrictionRulesAllow(GameCommand command, AbilityDefinition ability)
    {
        RequireSpellLimitAllows(command, ability);
        var performing = PerformingCard(command, ability);
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        foreach (var targetId in BarredTargetChannelIds(command))
        {
            if (!_state.Cards.TryGetValue(targetId, out var target)) continue;
            foreach (var rule in BarRulesOn(target))
                if (!rule.Forced && ActorMatches(rule.Actor, performing, command.PlayerId, ability, source, target))
                    RequireNotBarred(false, $"Targeting {Definition(target).Name} with this action", rule.Label);
        }
        RequireForcedTarget(command, ability, performing, source);
    }

    /// <summary>"Actions he performs must target Naoto, if legal": a matching actor that names another card is refused when the same action could legally target the forced card.</summary>
    private void RequireForcedTarget(GameCommand command, AbilityDefinition ability, CardInstance? performing, CardInstance? source)
    {
        if (command.TargetId is null || Restrictions.TargetBars.Count == 0) return;
        foreach (var rule in Restrictions.TargetBars.Where(r => r.Forced && RestrictionHolds(r.Until, r.TargetCardId, r.UntilTurn, r.UntilPhase)))
        {
            if (!_state.Cards.TryGetValue(rule.TargetCardId, out var forced) || forced.Id == command.TargetId) continue;
            if (!ActorMatches(rule.Actor, performing, command.PlayerId, ability, source, forced)) continue;
            var alternative = command with { TargetId = forced.Id };
            Require(AbilityRejection(alternative) is not null, $"This action must target {Definition(forced).Name} if it can.");
        }
    }

    /// <summary>The redirect/attack-target half (RequireTargetNotBarred): only the performing card is known, so keyword-scoped bars are not applied here.</summary>
    private void RequireTargetNotBarredByRules(CardInstance actor, CardInstance target)
    {
        foreach (var rule in BarRulesOn(target))
            if (!rule.Forced && rule.Actor.ActionKeyword is null && ActorMatches(rule.Actor, actor, actor.OwnerId, null, null, target))
                RequireNotBarred(false, $"Targeting {Definition(target).Name} with {Definition(actor).Name}'s actions", rule.Label);
    }
}
