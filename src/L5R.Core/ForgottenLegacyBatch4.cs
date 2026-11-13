namespace L5R.Core;

/// <summary>Seeking the Truth (oracle-l5r-6566): a player who owes "after this battle ends, if you did not destroy the battlefield's province or an army, discard a
/// card at random" for the battle numbered <see cref="BattleNumber"/>. <see cref="Destroyed"/> is set by that battle's resolution when the player destroyed
/// the province or an enemy army in it.</summary>
public sealed class SeekingTheTruthWatch
{
    public int PlayerId { get; set; }
    public int BattleNumber { get; set; }
    public bool Destroyed { get; set; }
}

// Forgotten Legacy pass (issue #467), batch 4: the helpers behind strategy-wave-55 -- Heart of Darkness, Intimidation, Never Beyond My Reach, Pearls & Spirits,
// Returning Home, Rise of the Dark Naga, Seeking the Truth, Siege Volley, Steadfast Defense and Sundering Strike. Each card's ability definitions and reading are
// documented at its entry in CardAbilities.cs; only the execution/validation helpers too card-specific for a shared file live here. The shared files gained
// only additive arms: two Performer categories (both switches), three Target categories, one discard-pile Target branch, a Performer-subject reading of
// CostExempt (AbilityCosts.cs), one branch condition and two branch effects (EffectChoice.cs), and one after-resolution Reaction condition (AfterResolution.cs).
public sealed partial class MatchEngine
{
    // ---- per-card conditions (called from ValidateAbility for every offer and every submitted command) ----

    private void ValidateForgottenLegacyBatch4(string id, GameCommand command, PlayerState player)
    {
        switch (id)
        {
            // Rise of the Dark Naga: "If you control two or more Nonhuman Personalities" -- in play (the province Personality it recruits is not yet).
            case "dark-naga-rise-recruit":
                Require(_state.Cards.Values.Count(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality && Keyword(c, "Nonhuman")) >= 2,
                    "You must control two or more Nonhuman Personalities.");
                break;
            // Steadfast Defense: "If you control a Temple, you may target a Personality or Follower" -- the optional target is legal only with a Temple. The
            // primary target of the command (or the candidate a slot offer is asking about) is that target.
            case "steadfast-defense-straighten":
                if (command.TargetId is not null) Require(ControlsKeyword(player.Id, "Temple"), "You may target a Personality or Follower only while you control a Temple.");
                break;
            // Sundering Strike: "you may target one of his attachments and pay Gold equal to its Gold Cost minus four" -- the Variable cost is exactly that
            // amount (never below 0) when an attachment is named, and nothing otherwise.
            case "sundering-strike":
            {
                var attachment = SlotCards(command, "attachment").FirstOrDefault();
                var owed = attachment is null ? 0 : Math.Max(0, CardGoldCost(attachment) - 4);
                Require(PaidGoldAmount(command) == owed, attachment is null ? "Pay nothing unless you target one of his attachments."
                    : $"Pay exactly {owed} Gold (the attachment's Gold Cost minus four).");
                break;
            }
        }
    }

    // Never Beyond My Reach: an unbowed Personality with the printed keyword at the current battlefield, or at a battlefield adjacent to it (the defender's
    // province line, AdjacentBattlefields) while he has a Weapon attached.
    private bool AtBattleOrAdjacentWithWeapon(CardInstance card, AbilityDefinition ability) =>
        Definition(card).Type == CardType.Personality && !card.Bowed && HasKeywords(card, ability.RequiredKeywords)
        && (AtBattle(card) || card.Zone == Zone.Battlefield && _state.CurrentBattleProvince is int current && card.ProvinceIndex is int at
            && AdjacentBattlefields(current).Contains(at) && Attachments(card.Id).Any(a => Keyword(a, "Weapon")));

