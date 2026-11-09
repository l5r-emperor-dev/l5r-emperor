namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, PASS GROUP P5 (third file): hidden information, borrowed cards and copies. Every card was read against its scan.
//
//   * FACE-DOWN PROVINCE CARDS AS TARGETS WITHOUT LEAKING THEM (Petty Squabbles): a face-down province card is named by the id every view already gives it, "hidden-p{owner}-province{n}"
//     (MatchQueries.ViewCard), never by its instance id; one offered action per set of one or two such cards (either player's). Turned face-up, a Celestial enters play as it does whenever it
//     is revealed (core rules, Celestial); any other card stays face-up in its province.
//   * A PRIVATE LOOK AT CARDS IN ANOTHER PLAYER'S HAND (Scouting the Pass; The Price of Honor's shown hand): EmperorP5State.HandSights, read into MatchView.HandSights -- PRIVATE to the
//     looker, for the rest of the turn while the card stays in that hand, named by definition only (never an instance id). "Choose and look at two cards" in a hand the chooser cannot see is
//     a blind choice: the engine takes two at random (the persisted generator), which is what a blind choice is.
//   * "PUT HIM INTO PLAY" FROM A LOOK AT THE TOP OF A DECK (Searching for Answers): a "play" disposition of the look (DeckLook.cs), legal for a Personality of the viewer's own deck that he could
//     bring into play (his Honor Requirement, Unique/Singular, Loyal), entering at the current battlefield ignoring his Gold Cost; his entering-play windows open once the look is arranged; he is
//     discarded (with his unit) after the battle ends.
//   * A HOLDING THAT COPIES ANOTHER'S BASE ABILITY OR ITS GOLD-PRODUCING TRAIT (Stolen Merchandise): the ability through the copy primitive's GrantAbility (until the end of the turn); the
//     trait by giving the Holding, for the turn, the target's printed Gold production and the Effect that carries its Gold condition (CardInstance.Printed, read everywhere Definition is).
//   * PLAYING ANOTHER PLAYER'S STRATEGY (Learning): it is controlled by the player while the additional action that names it stands (EmperorP5State.Borrowed -- exempt from the return of
//     out-of-play cards to their owners), played from the discard pile with the additional action (Daigotsu Bofana's grant), and removed from the game after it resolves (it returns to its
//     owner's removed pile); "its abilities may be used an additional time this turn" is The Fires of War's second-use mark on the card.
// ======================================================================================================================================================================================
public static class EmperorP5HiddenAbilities
{
    public const string PettyReveal = "p5-petty-reveal", PettyReturn = "p5-petty-return";
    public const string SearchingFocus = "p5-searching-focus", SearchingLook = "p5-searching-look";
    public const string ScoutingPass = "p5-scouting-pass";
    public const string StolenAbility = "p5-stolen-ability", StolenGold = "p5-stolen-gold";
    public const string Learning = "p5-learning";

    public const string FaceDownProvinceCards = "p5-face-down-province-cards";
    public const string AttachmentOwnedByOther = "p5-attachment-owned-by-other";
    public const string OtherHoldingCost1To4 = "p5-other-holding-cost-1-to-4";
    public const string BattleStrategyInEnemyDiscard = "p5-battle-strategy-in-enemy-discard";

