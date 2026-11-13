namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, PASS GROUP P5 (second file): zones and records. Every card was read against its scan.
//
//   * A ZONE UNDER A CARD (The New Foundries): Zone.Underneath -- a card set face-up under its controller's Stronghold. It is not in play (none of its abilities can be used), public (both players
//     see it: PlayerView.Underneath, client lane) and leaves only as the card that holds it says (discarded: to its owner's Fate discard pile). A Stronghold holds the cards under it for as long as
//     the game lasts.
//   * A CARD'S ABILITIES PERMANENTLY REMOVED (Unsettling Doubts): EmperorP5State.AbilitiesRemoved -- while that card stays in play it has none of its abilities (AbilityIdsOf's hook); the record is
//     dropped the moment it leaves play (swept after every command), so the card is a new object if it ever enters play again. Traits are not abilities (core rules, Reading the Cards).
//   * A UNIT REMOVED FROM THE GAME AND PUT BACK (Step Through the Void): the Shugenja and every card attached to him leave play together, the attachments staying attached (their AttachedTo is
//     kept while all of them are removed from the game); at the top of EndTurn ("before this turn ends", the delay store's point) the unit re-enters play at home, whole, with no cost paid, no
//     entering-play effect or window and no restriction checked.
//   * A UNIT TO THE BOTTOM OF ITS OWNERS' DECKS IN A CHOSEN ORDER (Riddle of Rebirth): each card to its owner's appropriate deck (ToOwnerDeck); a created card is removed from the game instead; the
//     acting player names the order (a DeciderChoice of his, one card at a time).
//   * A ONE-SHOT SHIELD (Blessings of the Shi-Tien Yen-Wang): "Negate the next card effect that bows, destroys, or moves him" -- until the end of the turn (Mendon's durations ruling), spent by the
//     first card effect that would bow, destroy or move him (StalwartNegatesBow, DestructionShielded and EnemyActionMovementAutoNegated's hooks). A cost is not an effect.
//   * A STRAIGHTEN-PHASE-START OPTIONAL TRAIT (Blessings: "After your turn begins: You may choose your dead Personality and put him at the top of your deck"): asked as the Straighten Phase begins,
//     before Bayushi Rentatsu's loss (the active player orders simultaneous start-of-turn traits; this one is his own).
//   * THE OPPONENT'S HAND SHOWN TO THE ACTING PLAYER (The Price of Honor): the target's controller decides (EitherOr); if he shows it, the acting player may discard a card from it (a DeciderChoice of
//     his, whose options name the shown cards -- only his own QueryActions carry them); if not, the Personality is not affected by the rulebook seppuku action until the turn ends.
// ======================================================================================================================================================================================
public static class EmperorP5ZoneAbilities
{
    public const string FoundriesPut = "p5-foundries-put", FoundriesStraighten = "p5-foundries-straighten", FoundriesAttack = "p5-foundries-attack", FoundriesDraw = "p5-foundries-draw";
    public const string DoubtsRemove = "p5-doubts-remove", DoubtsRefill = "p5-doubts-refill";
    public const string BlessingsStrength = "p5-blessings-strength";
    public const string StepThroughVoid = "p5-step-through-void";
    public const string RiddleRebirth = "p5-riddle-of-rebirth";
    public const string TwoFoldVirtue = "p5-two-fold-virtue";
    public const string PriceOfHonor = "p5-price-of-honor";

    public const string OwnSpellInHand = "p5-own-spell-in-hand";
    public const string UnderAirOrWaterSpell = "p5-under-air-or-water-spell";
    public const string ShugenjaAtBattle = "p5-shugenja-at-battle";
    public const string RingNotByOwnText = "p5-ring-not-by-own-text";
    public const string SharesKeywordWithPerformer = "p5-personality-sharing-keyword-with-performer";
    public const string OwnParagon = "p5-own-paragon";

    public const string BlessingsEffect = "p5-blessings-shi-tien";

