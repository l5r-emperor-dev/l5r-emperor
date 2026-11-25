namespace L5R.Core;

// Conditional effect choice (issue #337): an action whose effect branches on a condition evaluated when it RESOLVES, and that offers the
// acting player an engine-listed choice only on the "may" branch. Mirumoto Yumaru is the first card:
//
//   "Target an enemy Personality: If Yumaru has a Weapon, you may move her home and you may move the Personality home. Otherwise, move
//    both of them home."
//
// Mendon's ruling (verbatim in the decision doc): with a Weapon the player chooses one of THREE effects -- send Yumaru home, send the
// enemy Personality home, send both; without one the player "is not presented with any choices" and both are sent home.
//
// The shape is data on the ability (AbilityDefinition.Branch): a Condition, the Options offered when it holds (each a named list of
// effects), and the Otherwise option that simply happens when it does not. The flow when the action resolves:
//
//   1. The condition is read NOW -- after every "before this action resolves" reaction has had its say (a Weapon a reaction removed no
//      longer earns the choice; one it added does), not when the action was announced.
//   2. It fails: the Otherwise effects apply and the action carries on to its ordinary end. No decision exists, so none is asked.
//   3. It holds: a public "effectChoice" decision opens for the acting player, listing each option (QueryActions offers one
//      "chooseEffect" action per option, so a client that lists offered actions already shows it). The action waits.
//   4. The chosen option's effects apply, the negations a reaction set for this action are honoured exactly as they would have been
//      inline, and the action then completes through the same tail every action ends with (CompleteAbilityResolution).
//
// Nothing about the choice is hidden: the options, the chosen option and its effects are public, as for any resolved action.

/// <summary>One thing the player may choose: a stable id (GameCommand.Choice names it), the label QueryActions shows, and the effects it
/// applies in order (BranchEffect names).</summary>
public sealed record EffectOption(string Id, string Label, string[] Effects);

/// <summary>A branch. When <paramref name="Condition"/> (a BranchCondition) holds at resolution the player picks one of
/// <paramref name="Options"/>; otherwise <paramref name="Otherwise"/> applies with no choice.</summary>
public sealed record ConditionalChoice(string Condition, EffectOption[] Options, EffectOption Otherwise);

/// <summary>Conditions a branch may test at resolution. The source is the ability's own card, the target its chosen target.</summary>
public static class BranchCondition
{
    /// <summary>The source card has at least one attached Weapon.</summary>
    public const string SourceHasWeapon = "source-has-weapon";
    /// <summary>The target is a dishonorable Personality (issue #336: Doji Yoshitada's second sentence, judged after his first has resolved).</summary>
    public const string TargetDishonorable = "target-dishonorable";
    /// <summary>The target actually moved (issue #336: Fear Me!'s "If he moved"); the ability's own effect says whether it did.</summary>
    public const string TargetMoved = "target-moved";
    /// <summary>The target stands at a battlefield (Discovering a Conspiracy: "If he is at a battlefield").</summary>
    public const string TargetAtBattlefield = "target-at-battlefield";
    /// <summary>A card sits in the current battlefield's province (Silent Terror: "you may discard a card in the current battlefield's province").</summary>
    public const string ProvinceHoldsCard = "province-holds-card";
    /// <summary>The Personality the source is attached to has a keyword (Strength of the Tsunami: "if this Shugenja is Water").</summary>
    public static string WielderHas(string keyword) => $"wielder-has:{keyword}";
    /// <summary>The action's optional target was chosen (Forgotten Legacy pass, issue #467, batch 4 -- Steadfast Defense: "you may target a Personality or Follower and bow or
    /// straighten it" asks bow-or-straighten only when a target was named).</summary>
    public const string TargetChosen = "target-chosen";
    /// <summary>The target is dishonorable, Kolat, Ninja or Shadowlands (The Shadow's Embrace pass, issue #476 batch 3 -- Captured Convict, judged after he has moved home).</summary>
    public const string TargetDishonorableKolatNinjaOrShadowlands = "target-dishonorable-kolat-ninja-or-shadowlands";
    /// <summary>No condition: the choice is always asked (The Shadow's Embrace pass, issue #476, batch 4 -- Running Battle's "You may move your Scout home").</summary>
    public const string Always = "always";
    /// <summary>The target is Kolat, Ninja, or Shadowlands (Emperor Edition Personality pass, issue #477 -- Asako Tsunefusa's second sentence).</summary>
    public const string TargetKolatNinjaOrShadowlands = "target-kolat-ninja-or-shadowlands";
    /// <summary>The target is Kolat, Ninja, or Shadowlands, the acting player controls an Inquisitor, and it is not his turn (Emperor Edition Stronghold pass, issue #486 batch 2 -- Temple of Purity).</summary>
    public const string TargetKolatNinjaOrShadowlandsWithInquisitorOffTurn = "target-kolat-ninja-or-shadowlands-inquisitor-off-turn";
}