    public static Dictionary<string, AbilityDefinition> AddEmperorP5Hidden(this Dictionary<string, AbilityDefinition> all)
    {
        // Petty Squabbles (oracle-l5r-5939): "Limited: Target one or two face-down cards in provinces: Turn them face-up." and "Battle/Open: Target an attachment owned by another player: He may pay 4
        // Gold. If he did not, return it to its owner's hand."
        all[PettyReveal] = new("Limited", "Target one or two face-down cards in provinces: Turn them face-up", FaceDownProvinceCards);
        all[PettyReturn] = new("BattleOrOpen", "Target an attachment owned by another player: He may pay 4 Gold. If he did not, return it to its owner's hand", AttachmentOwnedByOther);
        // Searching for Answers (oracle-l5r-6526, the scan): "As a Focus Effect: After this duel ends, if your Personality won it, gain 1 Honor." "Battle: If you are the Defender: Look at the top four
        // cards of your Dynasty deck. If one of them is a Personality, you may put him into play at the current battlefield, ignoring Gold cost. Put the remaining cards back in any order. Discard him
        // after the battle ends." (The catalogue's "Absent Battle ... Recruit him" is a later text.) It brings a Personality into play at the battle: no presence needed (the Exception to Presence).
        all[SearchingFocus] = new("FocusEffect", "As a Focus Effect: After this duel ends, if your Personality won it, gain 1 Honor", "none");
        all[SearchingLook] = new("Battle", "If you are the Defender: Look at the top four cards of your Dynasty deck. If one of them is a Personality, you may put him into play at the current battlefield, ignoring Gold cost. Put the remaining cards back in any order. Discard him after the battle ends",
            "none", Absent: true, Look: EmperorP5Looks.SearchingForAnswers);
        // Scouting the Pass (oracle-l5r-6513, a Recon Strategy): "Reaction: After engaging, if you are the Attacker, choose your performing unbowed Scout at the current battlefield: You have
        // Reconnaissance there. Choose and look at two cards in the Defender's hand. You have the first chance to take a Battle action in this battle, which must be performed by that Scout."
        all[ScoutingPass] = new("Reaction", "After engaging, if you are the Attacker, choose your performing unbowed Scout at the current battlefield: You have Reconnaissance there. Choose and look at two cards in the Defender's hand. You have the first chance to take a Battle action in this battle, which must be performed by that Scout",
            "none", Trigger: "after-engaging", Performer: "own-unbowed-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Scout"]);
        // Stolen Merchandise (oracle-l5r-7494, a Kolat Holding; the scan): "Open: Target another player's Holding with a base Gold Cost from 1 to 4: This card copies one of its base abilities that does
        // not itself copy abilities, or one of its Gold-producing traits." One use, two ways (the ability chosen rides on Choice). The copy lasts until the end of the turn (nothing says permanent).
        all[StolenAbility] = new("Open", "Target another player's Holding with a base Gold Cost from 1 to 4: This card copies one of its base abilities that does not itself copy abilities", OtherHoldingCost1To4,
            UsageKey: "p5-stolen-merchandise", CopiesAbilities: true);
        all[StolenGold] = new("Open", "Target another player's Holding with a base Gold Cost from 1 to 4: This card copies one of its Gold-producing traits", OtherHoldingCost1To4,
            UsageKey: "p5-stolen-merchandise", CopiesAbilities: true);
        // Learning (oracle-l5r-10155): "Battle: Choose your performing Personality and a Battle Strategy in another player's discard pile: Its abilities may be used an additional time this turn. Your
        // Personality copies a keyword on an enemy Personality. You may take an additional action to play the Strategy for one of its actions. After it resolves, remove it from the game." The keyword
        // is the player's choice (Choice); the Strategy is chosen, not targeted.
        all[Learning] = new("Battle", "Choose your performing Personality and a Battle Strategy in another player's discard pile: Its abilities may be used an additional time this turn. Your Personality copies a keyword on an enemy Personality. You may take an additional action to play the Strategy for one of its actions. After it resolves, remove it from the game",
            BattleStrategyInEnemyDiscard, Performer: "own-personality-at-battle");
        return all;
    }
}

public static class EmperorP5Looks
{
    /// <summary>Searching for Answers: the top four of the viewer's Dynasty deck; one Personality may be put into play; the rest back in any order.</summary>
    public static LookSpec SearchingForAnswers { get; } = new("p5-searching-for-answers", "Look at the top four cards of your Dynasty deck", [new("Dynasty")], 4,
        [new(LookDisposition.Play, 0, 1, new("Personality", [CardType.Personality]))], Reorder: true);

    public static IEnumerable<LookSpec> All => [SearchingForAnswers];
}

/// <summary>A card of another player's hand one player looked at (Scouting the Pass, The Price of Honor): private to the viewer for the rest of the turn while the card stays in that hand.</summary>
public sealed record HandSight(int ViewerId, int OwnerId, string CardId, int Turn);
/// <summary>PRIVATE to the looker: a card he saw in another player's hand this turn, by definition only.</summary>
public sealed record HandSightView(int OwnerId, string DefinitionId, CardDefinition Definition);