    public static Dictionary<string, AbilityDefinition> AddEmperorP5Zones(this Dictionary<string, AbilityDefinition> all)
    {
        // The New Foundries (oracle-l5r-9947, Phoenix Stronghold): "Limited: Put a Spell from your hand face-up underneath this card." "Battle: Any number of times per turn, discard a Spell from under
        // this card: Either straighten one to three Shugenja at the current battlefield if the Spell is Air or Water, or Ranged 4 Attack if it is Earth or Fire, or draw a card if it is Void." The three
        // branches are three entries sharing the action's (unlimited) use; the Spell discarded rides on CardIds and is a declared cost.
        all[FoundriesPut] = new("Limited", "Put a Spell from your hand face-up underneath this card", OwnSpellInHand);
        all[FoundriesStraighten] = new("Battle", "Discard an Air or Water Spell from under this card: Straighten one to three Shugenja at the current battlefield", "slots",
            UsageKey: "p5-foundries-discard", UsesPerTurn: AbilityUseLimit.Unlimited, Costs: [new(CostVerbs.Discard, CostSubject.SlotPrefix + "spell")],
            Slots: [new("spell", UnderAirOrWaterSpell, "An Air or Water Spell under this card, to discard"), new("shugenja", ShugenjaAtBattle, "One to three Shugenja at the current battlefield", 1, 3)]);
        all[FoundriesAttack] = new("Battle", "Discard an Earth or Fire Spell from under this card: Ranged 4 Attack", "attack",
            UsageKey: "p5-foundries-discard", UsesPerTurn: AbilityUseLimit.Unlimited, Costs: [new(CostVerbs.Discard, CostSubject.Chosen)]);
        all[FoundriesDraw] = new("Battle", "Discard a Void Spell from under this card: Draw a card", "none",
            UsageKey: "p5-foundries-discard", UsesPerTurn: AbilityUseLimit.Unlimited, Costs: [new(CostVerbs.Discard, CostSubject.Chosen)]);
        // Unsettling Doubts (oracle-l5r-10178): "Open: Target a Ring which did not last enter play by its own text: This is not true enlightenment. Permanently remove its abilities." and "Limited:
        // Target one or more cards in your provinces: Discard them. Refill the provinces face-up from the bottom of your deck."
        all[DoubtsRemove] = new("Open", "Target a Ring which did not last enter play by its own text: Permanently remove its abilities", RingNotByOwnText);
        all[DoubtsRefill] = new("Limited", "Target one or more cards in your provinces: Discard them. Refill the provinces face-up from the bottom of your deck", "provinces");
        // Blessings of the Shi-Tien Yen-Wang (oracle-l5r-1040, a Celestial): its trait is its Effect; "Battle: Choose your performing opposed Personality and discard this card: The Lords of Death grant
        // him strength. Give him +1F for each dead Personality in all players' discard piles. Negate the next card effect that bows, destroys, or moves him."
        all[BlessingsStrength] = new("Battle", "Choose your performing opposed Personality and discard this card: Give him +1F for each dead Personality in all players' discard piles. Negate the next card effect that bows, destroys, or moves him",
            "none", Performer: "own-personality-opposed", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Step Through the Void (oracle-l5r-10132, a Spell): "Battle/Open: Destroy this card unless this Shugenja is Void: Remove this Shugenja from the game; his attachments stay attached. Before this
        // turn ends, put his unit into play, ignoring costs, entering-play effects, and restrictions." Performed by its Shugenja (a Spell's action).
        all[StepThroughVoid] = new("BattleOrOpen", "Destroy this card unless this Shugenja is Void: Remove this Shugenja from the game; his attachments stay attached. Before this turn ends, put his unit into play, ignoring costs, entering-play effects, and restrictions",
            "none", Performer: "own-wielder", Costs: [new(CostVerbs.Sacrifice, UnlessKeyword: "Void")]);
        // Riddle of Rebirth (oracle-l5r-6316): "Limited: Bow your performing Henshin, remove this card from the game, and target a Personality that shares a keyword with your Henshin: Put all cards in the
        // target's unit at the bottom of his owner's appropriate decks in any order."
        all[RiddleRebirth] = new("Limited", "Bow your performing Henshin, remove this card from the game, and target a Personality that shares a keyword with your Henshin: Put all cards in the target's unit at the bottom of his owner's appropriate decks in any order",
            SharesKeywordWithPerformer, Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Henshin"], Costs: [new(CostVerb.Bow, CostSubject.Performer), new(CostVerbs.Remove)]);
        // Two-Fold Virtue (oracle-l5r-9939, Gold Cost 3): "Battle: Target your Paragon: Create a Personality with the same base Chi, Personal Honor and keywords as the target, and whose base Force equals
        // that target's Personal Honor. Remove the Personality from the game after the battle ends."
        all[TwoFoldVirtue] = new("Battle", "Target your Paragon: Create a Personality with the same base Chi, Personal Honor and keywords as the target, and whose base Force equals that target's Personal Honor. Remove the Personality from the game after the battle ends",
            OwnParagon);
        // The Price of Honor (oracle-l5r-8268): "Limited: Choose your performing Courtier and target another player's dishonorable Personality: He is asked to make a sacrifice. The player may show you his
        // hand. If he does, you may discard a card from it. If he does not choose this, his Personality will not be affected by the rulebook seppuku action."
        all[PriceOfHonor] = new("Limited", "Choose your performing Courtier and target another player's dishonorable Personality: The player may show you his hand. If he does, you may discard a card from it. If he does not choose this, his Personality will not be affected by the rulebook seppuku action",
            "enemy-personality", Performer: "own-personality-with-keyword", RequiredKeywords: ["Courtier"], TargetMustBe: TargetStatus.IsDishonorable);
        return all.AddEmperorP5Hidden(); // EmperorP5Hidden.cs
    }
}

public static class EmperorP5Specs
{
    /// <summary>Riddle of Rebirth: the acting player orders the cards of the unit, one at a time (the first chosen goes to the bottom first).</summary>
    public const string RiddleOrder = "p5-riddle-order";
    /// <summary>The Price of Honor: the acting player may discard one card of the hand he was shown.</summary>
    public const string PriceDiscard = "p5-price-discard";
    /// <summary>The Price of Honor: the target's controller shows his hand or refuses.</summary>
    public const string PriceShowHand = "p5-price-show-hand";

    public static bool IsDeciderSpec(string spec) => spec is RiddleOrder or PriceDiscard;
}

/// <summary>A unit Step Through the Void removed from the game, to be put back into play before the turn ends.</summary>
public sealed record SteppedUnit(string HostId, string[] AttachmentIds, int Turn, int PlayerId, string Source);

/// <summary>The Price of Honor: this Personality is not affected by the rulebook seppuku action during this turn.</summary>
public sealed record SeppukuBar(string CardId, int Turn);

public sealed partial class MatchEngine
{
    private const string BlessingsShieldKey = "p5-blessings:shield";

    // ---- the zone under a card ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<CardInstance> CardsUnder(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Underneath && c.HostCardId is null).OrderBy(c => c.Id, StringComparer.Ordinal);

    // The branches whose Spell rides on CardIds (the straightening branch is a multi-target action: its Spell is its first slot).
    private static bool P5CarriesCostCard(string abilityId) => abilityId is EmperorP5ZoneAbilities.FoundriesAttack or EmperorP5ZoneAbilities.FoundriesDraw;

    private static string[] FoundriesElements(string abilityId) => abilityId switch
    {
        EmperorP5ZoneAbilities.FoundriesStraighten => ["Air", "Water"],
        EmperorP5ZoneAbilities.FoundriesAttack => ["Earth", "Fire"],
        EmperorP5ZoneAbilities.FoundriesDraw => ["Void"],
        _ => []
    };

    // The Spells under The New Foundries that pay for this branch, one offered action each (QueryActions); null for any other ability.
    private string?[]? P5CostCardChoices(string abilityId, PlayerState player, CardInstance? source) =>
        !P5CarriesCostCard(abilityId) ? null
        : CardsUnder(player.Id).Where(c => Definition(c).Type == CardType.Spell && FoundriesElements(abilityId).Any(e => Keyword(c, e))).Select(c => (string?)c.Id).DefaultIfEmpty(null).ToArray();

    private static string? P5CostCardVerb(string abilityId) => P5CarriesCostCard(abilityId) ? "discards from under the Stronghold" : null;

    // ---- the Ring whose abilities are removed ---------------------------------------------------------------------------------------------------------------------------------------------------

    // AbilityIdsOf's hook: a card whose abilities were permanently removed has none while it stays in play.
    private bool P5AbilitiesRemoved(CardInstance card) => P5.AbilitiesRemoved.Contains(card.Id) && InPlay(card);

    // After every command: a card that left play is a new object if it ever returns.
    private void SweepP5Records() => P5.AbilitiesRemoved.RemoveAll(id => !_state.Cards.TryGetValue(id, out var card) || !InPlay(card));

    // ---- Blessings of the Shi-Tien Yen-Wang --------------------------------------------------------------------------------------------------------------------------------------------------

    // The shield's hooks (a bow, a destruction, a movement by a card's effect): spent by the first, until the end of the turn.
    private bool BlessingsShieldNegates(CardInstance card, string what)
    {
        if (!card.AbilityUses.TryGetValue(BlessingsShieldKey, out var turn) || turn != _state.TurnNumber || !InPlay(card)) return false;
        card.AbilityUses.Remove(BlessingsShieldKey);
        Emit($"{what}Negated", $"{Definition(card).Name}'s {what} was negated (Blessings of the Shi-Tien Yen-Wang).", card.OwnerId, card.Id);
        return true;
    }

    // Movement: an action's effect (the running command, or the Attack being resolved); the battle's own return home is not a card effect.
    private bool BlessingsShieldNegatesMove(CardInstance card) =>
        (_runningCommand ?? _attackCommandInResolution) is { } mover && !MovementCannotBeNegated(mover) && BlessingsShieldNegates(card, "movement");

    // "After your turn begins: You may choose your dead Personality and put him at the top of your deck." Asked as the Straighten Phase begins; nothing to choose, nothing asked.
    private bool BeginBlessingsChoice(PlayerState player)
    {
        if (!_state.Cards.Values.Any(c => c.OwnerId == player.Id && c.Zone == Zone.Celestial && Definition(c).Effect == EmperorP5ZoneAbilities.BlessingsEffect)) return false;
        if (P5.BlessingsAskedTurn == _state.TurnNumber || !OwnDeadPersonalities(player.Id).Any()) return false;
        P5.BlessingsAskedTurn = _state.TurnNumber;
        var blessings = _state.Cards.Values.First(c => c.OwnerId == player.Id && c.Zone == Zone.Celestial && Definition(c).Effect == EmperorP5ZoneAbilities.BlessingsEffect);
        _state.PendingChoice = new("p5BlessingsTop", player.Id, blessings.Id, $"{Definition(blessings).Name}: you may choose your dead Personality and put him at the top of your deck.");
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        return true;
    }

    private IEnumerable<CardInstance> OwnDeadPersonalities(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId == playerId && c.Zone == Zone.Dead && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    private void ResolveBlessingsTop(GameCommand command)
    {
        var pending = _state.PendingChoice!;
        Require(command.Type == "chooseBlessings" && command.PlayerId == pending.PlayerId, "Choose a dead Personality to put at the top of your deck, or decline.");
        var player = Player(pending.PlayerId);
        if (command.Choice != "decline")
        {
            Require(command.TargetId is not null && OwnDeadPersonalities(player.Id).Any(c => c.Id == command.TargetId), "Choose one of your dead Personalities.");
            var dead = Card(command.TargetId);
            _state.PendingChoice = null;
            ToOwnerDeck(dead, "Top");
            dead.Dishonorable = false;
            Emit("blessingsTop", $"{Definition(Card(pending.CardId)).Name}: {player.Name} put {Definition(dead).Name} at the top of his Dynasty deck.", player.Id, dead.Id);
        }
        else
        {
            _state.PendingChoice = null;
            Emit("blessingsDeclined", $"{player.Name} put no dead Personality at the top of his deck.", player.Id, pending.CardId);
        }
        if (BeginRentatsuLoss(player)) return;
        ContinueStraightenPhase(player);
    }

    private IEnumerable<ActionOption> BlessingsTopActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "p5BlessingsTop" } pending || pending.PlayerId != playerId) yield break;
        foreach (var dead in OwnDeadPersonalities(playerId))
            yield return new("chooseBlessings", $"Put {Definition(dead).Name} at the top of your deck", true, null,
                new GameCommand("chooseBlessings", playerId, _state.Version, CardId: pending.CardId, TargetId: dead.Id), dead.Id);
        yield return new("chooseBlessings", "Put none", true, null, new GameCommand("chooseBlessings", playerId, _state.Version, CardId: pending.CardId, Choice: "decline"), pending.CardId);
    }

