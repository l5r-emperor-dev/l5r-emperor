namespace L5R.Core;

// ======================================================================================================================================================================================
// PROMOTIONAL-EMPEROR PASS (issue #653), bringing cards into play from a pile: Resurrection, Renewal, Govern the Land, Re-fortification, Peace, and Imperial Charter's created Holdings.
//
//   * RESURRECTION / RENEWAL: Tokiko's shape (a dead or discarded Personality brought into play for his whole recruit price, at home), with "Discard a card" as a declared cost on a hand slot.
//     Renewal's "it is not the active player's first or second turn" is judged against the player whose turn it is (his TurnsTaken counts his turns), whoever plays the Open action; its
//     "discarded Personality" is any Personality in his discard pile, a dead one included (Mendon, issue #420: a dead Personality is a discarded one too).
//   * GOVERN THE LAND / RE-FORTIFICATION: a Holding in the player's own discard pile is brought into play bowed (a Holding enters play bowed; the printed "(bowed)" is a reminder) for its whole
//     recruit price (QuotePurchase, which now prices a Holding from a discard pile as it prices a Personality), through the entering-play window every recruit opens. Govern's second action is
//     Foothills Keep's attach from a discard pile; Re-fortification's Battle half is Absent ("even if you control no units at the current battlefield") and bows the Castle as its cost.
//   * PEACE: a Dynasty deck search for a non-Unique Holding recruited bowed (Versatile Farm's search recruit, which enters its Holding unbowed; SearchEntersBowed says which).
//   * IMPERIAL CHARTER: "Starting with you, each player may create an unbowed Holding with the trait 'Bow this card: Produce 2 Gold.'" -- two optional decisions (the acting player's, then the
//     other player's) through the decider choice (DeciderChoice.cs hooks), each creating the published Charter Holding (oracle-l5r-10806) under his own control.
// ======================================================================================================================================================================================

public static partial class PromoEmperorFateAbilities
{
    public const string ResurrectionRecruit = "pe-resurrection-recruit", RenewalRecruit = "pe-renewal-recruit", GovernHolding = "pe-govern-the-land-holding", GovernAttach = "pe-govern-the-land-attach",
        FortHolding = "pe-re-fortification-holding", FortMove = "pe-re-fortification-move", CharterCreate = "pe-imperial-charter-create", PeaceAbility = "pe-peace-search";

    /// <summary>The Charter Holding Imperial Charter and Treasure Hunter create: "Bow this card: Produce 2 Gold." (published created-only card, holding-wave-4).</summary>
    public const string CharterScan = "oracle-l5r-10806";

    public static Dictionary<string, AbilityDefinition> AddPromoEmperorFateRecruits(this Dictionary<string, AbilityDefinition> all)
    {
        // Resurrection (oracle-l5r-6281): "Open: Discard a card and choose your performing dead Personality: Bring him into play, paying all costs."
        all[ResurrectionRecruit] = new("Open", "Discard a card and choose your performing dead Personality: Bring him into play, paying all costs", "slots",
            Slots: [new("target", "own-dead-personality", "Your dead Personality"), new("discard", "hand", "A card in your hand to discard")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))], Cost: GoldCostRule.TargetRecruit());
        // Renewal (oracle-l5r-6261): "Open: If it is not the active player's first or second turn, discard a card and choose your performing discarded Personality: Bring him into play, paying all costs."
        all[RenewalRecruit] = new("Open", "If it is not the active player's first or second turn, discard a card and choose your performing discarded Personality: Bring him into play, paying all costs", "slots",
            Slots: [new("target", "own-discarded-personality", "Your discarded Personality"), new("discard", "hand", "A card in your hand to discard")],
            Costs: [new(CostVerbs.Discard, CostSubject.Slot("discard"))], Cost: GoldCostRule.TargetRecruit());
        // Govern the Land (oracle-l5r-2877): "Open: Target a Holding in your discard pile: Bring it into play (bowed), paying all costs." and "Battle/Open: Choose your performing Personality and target a Follower
        // or Item in your discard pile: Attach it to him, paying all costs."
        all[GovernHolding] = new("Open", "Target a Holding in your discard pile: Bring it into play (bowed), paying all costs", "own-card-in-discard", Cost: GoldCostRule.TargetRecruit());
        all[GovernAttach] = new("BattleOrOpen", "Choose your performing Personality and target a Follower or Item in your discard pile: Attach it to him, paying all costs", "attachment-in-discard",
            Performer: "own-personality-at-battle", Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Target, AttachPile.Own, AttachWielder.Performer, [CardType.Follower, CardType.Item]));
        // Re-fortification (oracle-l5r-6192): "Limited: Target a Castle Holding in your discard pile: Put it into play, paying all costs." and "Battle: Even if you control no units at the current battlefield, bow
        // your target Castle and target an enemy Personality: Move him home."
        all[FortHolding] = new("Limited", "Target a Castle Holding in your discard pile: Put it into play, paying all costs", "own-card-in-discard", Cost: GoldCostRule.TargetRecruit());
        all[FortMove] = new("Battle", "Even if you control no units at the current battlefield, bow your target Castle and target an enemy Personality: Move him home", "enemy-personality", Absent: true,
            Performer: "own-castle-or-siege", Costs: ActionCost.BowPerformer, Move: new(MoveSpec.Target, MoveSpec.Home));
        // Peace (oracle-l5r-5895): "Limited: Search your Dynasty deck for a non-Unique Holding. Bring it into play (bowed), paying all costs."
        all[PeaceAbility] = new("Limited", "Search your Dynasty deck for a non-Unique Holding. Bring it into play (bowed), paying all costs", "none", Search: MatchEngine.PeaceSearch);
        // Imperial Charter (oracle-l5r-9990): "Limited: If it is not your first turn: Starting with you, each player may create an unbowed Holding with the trait 'Bow this card: Produce 2 Gold.'"
        all[CharterCreate] = new("Limited", "If it is not your first turn: Starting with you, each player may create an unbowed Holding with the trait \"Bow this card: Produce 2 Gold.\"", "none");
        return all;
    }

    public const string CharterFirst = "pe-charter-first", CharterSecond = "pe-charter-second";
    public static bool IsCharterSpec(string spec) => spec is CharterFirst or CharterSecond;
}