/// <summary>A Personality put into play by a card that discards him after the battle ends (Searching for Answers).</summary>
public sealed record DiscardAfterBattle(string CardId, int Battle, string Source);

/// <summary>Stolen Merchandise's copied Gold-producing trait: the Holding's printed production for the turn.</summary>
public sealed record StolenTrait(string CardId, int Turn);

public sealed partial class MatchEngine
{
    private const string EitherOrPettyPay = "p5-petty-pay-or-return";

    // ---- Petty Squabbles -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string HiddenProvinceId(int owner, int index) => $"hidden-p{owner}-province{index}";

    private IEnumerable<(PlayerState Owner, ProvinceState Province, CardInstance Card)> FaceDownProvinceCards() =>
        _state.Players.SelectMany(p => p.Provinces.Where(pr => !pr.Destroyed && pr.CardId is not null && !Card(pr.CardId).FaceUp).Select(pr => (p, pr, Card(pr.CardId!))));

    private (PlayerState Owner, ProvinceState Province, CardInstance Card)? FaceDownByHiddenId(string hiddenId) =>
        FaceDownProvinceCards().Select(x => ((PlayerState, ProvinceState, CardInstance)?)x).FirstOrDefault(x => HiddenProvinceId(x!.Value.Item1.Id, x.Value.Item2.Index) == hiddenId);

    private void ValidatePettyTargets(GameCommand command)
    {
        Require(command.TargetId is null, "This action names face-down province cards by their place, not their identity.");
        var chosen = command.CardIds ?? [];
        Require(chosen.Length is 1 or 2 && chosen.Distinct().Count() == chosen.Length, "Choose one or two different face-down province cards.");
        Require(chosen.All(id => FaceDownByHiddenId(id) is not null), "Choose face-down cards in provinces.");
    }

    // One offered action per set of one or two face-down province cards (a province set is not a card, Hidden Falls Dojo's shape).
    private IEnumerable<ActionOption> P5CustomOptions(string id, AbilityDefinition ability, CardInstance? source, int playerId)
    {
        if (id == EmperorP5HiddenAbilities.PettyReveal)
        {
            var hidden = FaceDownProvinceCards().Select(x => HiddenProvinceId(x.Owner.Id, x.Province.Index)).ToArray();
            var sets = hidden.Select(h => new[] { h }).Concat(hidden.SelectMany((a, i) => hidden.Skip(i + 1).Select(b => new[] { a, b }))).ToArray();
            if (sets.Length == 0) sets = [[]];
            foreach (var set in sets)
            {
                var command = new GameCommand("useAbility", playerId, _state.Version, source?.Id, AbilityId: id, CardIds: set.Length == 0 ? null : set);
                string? error = null;
                error = TryValidateAbility(command);
                yield return new("useAbility", ability.Label + (set.Length == 0 ? "" : " · " + string.Join(" and ", set.Select(DescribeHiddenProvince))), error is null, error, error is null ? command : null, source?.Id, set);
            }
            yield break;
        }
        // Stolen Merchandise: one offered action per target Holding and per thing it may copy from it.
        foreach (var holding in _state.Cards.Values.Where(c => InPlay(c) && c.OwnerId != playerId && Definition(c).Type == CardType.Holding).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var choices = id == EmperorP5HiddenAbilities.StolenAbility ? StolenAbilityChoices(holding).ToArray() : HasGoldProducingTrait(Definition(holding)) ? ["gold"] : [];
            foreach (var choice in choices)
            {
                var command = new GameCommand("useAbility", playerId, _state.Version, source?.Id, TargetId: holding.Id, AbilityId: id, Choice: choice);
                string? error = null;
                error = TryValidateAbility(command);
                var what = choice == "gold" ? "its Gold-producing trait" : AbilityCatalog.All[choice].Label;
                yield return new("useAbility", $"{ability.Label} · {Definition(holding).Name}: {what}", error is null, error, error is null ? command : null, source?.Id, [holding.Id], ChoiceLabel: what);
            }
        }
    }