    // ---- Step Through the Void ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void StepThroughTheVoid(PlayerState player, CardInstance shugenja, CardInstance source)
    {
        if (!InPlay(shugenja)) return;
        var attachments = Attachments(shugenja.Id).ToArray();
        foreach (var attachment in attachments)
        {
            RemoveAttachmentGrants(shugenja, attachment.Id);
            ResetCardState(attachment);
            attachment.Zone = Zone.RemovedFromGame; attachment.ProvinceIndex = null; // AttachedTo is kept: the attachments stay attached
        }
        var wasDishonorable = shugenja.Dishonorable; // R-082: removed from the game face up, he keeps the status and returns with it
        ResetCardState(shugenja);
        shugenja.Dishonorable = wasDishonorable;
        shugenja.Zone = Zone.RemovedFromGame; shugenja.ProvinceIndex = null;
        P5.SteppedUnits.Add(new(shugenja.Id, attachments.Select(a => a.Id).ToArray(), _state.TurnNumber, player.Id, Definition(source).Name));
        Emit("removedFromGame", $"{Definition(source).Name}: {Definition(shugenja).Name} was removed from the game with his attachments; his unit returns before this turn ends.", shugenja.OwnerId, shugenja.Id);
    }

    // The top of EndTurn ("before this turn ends"): every stepped unit re-enters play at home, whole, unbowed, ignoring costs, entering-play effects and restrictions. Stolen Merchandise's copied
    // Gold-producing trait ends with the turn too.
    private void ReturnSteppedUnits()
    {
        foreach (var stolen in P5.StolenTraits.ToArray())
        {
            P5.StolenTraits.Remove(stolen);
            if (_state.Cards.TryGetValue(stolen.CardId, out var holding)) holding.Printed = null;
        }
        foreach (var stepped in P5.SteppedUnits.ToArray())
        {
            P5.SteppedUnits.Remove(stepped);
            if (!_state.Cards.TryGetValue(stepped.HostId, out var host) || host.Zone != Zone.RemovedFromGame) continue;
            host.Zone = Zone.Home; host.FaceUp = true; host.Bowed = false; host.ProvinceIndex = null;
            foreach (var id in stepped.AttachmentIds)
            {
                if (!_state.Cards.TryGetValue(id, out var attachment) || attachment.Zone != Zone.RemovedFromGame) continue;
                attachment.Zone = Zone.Attached; attachment.AttachedTo = host.Id; attachment.FaceUp = true; attachment.Bowed = false; attachment.ProvinceIndex = null;
                if (Definition(attachment).AttachGrantsKeyword is string granted) GrantKeyword(host, granted, attachment.Id, "Attachment");
            }
            Emit("unitReturned", $"{stepped.Source}: {Definition(host).Name}'s unit was put back into play at home, ignoring costs, entering-play effects and restrictions.", host.OwnerId, host.Id);
        }
    }