public sealed partial class MatchEngine
{
    // ---- validation --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void ValidatePromoRecruit(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case PromoEmperorFateAbilities.ResurrectionRecruit or PromoEmperorFateAbilities.RenewalRecruit when ability.Slots is not null:
                {
                    Require(SlotCards(command, "discard").All(card => card.Id != command.CardId), "A Strategy being played cannot be discarded to itself.");
                    if (id == PromoEmperorFateAbilities.RenewalRecruit)
                        Require(Player(_state.ActivePlayerId).TurnsTaken > 2, "It is the active player's first or second turn.");
                    if (SlotCards(command, "target").FirstOrDefault() is { } recruit)
                    {
                        var quote = QuotePurchase(player.Id, recruit.Id, command.ClanDiscount, command.ClanAllegiance, fromDeadPile: true);
                        Require(quote.Error is null, quote.Error ?? "");
                    }
                    break;
                }
            case PromoEmperorFateAbilities.GovernHolding or PromoEmperorFateAbilities.FortHolding when command.TargetId is { } holdingId && _state.Cards.TryGetValue(holdingId, out var holding):
                {
                    Require(holding.OwnerId == player.Id && holding.Zone == Zone.DynastyDiscard && Definition(holding).Type == CardType.Holding, "Choose a Holding in your discard pile.");
                    if (id == PromoEmperorFateAbilities.FortHolding) Require(Keyword(holding, "Castle"), "Choose a Castle Holding in your discard pile.");
                    var quote = QuotePurchase(player.Id, holding.Id, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                    break;
                }
            case PromoEmperorFateAbilities.GovernAttach when command.TargetId is { } attachmentId && _state.Cards.TryGetValue(attachmentId, out var attachment):
                Require(attachment.Zone == Zone.FateDiscard && Definition(attachment).Type is CardType.Follower or CardType.Item, "Choose a Follower or Item in your discard pile.");
                break;
            case PromoEmperorFateAbilities.CharterCreate:
                Require(player.TurnsTaken > 1, "It is your first turn.");
                break;
            // "Your target Castle": a Castle Holding of the player's (the performer category also admits a Siege card).
            case PromoEmperorFateAbilities.FortMove when command.PerformerId is { } castleId && _state.Cards.TryGetValue(castleId, out var castle):
                Require(Definition(castle).Type == CardType.Holding && Keyword(castle, "Castle"), "Choose one of your Castle Holdings.");
                break;
        }
    }

    // ---- bringing a Holding into play from a discard pile ---------------------------------------------------------------------------------------------------------------------

    // The recruit's price has been paid (PayForAbility, from the ability's TargetRecruit rule). The Holding enters play at home, bowed, a new object (nothing it carried follows it), and its
    // entering-play effects and window open as any recruit's do. Returns true when a window opened.
    private bool PromoBringHoldingIntoPlay(PlayerState player, CardInstance holding, string via)
    {
        ResetCardState(holding);
        holding.Zone = Zone.Home; holding.ProvinceIndex = null; holding.FaceUp = true; holding.Bowed = true;
        Emit("cardEnteredPlay", $"{player.Name} brought {Definition(holding).Name} into play bowed via {via}.", player.Id, holding.Id);
        QueueOverconfidentDraw(holding);
        return OpenRecruitEntryWindows(player, holding, false, null);
    }

    // ---- Peace's search ----------------------------------------------------------------------------------------------------------------------------------------------------------

    internal static readonly SearchSpec PeaceSearch = new("pe-peace", "Search your Dynasty deck for a non-Unique Holding",
        [new(SearchDeck.Dynasty, new("non-Unique Holding", [CardType.Holding], NonUnique: true), SearchEffect.RecruitUnbowed)]);

    // ShadowsEmbraceBatch6's search recruit enters its Holding unbowed (Versatile Farm); Peace's enters bowed.
    private static bool SearchEntersBowed(PendingSearch search) => search.SearchId == PeaceSearch.Id;

    // ---- Imperial Charter ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private string? PromoCharterPrompt(PendingDeciderChoice p) => PromoEmperorFateAbilities.IsCharterSpec(p.SpecId)
        ? $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, you may create an unbowed Holding with the trait \"Bow this card: Produce 2 Gold.\" Choose the Strategy to create it, or none."
        : null;

    // The decision is a yes or no: the Strategy itself is the one "card" to choose (it is public, in the discard pile), and choosing none declines.
    private (List<string> Candidates, int Min, int Max)? PromoCharterOptions(string specId, GameCommand command) =>
        PromoEmperorFateAbilities.IsCharterSpec(specId) && command.CardId is { } strategyId ? ([strategyId], 0, 1) : null;

    private bool PromoApplyCharter(PendingDeciderChoice p)
    {
        if (!PromoEmperorFateAbilities.IsCharterSpec(p.SpecId)) return false;
        var source = Card(p.SourceCardId);
        if (p.Chosen.Count > 0) CreateInPlay(p.DeciderPlayerId, CardCatalog.Get(PromoEmperorFateAbilities.CharterScan), Zone.Home, null, false, " at home", true, null);
        else Emit("deciderChosen", $"{Player(p.DeciderPlayerId).Name} created no Holding ({Definition(source).Name}).", p.DeciderPlayerId, source.Id);
        if (p.SpecId == PromoEmperorFateAbilities.CharterFirst)
        {
            // The other player decides next; the action finishes when he has.
            var other = 1 - p.ActingPlayerId;
            OpenDeciderChoice(new PendingDeciderChoice
            {
                SpecId = PromoEmperorFateAbilities.CharterSecond,
                SourceCardId = p.SourceCardId,
                ActingPlayerId = p.ActingPlayerId,
                DeciderPlayerId = other,
                Candidates = [p.SourceCardId],
                Min = 0,
                Max = 1,
                Paused = p.Paused
            });
        }
        return true;
    }

    // ---- execution ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunPromoRecruits(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case PromoEmperorFateAbilities.ResurrectionRecruit:
            case PromoEmperorFateAbilities.RenewalRecruit:
                {
                    // "Discard a card" was paid as a cost; the recruit's price too. A card no longer in the pile (an action in between) is simply not brought back.
                    var recruit = SlotCards(command, "target").First();
                    if (!IsDiscardedPersonality(recruit)) { Emit("raiseFailed", $"{Definition(recruit).Name} is no longer in the discard pile.", player.Id, recruit.Id); return false; }
                    return BringDeadPersonalityIntoPlay(player, recruit, Definition(source!).Name, null);
                }
            case PromoEmperorFateAbilities.GovernHolding:
            case PromoEmperorFateAbilities.FortHolding:
                if (target!.Zone != Zone.DynastyDiscard) { Emit("raiseFailed", $"{Definition(target).Name} is no longer in the discard pile.", player.Id, target.Id); return false; }
                return PromoBringHoldingIntoPlay(player, target, Definition(source!).Name);
            case PromoEmperorFateAbilities.FortMove:
                ExecuteMove(command, ability, source, target);
                return false;
            case PromoEmperorFateAbilities.PeaceAbility:
                SpendHandPlayedCard(player, source); // a Strategy is spent as it is played: the search finishes the action
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case PromoEmperorFateAbilities.CharterCreate:
                return BeginDeciderChoice(PromoEmperorFateAbilities.CharterFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            default:
                return null;
        }
    }
}