    private static bool HasP5CustomOptions(string id) => id is EmperorP5HiddenAbilities.PettyReveal or EmperorP5HiddenAbilities.StolenAbility or EmperorP5HiddenAbilities.StolenGold;

    private string DescribeHiddenProvince(string hiddenId) =>
        FaceDownByHiddenId(hiddenId) is { } x ? $"{(x.Owner.Id == _state.PriorityPlayerId ? "your" : $"{x.Owner.Name}'s")} province {x.Province.Index + 1}" : hiddenId;

    private void RevealForPettySquabbles(GameCommand command, CardInstance source)
    {
        foreach (var hiddenId in command.CardIds ?? [])
        {
            if (FaceDownByHiddenId(hiddenId) is not { } x) continue;
            var (owner, province, card) = x;
            card.FaceUp = true;
            Emit("provinceRevealed", $"{Definition(source).Name}: {Definition(card).Name} in {owner.Name}'s province {province.Index + 1} is turned face-up.", command.PlayerId, card.Id);
            if (Definition(card).Type == CardType.Celestial) ResolveCelestial(owner, province, card); // a revealed Celestial enters play (core rules)
        }
    }

    private string? P5HiddenEitherOrPrompt(PendingEitherOr p, string source, string affected) =>
        p.SpecId == EitherOrPettyPay ? $"{source}: pay 4 Gold, or {affected} returns to its owner's hand." : null;

    private EitherOrOption[]? P5HiddenEitherOrOptions(PendingEitherOr p, CardInstance? affected) =>
        p.SpecId != EitherOrPettyPay ? null :
        [
            new("pay", "Pay 4 Gold", "Nothing further happens.", EitherOrKind.Avoid, 4, PaymentOptions(p.DeciderPlayerId, 4).Length > 0, "You cannot produce 4 Gold."),
            new("refuse", "Do not pay", $"{(affected is null ? "The attachment" : Definition(affected).Name)} returns to its owner's hand.", EitherOrKind.Accept, 0, true, null)
        ];