    // ---- Riddle of Rebirth ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private List<string> RiddleUnit(CardInstance personality)
    {
        var unit = new List<CardInstance> { personality };
        for (var i = 0; i < unit.Count; i++) unit.AddRange(Attachments(unit[i].Id));
        return unit.Select(c => c.Id).ToList();
    }

    private void PutAtBottomOfOwnersDeck(CardInstance card, string sourceName)
    {
        if (Definition(card).CreatedOnly || card.CreatedCopy) { RemoveFromGame(card); return; } // a created card leaving play is removed from the game
        ToOwnerDeck(card, "Bottom");
    }

    // ---- the decider choices (DeciderChoice.cs hooks) -------------------------------------------------------------------------------------------------------------------------------------------

    private int P5DeciderId(string specId, GameCommand command) => command.PlayerId;

    private (List<string> Candidates, int Min, int Max) P5DeciderOptions(string specId, int deciderId, GameCommand command)
    {
        if (specId == EmperorP5Specs.RiddleOrder)
        {
            var unit = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var target) && InPlay(target) ? RiddleUnit(target) : [];
            return (unit, unit.Count - 1, unit.Count); // the last card needs no choice: "done" with one left puts it last
        }
        var shown = Player(1 - command.PlayerId).Hand.Order(StringComparer.Ordinal).ToList();
        return (shown, 0, Math.Min(1, shown.Count));
    }

    private string P5DeciderPrompt(PendingDeciderChoice p) => p.SpecId == EmperorP5Specs.RiddleOrder
        ? $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose the order in which the cards of the unit go to the bottom of their owners' decks (the first chosen goes first)."
        : $"{Definition(Card(p.SourceCardId)).Name}: {Player(1 - p.DeciderPlayerId).Name} showed you his hand -- you may discard a card from it.";

    private static string P5DeciderVerb(string specId) => specId == EmperorP5Specs.RiddleOrder ? "Next to the bottom" : "Discard";

    private void ApplyP5DeciderChoice(PendingDeciderChoice p)
    {
        var name = Definition(Card(p.SourceCardId)).Name;
        if (p.SpecId == EmperorP5Specs.RiddleOrder)
        {
            foreach (var id in p.Chosen.Concat(p.Candidates.Except(p.Chosen)).ToArray())
            {
                var card = Card(id);
                if (InPlay(card)) PutAtBottomOfOwnersDeck(card, name);
            }
            return;
        }
        foreach (var id in p.Chosen)
        {
            var card = Card(id);
            if (card.Zone == Zone.Hand) DiscardFate(Player(card.OwnerId), card);
        }
    }

    // ---- The Price of Honor ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool SeppukuBarredByPriceOfHonor(CardInstance card) => P5.SeppukuBars.Any(b => b.CardId == card.Id && b.Turn == _state.TurnNumber);

    private string? P5EitherOrPrompt(PendingEitherOr p, string source, string affected) =>
        p.SpecId == EmperorP5Specs.PriceShowHand ? $"{source}: show your hand to {Player(p.ActingPlayerId).Name} (who may discard a card from it), or {affected} will not be affected by the rulebook seppuku action this turn." : P5HiddenEitherOrPrompt(p, source, affected);

    private EitherOrOption[]? P5EitherOrOptions(PendingEitherOr p, CardInstance? affected) =>
        p.SpecId != EmperorP5Specs.PriceShowHand ? P5HiddenEitherOrOptions(p, affected) :
        [
            new("show", "Show your hand", $"{Player(p.ActingPlayerId).Name} sees your hand and may discard a card from it.", EitherOrKind.Avoid, 0, true, null),
            new("refuse", "Do not show it", $"{(affected is null ? "The Personality" : Definition(affected).Name)} will not be affected by the rulebook seppuku action this turn.", EitherOrKind.Accept, 0, true, null)
        ];

    private bool ApplyP5EitherOr(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? affected, string[]? sourceIds)
    {
        if (p.SpecId != EmperorP5Specs.PriceShowHand) return ApplyP5HiddenEitherOr(p, option, decider, affected, sourceIds);
        var source = Card(p.SourceCardId);
        if (option.Key == "show")
        {
            Emit("eitherOrChosen", $"{decider.Name} showed his hand to {Player(p.ActingPlayerId).Name} ({Definition(source).Name}).", decider.Id, source.Id);
            SeeHandCards(p.ActingPlayerId, decider.Id, decider.Hand.ToArray(), Definition(source).Name); // private to the acting player (MatchView.HandSights)
            // The Either/Or prompt is answered: clear it before the discard choice opens, or OpenDeciderChoice shelves it as a suspended Reaction window and gives it back stale when the discard is done (bot soak, issue #642: the match could never continue).
            _state.PendingChoice = null;
            BeginDeciderChoice(EmperorP5Specs.PriceDiscard, p.Command!, p.ConsumedGrant, p.PlayedFromDiscard, p.OpponentHonorBefore, (p.BowedBefore ?? []).ToHashSet(StringComparer.Ordinal), p.PreExistingGrants);
        }
        else
        {
            Emit("eitherOrChosen", $"{decider.Name} did not show his hand ({Definition(source).Name}): {Definition(affected!).Name} will not be affected by the rulebook seppuku action this turn.", decider.Id, affected!.Id);
            P5.SeppukuBars.Add(new(affected!.Id, _state.TurnNumber));
        }
        return true;
    }

    // FinishEitherOr's hook: a shown hand opened the acting player's discard choice, which finishes the action.
    private bool FinishP5EitherOr(PendingEitherOr p) => p.SpecId == EmperorP5Specs.PriceShowHand && _state.PendingDeciderChoice is not null;

    // ---- validation and execution (EmperorP5.cs chains here) ----------------------------------------------------------------------------------------------------------------------------------

    private bool P5ZoneTargetAllows(string target, GameCommand command, PlayerState player, CardInstance? source, CardInstance card)
    {
        var own = card.OwnerId == player.Id; var personality = Definition(card).Type == CardType.Personality;
        return target switch
        {
            EmperorP5ZoneAbilities.ShugenjaAtBattle => personality && Keyword(card, "Shugenja"),
            EmperorP5ZoneAbilities.RingNotByOwnText => Definition(card).Type == CardType.Ring && !CountsForEnlightenment(card),
            EmperorP5ZoneAbilities.SharesKeywordWithPerformer => personality && command.PerformerId is { } henshinId && _state.Cards.TryGetValue(henshinId, out var henshin)
                && CurrentKeywordList(card).Intersect(CurrentKeywordList(henshin), StringComparer.Ordinal).Any(),
            EmperorP5ZoneAbilities.OwnParagon => personality && own && Keyword(card, "Paragon"),
            _ => P5HiddenTargetAllows(target, command, player, source, card) // EmperorP5Hidden.cs
        };
    }

    private static bool IsP5ZoneOutOfPlayTarget(string target) => target is EmperorP5ZoneAbilities.OwnSpellInHand or EmperorP5ZoneAbilities.UnderAirOrWaterSpell || IsP5HiddenOutOfPlayTarget(target);

    private void ValidateP5ZoneOutOfPlayTarget(string target, PlayerState player, CardInstance card)
    {
        if (IsP5HiddenOutOfPlayTarget(target)) ValidateP5HiddenOutOfPlayTarget(player, card);
        else if (target == EmperorP5ZoneAbilities.UnderAirOrWaterSpell)
            Require(card.OwnerId == player.Id && card.Zone == Zone.Underneath && card.HostCardId is null && Definition(card).Type == CardType.Spell && (Keyword(card, "Air") || Keyword(card, "Water")), "Choose an Air or Water Spell under this card.");
        else Require(card.OwnerId == player.Id && card.Zone == Zone.Hand && player.Hand.Contains(card.Id) && Definition(card).Type == CardType.Spell, "Choose a Spell in your hand.");
    }

    private string?[]? P5ZoneCandidates(string target, int playerId) =>
        target == EmperorP5ZoneAbilities.OwnSpellInHand ? Player(playerId).Hand.Where(h => Definition(Card(h)).Type == CardType.Spell).Cast<string?>().ToArray() : P5HiddenCandidates(target, playerId);

    private void ValidateEmperorP5Zones(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        ValidateEmperorP5Hidden(id, ability, command, player, source); // EmperorP5Hidden.cs
        switch (id)
        {
            case EmperorP5ZoneAbilities.FoundriesAttack:
            case EmperorP5ZoneAbilities.FoundriesDraw:
                Require(ChosenCostCard(command) is { } spell && spell.Zone == Zone.Underneath && spell.HostCardId is null && spell.OwnerId == player.Id && Definition(spell).Type == CardType.Spell
                    && FoundriesElements(id).Any(e => Keyword(spell, e)), $"Discard a Spell from under this card that is {string.Join(" or ", FoundriesElements(id))}.");
                break;
            case EmperorP5ZoneAbilities.BlessingsStrength when command.PerformerId is { } blessedId && _state.Cards.TryGetValue(blessedId, out var blessed):
                Require(AtBattle(blessed), "The performing Personality must be at the current battlefield.");
                break;
            case EmperorP5ZoneAbilities.PriceOfHonor:
                break;
        }
    }

    private bool? RunEmperorP5Zones(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorP5ZoneAbilities.FoundriesPut:
                player.Hand.Remove(target!.Id);
                target.Zone = Zone.Underneath; target.FaceUp = true; target.ProvinceIndex = null; target.AttachedTo = null;
                Emit("putUnderneath", $"{player.Name} put {Definition(target).Name} face-up underneath {Definition(source!).Name}.", player.Id, target.Id);
                return false;
            case EmperorP5ZoneAbilities.FoundriesStraighten:
                foreach (var shugenja in SlotCards(command, "shugenja").ToArray()) Straighten(shugenja);
                return false;
            case EmperorP5ZoneAbilities.FoundriesAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, source!.Id, consumedGrant);
            case EmperorP5ZoneAbilities.FoundriesDraw:
                Draw(player);
                return false;
            case EmperorP5ZoneAbilities.DoubtsRemove:
                if (InPlay(target!) && !P5.AbilitiesRemoved.Contains(target!.Id)) P5.AbilitiesRemoved.Add(target.Id);
                Emit("abilitiesRemoved", $"{Definition(source!).Name}: this is not true enlightenment -- {Definition(target!).Name} permanently loses its abilities.", player.Id, target!.Id);
                return false;
            case EmperorP5ZoneAbilities.DoubtsRefill:
                {
                    var emptied = new List<ProvinceState>();
                    foreach (var card in OwnProvinceCardIds(player, command.CardIds ?? []).Select(Card).ToArray())
                    {
                        if (card.Zone != Zone.Province) continue;
                        var province = player.Provinces.Single(p => p.CardId == card.Id);
                        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; card.FaceUp = true; province.CardId = null;
                        Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {player.Name}'s province {province.Index + 1} ({Definition(source!).Name}).", player.Id, card.Id);
                        emptied.Add(province);
                    }
                    foreach (var province in emptied)
                    {
                        if (province.Destroyed || province.CardId is not null || player.DynastyDeck.Count == 0) continue;
                        var refillId = player.DynastyDeck[^1]; player.DynastyDeck.RemoveAt(player.DynastyDeck.Count - 1);
                        var refill = Card(refillId);
                        province.CardId = refillId; refill.Zone = Zone.Province; refill.ProvinceIndex = province.Index; refill.FaceUp = true;
                        Emit("provinceRefilled", $"{Definition(source!).Name}: {player.Name} refilled province {province.Index + 1} face up from the bottom of his deck with {Definition(refill).Name}.", player.Id, refillId);
                    }
                    return false;
                }
            case EmperorP5ZoneAbilities.BlessingsStrength:
                {
                    var blessed = Card(command.PerformerId!);
                    var dead = _state.Cards.Values.Count(c => c.Zone == Zone.Dead && Definition(c).Type == CardType.Personality);
                    if (dead > 0) ApplyModifier(blessed, source!.Id, id, "Force", dead);
                    if (InPlay(blessed))
                    {
                        blessed.AbilityUses[BlessingsShieldKey] = _state.TurnNumber;
                        Emit("shieldRaised", $"{Definition(source!).Name}: the next card effect that bows, destroys or moves {Definition(blessed).Name} this turn is negated.", player.Id, blessed.Id);
                    }
                    return false;
                }
            case EmperorP5ZoneAbilities.StepThroughVoid:
                StepThroughTheVoid(player, Card(command.PerformerId!), source!);
                return false;
            case EmperorP5ZoneAbilities.RiddleRebirth:
                {
                    if (!InPlay(target!)) return false;
                    var unit = RiddleUnit(target!);
                    if (unit.Count == 1) { PutAtBottomOfOwnersDeck(target!, Definition(source!).Name); return false; }
                    return BeginDeciderChoice(EmperorP5Specs.RiddleOrder, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            case EmperorP5ZoneAbilities.TwoFoldVirtue:
                return CreateTwoFoldVirtue(player, source!, target!);
            case EmperorP5ZoneAbilities.PriceOfHonor:
                {
                    SpendHandPlayedCard(player, source);
                    if (!OpenEitherOr(EmperorP5Specs.PriceShowHand, source!, player.Id, target!.OwnerId, target, "action", bowedBeforeAction)) return false;
                    var pending = _state.PendingEitherOr!;
                    pending.Command = command; pending.ConsumedGrant = consumedGrant; pending.PlayedFromDiscard = playedFromDiscard; pending.OpponentHonorBefore = opponentHonorBeforeBattleAction;
                    pending.PreExistingGrants = preExistingGrants.ToList();
                    return true;
                }
            default:
                return RunEmperorP5Hidden(id, ability, command, player, source, target, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants); // EmperorP5Hidden.cs
        }
    }

    // Two-Fold Virtue: the created Personality shows the target's scan as a created copy (its title marked, so a Unique target is not a second copy of his own title); its printed values are the
    // target's base Chi and Personal Honor, his printed keywords, and a base Force equal to his Personal Honor as it stands now; no abilities, no traits, Gold Cost 0, no Honor Requirement.
    // It is created at the Paragon's battlefield (Mendon, issue #1224); a Battle action's target is at the current battlefield, so home is only a fallback.
    private bool CreateTwoFoldVirtue(PlayerState player, CardInstance source, CardInstance target)
    {
        if (!InPlay(target)) return false;
        var copied = Definition(target);
        var honor = CardPersonalHonor(target);
        var printed = new CardDefinition(copied.Id, $"{copied.Name} (Two-Fold Virtue)", CardType.Personality, copied.Clan, 0, 0, honor, copied.Chi, copied.PersonalHonor, null, 0, 0, "none",
            $"Created by Two-Fold Virtue: {honor}F/{copied.Chi}C/{copied.PersonalHonor}PH and the keywords of {copied.Name}.", false, copied.ArtworkKey, (copied.Keywords ?? []).ToArray(), [], 0, copied.Clans);
        var name = Definition(source).Name;
        var battlefield = target.Zone == Zone.Battlefield ? target.ProvinceIndex : null;
        return CreatePersonality(player.Id, CardCatalog.Get(target.DefinitionId), false, battlefield, out _, printed, () =>
        {
            var created = Card($"created-{_state.CreatedCardSequence:D4}");
            _state.CreatedUntilBattleEndsIds.Add(created.Id);
            AddCopyCondition(created, $"Removed from the game after the battle ends ({name}).");
        }, createdCopy: true, original: target);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredP5BlessingsTop(string type, GameCommand command)
    {
        if (type != "p5BlessingsTop") return false;
        ResolveBlessingsTop(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    private bool RegisteredApplyP5DeciderChoice(PendingDeciderChoice p)
    {
        if (!(EmperorP5Specs.IsDeciderSpec(p.SpecId))) return false;
        ApplyP5DeciderChoice(p);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP5ZonesHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredP5BlessingsTop", 1700, static (engine, type, command) => engine.HandleRegisteredP5BlessingsTop(type, command));
        EngineHooks.EitherOrPrompt.Register("P5EitherOrPrompt", 200, static (engine, p, source, affected) => engine.P5EitherOrPrompt(p, source, affected));
        EngineHooks.EitherOrOptions.Register("P5EitherOrOptions", 200, static (engine, p, affected, source, payable) => engine.P5EitherOrOptions(p, affected));
        EngineHooks.ApplyEitherOr.Register("ApplyP5EitherOr", 200, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyP5EitherOr(p, option, decider, affected, sourceIds));
        EngineHooks.FinishEitherOr.Register("FinishP5EitherOr", 500, static (engine, p) => engine.FinishP5EitherOr(p));
        EngineHooks.DeciderPrompt.Register("P5DeciderPrompt", 400, static (engine, p) => EmperorP5Specs.IsDeciderSpec(p.SpecId) ? engine.P5DeciderPrompt(p) : null);
        EngineHooks.DeciderOptions.Register("P5DeciderOptions", 400, static (engine, specId, deciderId, command) => EmperorP5Specs.IsDeciderSpec(specId) ? engine.P5DeciderOptions(specId, deciderId, command) : null);
        EngineHooks.ApplyDecider.Register("ApplyP5DeciderChoice", 400, static (engine, p) => engine.RegisteredApplyP5DeciderChoice(p));
        EngineHooks.DeciderVerb.Register("P5DeciderVerb", 300, static (engine, specId) => EmperorP5Specs.IsDeciderSpec(specId) ? P5DeciderVerb(specId) : null);
        EngineHooks.DeciderIdBefore.Register("P5DeciderId", 100, static (engine, specId, command) => EmperorP5Specs.IsDeciderSpec(specId) ? engine.P5DeciderId(specId, command) : null);
    }
}