/// <summary>Effects an option may apply. Each goes through the same helper an ordinary effect would, so a negation a reaction set
/// (Tamori Tamoko's movement, Champion of Thunder's "all") still applies to it.</summary>
public static class BranchEffect
{
    /// <summary>The source card's unit moves home.</summary>
    public const string MoveSourceHome = "move-source-home";
    /// <summary>The target's unit moves home.</summary>
    public const string MoveTargetHome = "move-target-home";
    /// <summary>A player gains (positive) or loses (negative) Honor because of the action (issue #336): "choose a player who gains or loses 2 Honor". Who
    /// is relative to the player who took the action ("self" is him, "opponent" the other player), so the option's label reads the same for both seats.
    /// Applied by ApplyPlayerHonorChoice, so a gain for a dishonorable performer's own controller rehonors him instead.</summary>
    public static string Honor(bool self, int amount) => $"honor:{(self ? "self" : "opponent")}:{amount}";
    /// <summary>The card in the current battlefield's province is discarded (and the province refilled), whoever's it is (Silent Terror).</summary>
    public const string DiscardProvinceCard = "discard-province-card";
    /// <summary>The Personality the source is attached to is straightened (and only he: "Straighten him").</summary>
    public const string StraightenWielder = "straighten-wielder";
    /// <summary>The Personality the source is attached to moves home.</summary>
    public const string MoveWielderHome = "move-wielder-home";
    /// <summary>Celestial Sword of the Scorpion - exp (issue #453 batch 5): the card in the current battlefield's province is discarded, then the target is destroyed if that card was a
    /// Personality or the acting player controls a Courtier; and the same conditional destroy with nothing discarded (MatchEngine.ScorpionSwordResolve).</summary>
    public const string ScorpionSwordDiscard = "scorpion-sword-discard";
    public const string ScorpionSwordKeep = "scorpion-sword-keep";
    /// <summary>The target is bowed / straightened (Steadfast Defense, issue #467 batch 4).</summary>
    public const string BowTarget = "bow-target";
    public const string StraightenTarget = "straighten-target";
    /// <summary>The action's named performer's unit moves home (Running Battle, issue #476 batch 4).</summary>
    public const string MovePerformerHome = "move-performer-home";
}

/// <summary>The four answers to "choose a player who gains or loses N Honor" (issue #336): each player, each direction. Shared by every card that prints it. Ordered with the two usual answers
/// first (Mendon, 2026-11-25): you gain, your opponent loses, then your opponent gains, you lose. The Either/Or copies (Remember Your Ancestors, The Shadow Court) and Wave733 follow it.</summary>
public static class HonorChoice
{
    public static EffectOption[] Options(int amount) =>
    [
        new("self-gain", $"You gain {amount} Honor", [BranchEffect.Honor(true, amount)]),
        new("opponent-lose", $"Your opponent loses {amount} Honor", [BranchEffect.Honor(false, -amount)]),
        new("opponent-gain", $"Your opponent gains {amount} Honor", [BranchEffect.Honor(false, amount)]),
        new("self-lose", $"You lose {amount} Honor", [BranchEffect.Honor(true, -amount)]),
    ];
    /// <summary>What happens when the condition fails: nothing, and nothing is asked.</summary>
    public static readonly EffectOption None = new("none", "No Honor change", []);
}