    // Seeking the Truth: "a Battle Strategy" -- a Strategy with a Battle (or Battle/Open) action.
    private static bool IsBattleStrategy(CardInstance card) =>
        (Definition(card).Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var def) && def.Timing is "Battle" or "BattleOrOpen");

    // ---- effects ----

    // Intimidation: "Move them both there. Straighten each unit that this moved." Each mover's unit straightens only when it actually arrived (a negated
    // movement, or a performer already at the battlefield, moved nothing).
    private void IntimidationMove(GameCommand command, AbilityDefinition ability, CardInstance? source, CardInstance target)
    {
        var performer = Card(command.PerformerId!);
        var performerWasThere = AtBattle(performer);
        ExecuteMove(command, ability, source, target);
        if (!performerWasThere && AtBattle(performer)) StraightenUnit(performer);
        if (AtBattle(target)) StraightenUnit(target);
    }

    // Rise of the Dark Naga: the shared recruit entry sequence into HOME at the full recruit price (Shinden Shorai's shape), then "Refill the province
    // face-up" (Kharmic's own face-up replacement). True when an entry window opened.
    private bool RiseOfTheDarkNagaRecruit(PlayerState player, CardInstance recruit, GameCommand command)
    {
        var quote = QuotePurchase(player.Id, recruit.Id, command.ClanDiscount, command.ClanAllegiance); // issue #1232: the way to pay the player chose
        Require(quote.Error is null && Definition(recruit).Type == CardType.Personality, quote.Error ?? "");
        var province = player.Provinces.Single(p => p.CardId == recruit.Id);
        var opened = BringProvincePersonalityIntoPlay(player, recruit, quote, null, "Rise of the Dark Naga", proclaimResumesTo: 1 - player.Id);
        if (province.CardId is string refilled && !Card(refilled).FaceUp)
        {
            Card(refilled).FaceUp = true;
            Emit("provinceRefilledFaceUp", $"{player.Name} refilled province {province.Index + 1} face-up with {Definition(Card(refilled)).Name}.", player.Id, refilled);
        }
        return opened;
    }

    // Seeking the Truth: Reconnaissance at the current battlefield, the chosen Strategy into the hand, and the watch settled when the battle ends.
    private void SeekingTheTruth(GameCommand command, PlayerState player)
    {
        GrantReconnaissanceAtCurrentBattlefield(player.Id);
        foreach (var strategy in SlotCards(command, "strategy").ToArray())
        {
            if (strategy.Zone != Zone.FateDiscard || P2CardIntoHandBlocked(player.Id)) continue; // issue #503: The Scorpion Wall is Finished
            strategy.Zone = Zone.Hand; strategy.FaceUp = true;
            player.Hand.Add(strategy.Id); NoteCardIntoHand(player.Id); // Isawa Kimi - exp 2 (issue #477, batch 5)
            Emit("cardReturnedToHand", $"{player.Name} put {Definition(strategy).Name} from the discard pile into their hand.", player.Id, strategy.Id);
        }
        _state.SeekingTheTruthWatches.Add(new() { PlayerId = player.Id, BattleNumber = _state.BattleNumber });
    }

    // The battle's resolution: a watcher who destroyed the province or an enemy army in it owes nothing.
    private void MarkSeekingTheTruthResolution(int[] destroyedByPlayer, int? provinceDestroyedBy)
    {
        foreach (var watch in _state.SeekingTheTruthWatches.Where(w => w.BattleNumber == _state.BattleNumber))
            if (destroyedByPlayer[watch.PlayerId] > 0 || provinceDestroyedBy == watch.PlayerId) watch.Destroyed = true;
    }

    // The battle has ended: each watcher of it who destroyed neither discards a card at random (the persisted generator, as a mid-game shuffle is).
    private void ResolveSeekingTheTruthWatches()
    {
        var due = _state.SeekingTheTruthWatches.ToArray();
        _state.SeekingTheTruthWatches.Clear();
        foreach (var watch in due)
        {
            if (watch.BattleNumber != _state.BattleNumber || watch.Destroyed) continue;
            var player = Player(watch.PlayerId);
            if (player.Hand.Count == 0) continue;
            var hand = player.Hand.ToList();
            var random = new StableRandom(_state.RngState);
            random.Shuffle(hand);
            _state.RngState = random.State;
            Emit("randomDiscard", $"Seeking the Truth: {player.Name} destroyed neither the province nor an army and discards a card at random.", player.Id);
            DiscardFate(player, Card(hand[0]));
        }
    }

    // Siege Volley: "Give the current Province a strength bonus or penalty equal to the number of Castles you control." Like every other Province Strength
    // change (Expensive Achievement, Ring of Earth), applied to the current battlefield's (the Defender's) province directly.
    private void SiegeVolleyStrength(int playerId, bool raise)
    {
        var castles = _state.Cards.Values.Count(c => c.OwnerId == playerId && InPlay(c) && Keyword(c, "Castle"));
        if (CurrentBattlefieldProvince() is not { } province) return; // a battlefield without a province has no strength to change (issue #492, rulings pass R1)
                GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, province.Index, raise ? castles : -castles, "Siege Volley"); // until end of turn (issue #492 ruling R3)
    }

    // Sundering Strike: "If you did, destroy it. Move the Personality home."
    private void SunderingStrike(GameCommand command, CardInstance personality, int playerId)
    {
        if (SlotCards(command, "attachment").FirstOrDefault() is { Zone: Zone.Attached } attachment && attachment.AttachedTo == personality.Id) DestroyTargetCard(attachment, playerId);
        if (InPlay(personality)) MoveHome(personality, false);
    }

    // ---- Heart of Darkness: its before-resolution trait, answered through "beforeBattleOptional" ----

    private bool IsHeartOfDarknessChoice(PendingBeforeBattleResolve pending) =>
        pending.ActiveSourceId is { } itemId && _state.Cards.TryGetValue(itemId, out var source) && BeforeResolutionAbilityId(source) is "heart-of-darkness-bow" or "rocky-terrain-bow"; // Rocky Terrain: Emperor Edition remaining-cards pass, group C (issue #492)

    // "Cards at the current battlefield with 4 or greater Force": the Personalities, Followers and Items of every unit there, either side's. A bowed card is not
    // offered (bowing it again would do nothing).
    private IEnumerable<CardInstance> HeartOfDarknessCandidates(int provinceIndex, string? sourceId = null) =>
        sourceId is not null && _state.Cards.TryGetValue(sourceId, out var trait) && BeforeResolutionAbilityId(trait) == "rocky-terrain-bow" ? RockyTerrainCandidates(provinceIndex) :
        _state.Cards.Values.Where(c => !c.Bowed && Definition(c).Type is CardType.Personality or CardType.Follower or CardType.Item
                && (c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex
                    || c.Zone == Zone.Attached && c.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == provinceIndex)
                && CardForce(c) >= 4)
            .OrderBy(c => c.Id, StringComparer.Ordinal);

    // "use" names one card (TargetId) or two (TargetId and CardIds[0]); "decline" bows none -- "zero to two".
    private void ResolveHeartOfDarkness(GameCommand command, PendingBeforeBattleResolve pending, int playerId)
    {
        var traitName = pending.ActiveSourceId is { } traitId && _state.Cards.TryGetValue(traitId, out var traitCard) ? Definition(traitCard).Name : "Heart of Darkness";
        if (command.Choice != "use") { Emit("beforeBattleEffect", $"{traitName}: no card was bowed.", playerId); return; }
        var ids = new[] { command.TargetId }.Concat(command.CardIds ?? []).Where(i => i is not null).Select(i => i!).ToArray();
        var legal = HeartOfDarknessCandidates(pending.ProvinceIndex, pending.ActiveSourceId).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        Require(ids.Length is 1 or 2 && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length && ids.All(legal.Contains),
            legal.Count > 0 && pending.ActiveSourceId is { } rockyId && _state.Cards.TryGetValue(rockyId, out var rocky) && BeforeResolutionAbilityId(rocky) == "rocky-terrain-bow" ? "Choose one or two different unbowed attacking Personalities at this battlefield." : "Choose one or two different unbowed cards at this battlefield with 4 or greater Force.");
        foreach (var id in ids)
        {
            var card = Card(id);
            if (!StalwartNegatesBow(card, playerId)) card.Bowed = true;
            Emit("beforeBattleEffect", $"{traitName}: {Definition(card).Name} bowed.", playerId, card.Id);
        }
    }

    private IEnumerable<ActionOption> HeartOfDarknessOptions(int playerId, string? sourceId)
    {
        if (_state.PendingBeforeBattleResolve is not { } pending || _state.PendingChoice?.PlayerId != playerId) yield break;
        yield return new("beforeBattleOptional", "Bow no card", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, Choice: "decline"), sourceId);
        var candidates = HeartOfDarknessCandidates(pending.ProvinceIndex, sourceId ?? pending.ActiveSourceId).ToArray();
        for (var i = 0; i < candidates.Length; i++)
        {
            yield return new("beforeBattleOptional", $"Bow {Definition(candidates[i]).Name}", true, null,
                new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: candidates[i].Id, Choice: "use"), candidates[i].Id);
            for (var j = i + 1; j < candidates.Length; j++)
                yield return new("beforeBattleOptional", $"Bow {Definition(candidates[i]).Name} and {Definition(candidates[j]).Name}", true, null,
                    new GameCommand("beforeBattleOptional", playerId, _state.Version, CardIds: [candidates[j].Id], TargetId: candidates[i].Id, Choice: "use"), candidates[i].Id);
        }
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterForgottenLegacyBatch4Hooks()
    {
        EngineHooks.ValidateMiddle.Register("ValidateForgottenLegacyBatch4", 100, static (engine, context) => engine.ValidateForgottenLegacyBatch4(context.id, context.command, context.player));
    }
}