    private bool ApplyP5HiddenEitherOr(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? affected, string[]? sourceIds)
    {
        if (p.SpecId != EitherOrPettyPay) return false;
        var source = Card(p.SourceCardId);
        if (option.Key == "pay")
        {
            Pay(decider.Id, source, option.GoldCost, sourceIds);
            Emit("eitherOrChosen", $"{decider.Name} paid 4 Gold ({Definition(source).Name}).", decider.Id, source.Id);
        }
        else
        {
            Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}): {Definition(affected!).Name} returns to its owner's hand.", decider.Id, affected!.Id);
            if (affected!.Zone == Zone.Attached) ReturnToHand(affected);
        }
        return true;
    }

    // ---- private looks at a hand ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void SeeHandCards(int viewerId, int ownerId, IEnumerable<string> cardIds, string sourceName)
    {
        P5.HandSights.RemoveAll(s => s.Turn != _state.TurnNumber);
        var ids = cardIds.ToArray();
        foreach (var id in ids) P5.HandSights.Add(new(viewerId, ownerId, id, _state.TurnNumber));
        Emit("handCardsLooked", $"{sourceName}: {Player(viewerId).Name} looks at {ids.Length} card{(ids.Length == 1 ? "" : "s")} in {Player(ownerId).Name}'s hand.", viewerId);
    }

    private HandSightView[]? BuildHandSights(int viewerId)
    {
        var sights = P5.HandSights.Where(s => s.ViewerId == viewerId && s.Turn == _state.TurnNumber && _state.Cards.TryGetValue(s.CardId, out var c) && c.Zone == Zone.Hand && Player(s.OwnerId).Hand.Contains(s.CardId)).ToArray();
        return sights.Length == 0 ? null : sights.Select(s => new HandSightView(s.OwnerId, Card(s.CardId).DefinitionId, Copy(Definition(Card(s.CardId))))).ToArray();
    }

    // ---- Searching for Answers -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The look's "play" disposition (DeckLook.cs hooks): a Personality of the viewer's own deck that he could bring into play (Honor Requirement, Unique/Singular, Loyal; the Gold is ignored).
    private (bool Legal, string? Reason) P5PlayDispositionLegal(PendingLook look, CardInstance card)
    {
        if (look.DeckOwnerId != look.PlayerId) return (false, "Only a card from your own deck may be put into play");
        if (_state.CurrentBattleProvince is null) return (false, "There is no current battlefield");
        var quote = QuotePurchase(look.PlayerId, card.Id, fromDynastyDeck: true);
        return quote.Error is null ? (true, null) : (false, quote.Error);
    }

    private void PutIntoPlayFromLook(PendingLook look, CardInstance card, CardInstance source)
    {
        var viewer = Player(look.PlayerId);
        ResetCardState(card);
        EnterPlayFromRecruit(viewer, card, _state.CurrentBattleProvince, Definition(source).Name);
        P5.DiscardAfterBattle.Add(new(card.Id, _state.BattleNumber, Definition(source).Name));
        P5.LookEntryId = card.Id;
        Emit("discardScheduled", $"{Definition(source).Name}: {Definition(card).Name} will be discarded after the battle ends.", viewer.Id, card.Id);
    }

    // FinishLook's hook: the Personality the look put into play gets his entering-play windows now; true when one opened (it closes to the other player, as after any action).
    private bool OpenP5LookEntryWindows(PendingLook look)
    {
        if (P5.LookEntryId is not { } entered) return false;
        P5.LookEntryId = null;
        if (!_state.Cards.TryGetValue(entered, out var card) || !InPlay(card)) return false;
        _state.PendingLook = null;
        return OpenRecruitEntryWindows(Player(look.PlayerId), card, false, null);
    }

    // The battle has ended (with its created cards): whoever Searching for Answers put into play for it is discarded, his unit with him.
    private void DiscardP5AfterBattle()
    {
        foreach (var due in P5.DiscardAfterBattle.Where(d => d.Battle == _state.BattleNumber).ToArray())
        {
            P5.DiscardAfterBattle.Remove(due);
            if (_state.Cards.TryGetValue(due.CardId, out var card) && InPlay(card) && Definition(card).Type == CardType.Personality) DiscardPersonalityFromPlay(card, due.Source);
        }
        P5.DiscardAfterBattle.RemoveAll(d => d.Battle < _state.BattleNumber);
    }

    // The Focus Effect (ApplyDuelFocusEffectCore's hook).
    private void ApplyP5FocusEffect(string abilityId, int ownerId, int? winnerPlayerId)
    {
        if (abilityId == EmperorP5HiddenAbilities.SearchingFocus && ownerId == winnerPlayerId) GainHonor(ownerId, 1, "Searching for Answers");
    }

    // The card whose "first action must be performed by" limit stands (the Naval Invasion shape, which Scouting the Pass shares).
    private string FirstPerformerSource() => P5.ScoutingBattle == _state.BattleNumber ? "Scouting the Pass" : "Naval Invasion";

    // ---- Stolen Merchandise ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private IEnumerable<string> StolenAbilityChoices(CardInstance holding) =>
        (Definition(holding).Abilities ?? []).Where(a => AbilityCatalog.All.TryGetValue(a, out var ability) && CopyableAbility(ability));

    private static bool IsGoldEffect(string effect) => ClanGoldEffects.IsClanGold(effect) || ConditionalProductionCatalog.For(effect) is not null;

    private static bool HasGoldProducingTrait(CardDefinition holding) => holding.GoldProduction > 0 || IsGoldEffect(holding.Effect);

    private void StealGoldTrait(CardInstance stolen, CardInstance holding, string sourceName)
    {
        var from = Definition(holding);
        var own = CardCatalog.Get(stolen.DefinitionId);
        stolen.Printed = own with { GoldProduction = from.GoldProduction, Effect = IsGoldEffect(from.Effect) ? from.Effect : own.Effect };
        P5.StolenTraits.RemoveAll(t => t.CardId == stolen.Id);
        P5.StolenTraits.Add(new(stolen.Id, _state.TurnNumber));
        Emit("goldTraitCopied", $"{sourceName} copies {from.Name}'s Gold-producing trait (it produces {from.GoldProduction} Gold) until the end of the turn.", stolen.OwnerId, stolen.Id);
    }

    // ---- Learning -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private string[] EnemyPersonalityKeywords(int playerId) =>
        _state.Cards.Values.Where(c => c.OwnerId != playerId && InPlay(c) && Definition(c).Type == CardType.Personality).SelectMany(CurrentKeywordList).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    // The keyword chosen rides on the command (one offered action per keyword on an enemy Personality); null when there is none to copy.
    private (string Key, string Label)[]? P5ChoiceOptions(string abilityId, int playerId) =>
        abilityId != EmperorP5HiddenAbilities.Learning ? null
        : EnemyPersonalityKeywords(playerId) is { Length: > 0 } keywords ? keywords.Select(k => (k, $"copies {k}")).ToArray() : null;

    private static bool P5AllowsChoice(string abilityId) => abilityId is EmperorP5HiddenAbilities.Learning or EmperorP5HiddenAbilities.StolenAbility or EmperorP5HiddenAbilities.StolenGold;

    // ReturnCardsOutOfPlayToTheirOwners' exemption: another player's Strategy Learning borrowed stays under the borrower's control while the additional action naming it stands.
    private bool P5Borrowed(CardInstance card) =>
        P5.Borrowed.Contains(card.Id) && card.Zone == Zone.FateDiscard && _state.AdditionalActions.Any(g => g.FromDiscard && g.PlayerId == card.OwnerId && g.ScopeCardIds?.Contains(card.Id) == true);

    // After every command: a borrowed Strategy whose additional action is gone (declined, forfeited, spent) goes back to its owner.
    private void SweepP5Borrowed()
    {
        foreach (var id in P5.Borrowed.ToArray())
        {
            if (_state.Cards.TryGetValue(id, out var card) && P5Borrowed(card)) continue;
            P5.Borrowed.Remove(id);
            if (_state.Cards.TryGetValue(id, out var returned) && !InPlay(returned) && returned.Zone != Zone.Hand) RestoreOwner(returned);
        }
    }

    // ---- validation and execution (EmperorP5.cs chains here) -----------------------------------------------------------------------------------------------------------------------------------

    private bool P5HiddenTargetAllows(string target, GameCommand command, PlayerState player, CardInstance? source, CardInstance card)
    {
        var own = card.OwnerId == player.Id;
        return target switch
        {
            EmperorP5HiddenAbilities.AttachmentOwnedByOther => card.Zone == Zone.Attached && Definition(card).Type is CardType.Follower or CardType.Item or CardType.Spell && (card.OriginalOwnerId ?? card.OwnerId) != player.Id,
            EmperorP5HiddenAbilities.OtherHoldingCost1To4 => !own && Definition(card).Type == CardType.Holding && CardCatalog.Get(card.DefinitionId).GoldCost is >= 1 and <= 4,
            _ => false
        };
    }

    private static bool IsP5HiddenOutOfPlayTarget(string target) => target == EmperorP5HiddenAbilities.BattleStrategyInEnemyDiscard;

    private void ValidateP5HiddenOutOfPlayTarget(PlayerState player, CardInstance card) =>
        Require(card.OwnerId != player.Id && card.Zone == Zone.FateDiscard && Definition(card).Type == CardType.Strategy
            && (Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var ability) && ability.Timing is "Battle" or "BattleOrOpen"), "Choose a Battle Strategy in another player's discard pile.");

    private string?[]? P5HiddenCandidates(string target, int playerId) =>
        target != EmperorP5HiddenAbilities.BattleStrategyInEnemyDiscard ? null
        : _state.Cards.Values.Where(c => c.OwnerId != playerId && c.Zone == Zone.FateDiscard && Definition(c).Type == CardType.Strategy).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray();

    private void ValidateEmperorP5Hidden(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case EmperorP5HiddenAbilities.SearchingLook:
                break;
            case EmperorP5HiddenAbilities.ScoutingPass:
                break;
            case EmperorP5HiddenAbilities.StolenAbility:
                Require(command.TargetId is { } abilityHolding && _state.Cards.ContainsKey(abilityHolding) && command.Choice is { } copied && StolenAbilityChoices(Card(abilityHolding)).Contains(copied),
                    "Choose one of the Holding's base abilities that does not itself copy abilities.");
                break;
            case EmperorP5HiddenAbilities.StolenGold:
                Require(command.Choice is null or "gold", "Choose its Gold-producing trait.");
                Require(command.TargetId is { } goldHolding && _state.Cards.ContainsKey(goldHolding) && HasGoldProducingTrait(Definition(Card(goldHolding))), "That Holding has no Gold-producing trait.");
                break;
            case EmperorP5HiddenAbilities.Learning:
                Require(command.Choice is null ? EnemyPersonalityKeywords(player.Id).Length == 0 : EnemyPersonalityKeywords(player.Id).Contains(command.Choice, StringComparer.Ordinal),
                    "Choose a keyword on an enemy Personality to copy.");
                break;
        }
    }

    private bool? RunEmperorP5Hidden(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorP5HiddenAbilities.PettyReveal:
                RevealForPettySquabbles(command, source!);
                return false;
            case EmperorP5HiddenAbilities.PettyReturn:
                SpendHandPlayedCard(player, source);
                return OpenEitherOr(EitherOrPettyPay, source!, player.Id, target!.OriginalOwnerId ?? target.OwnerId, target, "action", bowedBeforeAction);
            case EmperorP5HiddenAbilities.SearchingLook:
                SpendHandPlayedCard(player, source);
                BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case EmperorP5HiddenAbilities.ScoutingPass:
                {
                    GrantReconnaissanceAtCurrentBattlefield(player.Id);
                    var defender = Player(1 - player.Id);
                    var hand = defender.Hand.ToList();
                    var random = new StableRandom(_state.RngState);
                    random.Shuffle(hand);
                    _state.RngState = random.State;
                    SeeHandCards(player.Id, defender.Id, hand.Take(2), Definition(source!).Name);
                    ClaimFirstOpportunity(player.Id, "Engage", source);
                    if (_state.PendingEngage is { } engage) { engage.NavalPerformerId = command.PerformerId; P5.ScoutingBattle = _state.BattleNumber; } // "which must be performed by that Scout": the first-action limit (CloseEngageWindow)
                    return false;
                }
            case EmperorP5HiddenAbilities.StolenAbility:
                if (!InPlay(target!)) return false;
                GrantAbility(source!, command.Choice!, id, GrantDuration.Turn, target!.DefinitionId, Definition(target).Name);
                Emit("abilityCopied", $"{Definition(source!).Name} copies an ability from {Definition(target!).Name}: {AbilityCatalog.All[command.Choice!].Label}.", player.Id, source!.Id);
                return false;
            case EmperorP5HiddenAbilities.StolenGold:
                if (InPlay(target!)) StealGoldTrait(source!, target!, Definition(source!).Name);
                return false;
            case EmperorP5HiddenAbilities.Learning:
                {
                    var strategy = target!;
                    if (strategy.Zone != Zone.FateDiscard) return false;
                    strategy.AbilityUses[SecondUseKey] = _state.TurnNumber; // "Its abilities may be used an additional time this turn"
                    var performer = Card(command.PerformerId!);
                    if (command.Choice is { } keyword && InPlay(performer) && !Keyword(performer, keyword))
                    {
                        performer.GrantedKeywords.Add(new(keyword, id, GrantDuration.Turn));
                        Emit("keywordCopied", $"{Definition(performer).Name} copies the keyword {keyword} from an enemy Personality.", player.Id, performer.Id);
                    }
                    // The additional action to play it: the player controls the Strategy while that action stands.
                    strategy.OriginalOwnerId ??= strategy.OwnerId; strategy.OwnerId = player.Id;
                    if (!P5.Borrowed.Contains(strategy.Id)) P5.Borrowed.Add(strategy.Id);
                    GrantAdditionalAction(player.Id, [strategy.Id], fromDiscard: true);
                    Emit("strategyBorrowed", $"{Definition(source!).Name}: {player.Name} may take an additional action to play {Definition(strategy).Name} from {Player(strategy.OriginalOwnerId!.Value).Name}'s discard pile; it is removed from the game after it resolves.", player.Id, strategy.Id);
                    return false;
                }
            default:
                return null;
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP5HiddenHooks()
    {
        EngineHooks.Looks.Register("EmperorP5Looks", 400, static () => EmperorP5Looks.All);
    }
}