/// <summary>Everything a paused action needs to finish once the player has chosen (MatchState.PendingEffectChoice). Public state, no hidden
/// information: the command is the one both players already saw announced.</summary>
public sealed class PendingEffectChoice
{
    public GameCommand Command { get; set; } = null!;
    public int PlayerId { get; set; }
    /// <summary>Negations a before-action reaction set for this command. They were cleared when the deferred command returned; they are
    /// restored while the chosen effects apply, so a negated move stays negated.</summary>
    public List<NegatedEffect> Negated { get; set; } = [];
    public bool ConsumedGrant { get; set; }
    public bool PlayedFromDiscard { get; set; }
    public int? OpponentHonorBefore { get; set; }
    public List<string> BowedBefore { get; set; } = [];
    public List<AdditionalActionGrant> PreExistingGrants { get; set; } = [];
    /// <summary>Options the engine listed for THIS resolution, when they depend on the game (Forgotten Legacy pass, issue #467, batch 1: "choose one of his
    /// abilities", "destroy a card without attachments in his unit"); null for a branch whose options are the ability's own fixed data.</summary>
    public List<EffectOption>? Options { get; set; }
}

public sealed partial class MatchEngine
{
    // The branch an ability carries: its own Branch (Yumaru), or the conditional follow-up slot of a multi-target ability (TargetSlots.cs).
    private static ConditionalChoice? FollowUpOf(AbilityDefinition ability) => ability.Branch ?? ability.Slots?.FirstOrDefault(s => s.Resolution is not null)?.Resolution;

    private bool BranchConditionMet(string condition, CardInstance source, CardInstance? target, bool targetMoved) => condition switch
    {
        BranchCondition.SourceHasWeapon => Attachments(source.Id).Any(a => Keyword(a, "Weapon")),
        BranchCondition.TargetDishonorable => target is not null && MatchesTargetStatus(target, TargetStatus.IsDishonorable),
        BranchCondition.TargetMoved => targetMoved,
        BranchCondition.TargetAtBattlefield => target is not null && target.Zone == Zone.Battlefield,
        BranchCondition.ProvinceHoldsCard => CurrentBattlefieldProvince() is { CardId: not null },
        BranchCondition.TargetChosen => target is not null,
        BranchCondition.TargetDishonorableKolatNinjaOrShadowlands => target is not null && MatchesTargetStatus(target, TargetStatus.DishonorableKolatNinjaOrShadowlands),
        BranchCondition.Always => true,
        BranchCondition.TargetKolatNinjaOrShadowlands => target is not null && MatchesTargetStatus(target, TargetStatus.KolatNinjaOrShadowlands),
        BranchCondition.TargetKolatNinjaOrShadowlandsWithInquisitorOffTurn => TempleHonorConditionMet(source, target),
        _ when condition.StartsWith("wielder-has:", StringComparison.Ordinal) => source.AttachedTo is { } wielderId && Keyword(Card(wielderId), condition["wielder-has:".Length..]),
        _ => throw new InvalidOperationException($"Unknown branch condition '{condition}'.")
    };

    private void ApplyBranchEffect(string effect, GameCommand command, CardInstance source, CardInstance? target)
    {
        if (effect.StartsWith("honor:", StringComparison.Ordinal))
        {
            var parts = effect.Split(':');
            var affected = parts[1] == "self" ? command.PlayerId : 1 - command.PlayerId;
            // The performer, not the Strategy, is who the rehonor rule looks at when the action names one.
            var performer = command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var named) ? named : source;
            ApplyPlayerHonorChoice(performer, affected, int.Parse(parts[2]));
            return;
        }
        if (ApplyForgottenLegacyBatch1BranchEffect(effect, command)) return;
        if (ApplyWlStratTornBranchEffect(effect, command)) return; // issue #674 (WlStratTornRun.cs): A Threat Enacted's destroy-then-dishonor option.
        if (ApplyWlEssenceBranchEffect(effect, command)) return; // issue #699 (WlEssence.cs): Read the Essence's optional second flip.
        if (ApplyAbilityRemovalBranchEffect(effect, command)) return; // issue #667 (AbilityRemoval.cs): permanent removal options.
        if (ApplyWlPersTornBranchEffect(effect, command)) return; // issue #674 (WlPersTorn.cs): Asahina Nanae - exp's options.
        if (ApplyShadowsEmbraceBatch1BranchEffect(effect, command)) return; // The Shadow's Embrace pass (issue #476), batch 1.
        if (ApplyShadowsEmbraceBatch3BranchEffect(effect, command)) return; // The Shadow's Embrace pass (issue #476), batch 3
        if (ApplyShadowsEmbraceBatch4BranchEffect(effect, command)) return; // The Shadow's Embrace pass (issue #476), batch 4: The Game Has Changed's steps.
        if (ApplyEmperorBatch4BranchEffect(effect, command)) return; // Emperor Edition Personality pass (issue #477), batch 4.
        if (ApplyEmperorRemainingDBranchEffect(effect, command)) return; // Emperor Edition remaining-cards pass (issue #492), group D: The Sincerity of the Dragon's named number.
        if (ApplyRulingR2BranchEffect(effect, command)) return;
        if (ApplyEmperorP4BranchEffect(effect, command)) return; // Issue #503, group P4 (EmperorP4.cs).
        if (ApplyWlPersCoilsBranchEffect(effect, command)) return; // issue #674 (WlPersCoils.cs): Asako Chukage - exp2's removal of a Ring and the target from the game.
        if (ApplyEmperorMechABranchEffect(effect, command)) return; // issue #501 (EmperorMechA.cs): String of Victories' Ring, Aramasu's Legacy's bow // Emperor Edition ruling pass R2 (issue #492): The Honesty of the Phoenix's named card.
        if (RegisteredBranchEffect(effect, command, source, target)) return; // P25 (DecisionInfra.cs)
        switch (effect)
        {
            case BranchEffect.MoveSourceHome: MoveHome(source, false); break;
            case BranchEffect.MoveTargetHome: MoveHome(target!, false); break;
            case BranchEffect.DiscardProvinceCard: DiscardCurrentProvinceCard(); break;
            case BranchEffect.StraightenWielder: Straighten(Card(source.AttachedTo)); break;
            case BranchEffect.MoveWielderHome: MoveHome(Card(source.AttachedTo), false); break;
            case BranchEffect.ScorpionSwordDiscard: ScorpionSwordResolve(command, target!, discardFirst: true); break;
            case BranchEffect.ScorpionSwordKeep: ScorpionSwordResolve(command, target!, discardFirst: false); break;
            case BranchEffect.BowTarget: if (InPlay(target!) && !StalwartNegatesBow(target!, command.PlayerId)) target!.Bowed = true; break;
            case BranchEffect.StraightenTarget: if (InPlay(target!)) Straighten(target!); break;
            case BranchEffect.MovePerformerHome: if (command.PerformerId is not null && InPlay(Card(command.PerformerId))) MoveHome(Card(command.PerformerId), false); break;
            default: throw new InvalidOperationException($"Unknown branch effect '{effect}'.");
        }
    }

    // (The current battlefield's province, null when it is at none: EmperorRulingR1.cs.)

    // "Discard a card in the current battlefield's province": the Dynasty card sitting there, face up or face down (a discard pile is public, so it is shown), and the
    // province is refilled as after any discard from it.
    private void DiscardCurrentProvinceCard()
    {
        if (CurrentBattlefieldProvince() is not { CardId: { } cardId } province) return;
        var card = Card(cardId); var owner = Player(card.OwnerId);
        card.Zone = Zone.DynastyDiscard; card.ProvinceIndex = null; card.FaceUp = true;
        province.CardId = null;
        Emit("provinceDiscard", $"{Definition(card).Name} was discarded from {owner.Name}'s province.", owner.Id, card.Id);
        Refill(owner, province);
    }

    // Called from the ability's execution case. True when the action paused for a decision (the caller returns without finishing);
    // false when the Otherwise branch already applied and the action carries on.
    // targetMoved: only for a condition the ability's own effect settles (Fear Me!'s "if he moved"). A conditional follow-up is prompted ONLY when
    // its condition holds (Mendon's ruling, issue #336): the sentence is independent of the one before it, so a failed condition opens nothing.
    private bool BeginConditionalChoice(GameCommand command, AbilityDefinition ability, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBefore, HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants, bool targetMoved = false)
    {
        var branch = FollowUpOf(ability)!;
        var source = Card(command.CardId);
        var target = command.TargetId is null ? null : Card(command.TargetId);
        if (!BranchConditionMet(branch.Condition, source, target, targetMoved))
        {
            foreach (var effect in branch.Otherwise.Effects) ApplyBranchEffect(effect, command, source, target);
            return false;
        }
        _state.PendingEffectChoice = new()
        {
            Command = command,
            PlayerId = command.PlayerId,
            Negated = _state.NegatedEffects.ToList(),
            ConsumedGrant = consumedGrant,
            PlayedFromDiscard = playedFromDiscard,
            OpponentHonorBefore = opponentHonorBefore,
            BowedBefore = bowedBefore.ToList(),
            PreExistingGrants = preExistingGrants.ToList()
        };
        _state.PendingChoice = new("effectChoice", command.PlayerId, source.Id,
            $"{Definition(source).Name}: choose one — {string.Join("; ", branch.Options.Select(o => o.Label))}.");
        _state.PriorityPlayerId = command.PlayerId; _state.ConsecutivePasses = 0;
        return true;
    }

    private void ResolveEffectChoice(GameCommand command)
    {
        var pending = _state.PendingEffectChoice ?? throw new RuleViolation("No effect choice is open.");
        Require(command.Type == "chooseEffect" && command.PlayerId == pending.PlayerId, "Choose one of the offered effects.");
        var ability = AbilityCatalog.All[pending.Command.AbilityId!];
        var option = (pending.Options?.ToArray() ?? FollowUpOf(ability)!.Options).FirstOrDefault(o => o.Id == command.Choice);
        Require(option is not null, "Choose one of the offered effects.");
        var source = Card(pending.Command.CardId);
        var target = pending.Command.TargetId is null ? null : Card(pending.Command.TargetId);
        _state.PendingChoice = null; _state.PendingEffectChoice = null;
        Emit("effectChosen", $"{Player(pending.PlayerId).Name} chose: {option!.Label}.", pending.PlayerId, source.Id);
        _state.NegatedEffects = pending.Negated.ToList();
        // The chosen effects are the paused action's own: they resolve as ITS effects (issue #496: an action whose destruction will not be negated or delayed -- Makito's -- must still be that action here).
        var runningBefore = _runningCommand;
        _runningCommand = pending.Command;
        try { foreach (var effect in option.Effects) ApplyBranchEffect(effect, pending.Command, source, target); }
        finally { _runningCommand = runningBefore; }
        _state.NegatedEffects.Clear();
        // The Shadow's Embrace pass (issue #476), batch 4: a choice with further steps (The Game Has Changed) opens its next step and waits.
        if (ContinueShadowsEmbraceBatch4Choice(pending, option!)) return;
        if (ContinueEmperorP4Choice(pending, option!)) return; // Issue #503 (EmperorP4More.cs): Secret Reserve's next Item, Power of Strength's deferred Attack.
        if (RegisteredEffectChoiceContinue(pending, option!)) return; // P25 (DecisionInfra.cs): a chosen option that opens a further decision
        CompleteAbilityResolution(pending.Command, ability, pending.ConsumedGrant, pending.PlayedFromDiscard, pending.OpponentHonorBefore,
            pending.BowedBefore.ToHashSet(StringComparer.Ordinal), pending.PreExistingGrants);
    }

    // The offered answers to an open effectChoice: one "chooseEffect" action per option, each a complete command.
    private IEnumerable<ActionOption> EffectChoiceActions(int playerId)
    {
        if (_state.PendingEffectChoice is not { } pending || pending.PlayerId != playerId) yield break;
        var ability = AbilityCatalog.All[pending.Command.AbilityId!];
        foreach (var option in pending.Options?.ToArray() ?? FollowUpOf(ability)!.Options)
            yield return new("chooseEffect", option.Label, true, null, new GameCommand("chooseEffect", playerId, _state.Version, Choice: option.Id), pending.Command.CardId);
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredEffectChoice(string type, GameCommand command)
    {
        if (type != "effectChoice") return false;
        WithResumedAction(() => ResolveEffectChoice(command));
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEffectChoiceHooks()
    {
        EngineHooks.PendingChoice.Register("HandleRegisteredEffectChoice", 4100, static (engine, type, command) => engine.HandleRegisteredEffectChoice(type, command));
    }
}
