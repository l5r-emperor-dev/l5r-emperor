namespace L5R.Core;

// ======================================================================================================================================================================================
// EMPEROR EDITION THREE-SCANS PASS (issue #496): the three cards whose scans Mendon supplied after the remaining-cards pass left them unwired -- Honor Never Falls (Forgotten Legacy, a Strategy,
// strategy-wave-65), Kurohito's Perfection (Torn Asunder, a Celestial, celestial-wave-9) and Makito (Embers of War, a Personality, personality-wave-79). Each was read letter by letter against its scan.
//
// New primitives (each reads the core rules; none conflicts with them):
//   * "(FROM ANY SOURCE)" DUEL DELAY (Honor Never Falls' Focus Effect). The R2 duel delay (EmperorRulingR2Cards.cs) records the effects the duel's OUTCOME applies to the delaying player's losing unit. This one is
//     mandatory (no "(now)" choice) and its DelayedLossChoice carries AnySource: besides the outcome, every effect a Focus Effect of ANOTHER player applies to that unit is recorded too (WithAnySourceLossDelays,
//     wrapped around each Focus Effect's application and around a Focus Effect's decision), all until the end of the turn (DelayUntil.EndOfTurn, applied at the top of EndTurn).
//   * A BASE-ABILITY-ONLY ADDITIONAL ACTION (Honor Never Falls' Battle half): AdditionalActionGrant.BaseAbilityOnly -- with the grant's PerformerId, spent only by one of the abilities PRINTED on that Personality
//     (his own definition's), never by one an attachment provides or one granted to him.
//   * A DELAYED DRAW (Kurohito's Perfection): DelayedKinds.Draw, an effect on a PLAYER recorded in the R2 store, applied when the End Phase begins (DelayUntil.EndPhaseBegins). The rulebook Favor Political
//     Limited action's twin ("favor-cycle-kurohito") is the same action paid with the discard of a card ignored, offered only to a player who controls the Celestial: the whole action's effects (a draw) are the
//     recorded effect. It shares the rulebook action's per-turn use and every rule that names it (Ring of Air's count of Favor actions, the Political classification, the Favor discard as a cost).
//   * A TRAIT ON ANY CARD'S DESTRUCTION (Makito): AfterCardDestroyed, called where a card is destroyed (a unit or its attachments, a lone attachment, a Region).
//   * A DESTRUCTION THAT WILL NOT BE NEGATED OR DELAYED (Makito's Maho Battle): the action counts as one whose destruction is never negated or delayed, the same question Yoritomo Hiromi's Battle actions and The Shadow's
//     Lair answer (HiromiPerforms -> HiromiUnnegatable): NegatesEffect, Delay (CannotBeDelayed), Wide Valley, Megumi's Guidance, Fields of Mercy, Negishi, Gift Armor and the whole-action protection all read it. The
//     effect choice that picks the card to destroy resolves as the action's own (ResolveEffectChoice sets the running command).
// ======================================================================================================================================================================================
public static class EmperorThreeSAbilities
{
    public const string HonorNeverFallsFocus = "honor-never-falls-focus";
    public const string HonorNeverFallsStraighten = "honor-never-falls-straighten";
    public const string KurohitoFavorDraw = "favor-cycle-kurohito";
    public const string MakitoDestroy = "makito-maho-destroy";
    public const string KurohitoEffect = "kurohito-perfection";
    public const string MakitoEffect = "makito-trait";
    public const string OwnBowedPersonalityAtBattle = "three-s-own-bowed-personality-at-battle";
    public const string EnemyPersonalityChiLteSelf = "three-s-enemy-personality-chi-lte-self";

    public static readonly (string Id, string Keyword)[] KurohitoKeywords = [("kurohito-perfection-cavalry", "Cavalry"), ("kurohito-perfection-duelist", "Duelist"), ("kurohito-perfection-tactician", "Tactician")];

    public static Dictionary<string, AbilityDefinition> AddEmperorThreeS(this Dictionary<string, AbilityDefinition> all)
    {
        // Honor Never Falls (oracle-l5r-3426, Unique, gold cost 0, Focus 3): "As a Focus Effect: Delay all consequences of losing this duel (from any source) until the end of the turn."
        all[HonorNeverFallsFocus] = new("FocusEffect", "As a Focus Effect: Delay all consequences of losing this duel (from any source) until the end of the turn", "none");
        // "Battle: Choose your performing bowed Personality: Straighten him. If he is Unique, you may take an additional Battle action to use one of his base abilities."
        all[HonorNeverFallsStraighten] = new("Battle", "Choose your performing bowed Personality: Straighten him. If he is Unique, you may take an additional Battle action to use one of his base abilities", "none",
            Performer: OwnBowedPersonalityAtBattle);
        // The rulebook Favor Political Limited action, paid the way Kurohito's Perfection allows: "When paying the cost of the rulebook Favor Political Limited action: You may ignore the cost of discarding a
        // card; if you did, delay its effects until your End Phase begins." The Imperial Favor is still discarded (only the discard of a card is ignored); the action's one effect, a draw, is delayed.
        all[KurohitoFavorDraw] = new("Limited", "Favor Political Limited (Kurohito's Perfection): Discard the Imperial Favor, ignoring the cost of discarding a card: Draw a card, delayed until your End Phase begins", "none", true, UsageKey: "favor-cycle", ActionKeywords: ["Political"]);
        // Kurohito's Perfection (oracle-l5r-10258, a Unique Fortune Celestial): "Limited: Discard this card and target a Personality: Permanently give him Cavalry, Duelist, or Tactician." -- one action per
        // keyword sharing one use (the Manipulate Energies shape); the discard is the cost of whichever is taken.
        foreach (var (id, keyword) in KurohitoKeywords)
            all[id] = new("Limited", $"Discard this card and target a Personality: Permanently give him {keyword}", "personality", UsageKey: "kurohito-perfection", Costs: [new(CostVerbs.DiscardFromPlay)]);
        // Makito (oracle-l5r-4806): "Maho Battle: Target an enemy Personality with equal or lower Chi: Destroy a card without attachments in his unit; this destruction will not be negated or delayed."
        // "Maho" is a plain classification word (Asako Rikate's precedent).
        all[MakitoDestroy] = new("Battle", "Maho Battle: Target an enemy Personality with equal or lower Chi: Destroy a card without attachments in his unit; this destruction will not be negated or delayed", EnemyPersonalityChiLteSelf);
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- Performer and Target categories (with their listing twins) ----

    private bool ThreeSPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // Honor Never Falls: "your performing bowed Personality" -- at the current battlefield while acting as a Battle action.
        EmperorThreeSAbilities.OwnBowedPersonalityAtBattle => Definition(performer).Type == CardType.Personality && performer.Bowed && (!actingAsBattle || AtBattle(performer)),
        _ => false
    };

    private string?[]? ThreeSPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer is { } category && category.StartsWith("three-s-", StringComparison.Ordinal)
        ? _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && ThreeSPerformerAllows(ability, c, actingAsBattle)).Select(c => (string?)c.Id).ToArray()
        : null;

    private bool ThreeSTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        // Makito: "an enemy Personality with equal or lower Chi" -- current Chi, the reading of every Chi-comparing category (issue #336), compared with the Personality the action is printed on.
        EmperorThreeSAbilities.EnemyPersonalityChiLteSelf => Definition(target).Type == CardType.Personality && target.OwnerId != player.Id && source is not null && PersonalityChi(target) <= PersonalityChi(source),
        _ => false
    };

    // ---- validation ----

    private bool ControlsKurohitosPerfection(int playerId) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.Celestial && Definition(c).Effect == EmperorThreeSAbilities.KurohitoEffect);

    // The Favor twin is only on offer to a player who controls the Celestial (a standing player ability that is dormant otherwise, Assault in the Jungle's shape).
    private bool ThreeSPlayerAbilityDormant(string abilityId, int playerId) => abilityId == EmperorThreeSAbilities.KurohitoFavorDraw && !ControlsKurohitosPerfection(playerId);

    private void ValidateEmperorThreeS(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (id == EmperorThreeSAbilities.KurohitoFavorDraw) Require(ControlsKurohitosPerfection(player.Id), "You control no Kurohito's Perfection.");
    }

    // ---- execution: null when the id is not this pass's; false when its effects are done; true when a decision opened ----

    private bool? RunEmperorThreeS(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            // Honor Never Falls' Battle half: the performer is straightened; if he is Unique the additional Battle action is granted -- his, and only for one of his base (printed) abilities.
            case EmperorThreeSAbilities.HonorNeverFallsStraighten:
                {
                    var performer = Card(command.PerformerId!);
                    Straighten(performer);
                    if (Keyword(performer, "Unique")) GrantAdditionalAction(player.Id, null, performerId: performer.Id, baseAbilityOnly: true);
                    return false;
                }
            // Kurohito's Perfection's trait: the action's effect (a draw) is recorded, to be applied as the End Phase begins.
            case EmperorThreeSAbilities.KurohitoFavorDraw:
                Record(new DelayedEffect { Kind = DelayedKinds.Draw, Until = DelayUntil.EndPhaseBegins, PlayerId = player.Id, Reason = "Kurohito's Perfection" });
                Emit("effectDelayed", $"{player.Name}: the Favor action's draw is delayed until {DelayUntil.Describe(DelayUntil.EndPhaseBegins)} (Kurohito's Perfection).", player.Id);
                return false;
            case "kurohito-perfection-cavalry":
            case "kurohito-perfection-duelist":
            case "kurohito-perfection-tactician":
                GiveKeywordPermanently(target!, EmperorThreeSAbilities.KurohitoKeywords.First(k => k.Id == id).Keyword, source!.Id);
                return false;
            // Makito's Maho Battle: the acting player chooses the card without attachments in the target's unit to destroy (the Personality if he has none, else one of his attachments) as the action resolves.
            case EmperorThreeSAbilities.MakitoDestroy:
                {
                    var candidates = UnitCardsWithoutAttachments(target!);
                    var options = candidates.Select(c => new EffectOption(c.Id, $"Destroy {Definition(c).Name}", [Batch1DestroyCardEffect + c.Id])).ToArray();
                    return BeginListedEffectChoice(command, options, $"{Definition(source!).Name}: choose the card without attachments in {Definition(target!).Name}'s unit to destroy (it will not be negated or delayed).",
                        consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            default:
                return null;
        }
    }

    // ---- Honor Never Falls: an additional action limited to base abilities ----

    // ConsumeAdditionalActionGrant's condition (with PerformerGrantMatches): the action must be one of the abilities printed on the grant's Personality.
    private bool BaseAbilityGrantMatches(AdditionalActionGrant grant, GameCommand? command) =>
        !grant.BaseAbilityOnly || command?.AbilityId is { } abilityId && grant.PerformerId is { } performerId && _state.Cards.TryGetValue(performerId, out var performer)
            && (Definition(performer).Abilities ?? []).Contains(abilityId);

    // ---- Honor Never Falls: the mandatory duel delay "(from any source)" ----

    // Called by BeginDuelLossDelayDecisions for each of the duel's ordered Focus Effects that is Honor Never Falls: when a Personality of its owner lost, the effects of losing (the outcome's and, as they resolve, the
    // other players' Focus Effects') are delayed until the end of the turn. No choice is asked: the card says "Delay", not "you may". A delay the owner already has (an R2 Strategy's) is widened to any source.
    private void RegisterHonorNeverFallsDelay(PendingDuel duel, CardInstance focus, List<string> loserIds)
    {
        var owner = focus.OwnerId;
        if (!loserIds.Any(id => Card(id).OwnerId == owner))
        {
            Emit("focusEffectNothing", $"{Definition(focus).Name}: none of {Player(owner).Name}'s Personalities lost the duel, so there is nothing to delay.", owner, focus.Id);
            return;
        }
        var existing = duel.DelayedLosses.FirstOrDefault(d => d.PlayerId == owner);
        duel.DelayedLosses.RemoveAll(d => d.PlayerId == owner);
        duel.DelayedLosses.Add(existing is null ? new(owner, DelayUntil.EndOfTurn, focus.Id, true) : existing with { AnySource = true });
        Emit("duelLossDelayed", $"{Player(owner).Name} delays all consequences of losing this duel, from any source, until {DelayUntil.Describe(existing?.Until ?? DelayUntil.EndOfTurn)} ({Definition(focus).Name}).", owner, focus.Id);
    }

    // The scopes of the current "any source" application (so a nested one can suspend them: a Focus Effect's decision resumes the duel's next Focus Effects inside the call that answered it).
    private readonly List<DelayScope> _anySourceScopes = [];

    /// <summary>Runs <paramref name="apply"/> -- what one Focus Effect (of <paramref name="focusOwnerId"/>) does -- with every effect on the units of the losing Personalities of players who delayed "from any source" recorded.
    /// The effects of a player's OWN Focus Effects are not consequences of his losing and happen as usual.</summary>
    private void WithAnySourceLossDelays(PendingDuel duel, int focusOwnerId, Action apply)
    {
        if (!duel.DelayedLosses.Any(d => d.AnySource) || duel.LoserIds.Count == 0) { apply(); return; }
        var suspended = _anySourceScopes.ToList();
        foreach (var scope in suspended) _delayScopes.Remove(scope);
        _anySourceScopes.Clear();
        var added = new List<DelayScope>();
        foreach (var loserId in duel.LoserIds)
        {
            var loser = Card(loserId);
            if (loser.OwnerId == focusOwnerId || duel.DelayedLosses.FirstOrDefault(d => d.AnySource && d.PlayerId == loser.OwnerId) is not { } choice) continue;
            var reason = $"losing the duel, from any source ({Definition(Card(choice.SourceId)).Name})";
            foreach (var member in Attachments(loserId).Prepend(loser)) added.Add(new(member.Id, choice.Until, null, null, focusOwnerId, reason));
        }
        _delayScopes.AddRange(added); _anySourceScopes.AddRange(added);
        try { apply(); }
        finally
        {
            foreach (var scope in added) { _delayScopes.Remove(scope); _anySourceScopes.Remove(scope); }
            _delayScopes.AddRange(suspended); _anySourceScopes.AddRange(suspended);
        }
    }

    // ---- Makito ----

    // "Makito has +1F while a Phoenix Clan player controls him." (PersonalityForce's hook.)
    private int ThreeSForce(CardInstance card) =>
        Definition(card).Effect == EmperorThreeSAbilities.MakitoEffect && Definition(card).Type == CardType.Personality && Player(card.OwnerId).Clan == "Phoenix" ? 1 : 0;

    // "After a card is destroyed: Give Makito +1F." -- a trait, for any card either player's (a Personality, an attachment, a Region), each time one is; the bonus lasts until the turn ends (no "permanently").
    // Called after the destruction has happened, so a Makito destroyed himself is out of play and gains nothing.
    private void AfterCardDestroyed(CardInstance destroyed)
    {
        ControlReturnsOnDestruction(destroyed); // issue #501 (EmperorMechB.cs): Sachina's return, the moment she is destroyed.
        NoteCreatedProxies677Destroyed(destroyed); // Goju Kaxt (issue #677, CreatedProxies677.cs): his destruction is marked on the record of the action in progress.
        foreach (var makito in _state.Cards.Values.Where(c => c.Id != destroyed.Id && InPlay(c) && Definition(c).Effect == EmperorThreeSAbilities.MakitoEffect && Definition(c).Type == CardType.Personality).ToArray())
        {
            Emit("makitoGrows", $"{Definition(makito).Name}: {Definition(destroyed).Name} was destroyed, so he gets +1F.", makito.OwnerId, makito.Id);
            ApplyModifier(makito, makito.Id, EmperorThreeSAbilities.MakitoEffect, "Force", 1);
        }
    }

    // Makito's Maho Battle action (the running command, or the Attack under way): its destruction will not be negated or delayed (HiromiPerforms' hook).
    private static bool MakitoDestruction(GameCommand? command) => command?.AbilityId == EmperorThreeSAbilities.MakitoDestroy;
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorThreeSHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorThreeS", 1700, static abilities => abilities.AddEmperorThreeS());
        EngineHooks.PerformerAllows.Register("ThreeSPerformerAllows", 500, static (engine, context) => engine.ThreeSPerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.TargetAllows.Register("ThreeSTargetAllows", 800, static (engine, context) => engine.ThreeSTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PerformerCandidates.Register("ThreeSPerformers", 500, static (engine, context) => engine.ThreeSPerformers(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.PersonalityForce.Register("ThreeSForce", 300, static (engine, context) => engine.ThreeSForce(context.card));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorThreeS", 700, static (engine, context) => engine.ValidateEmperorThreeS(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorThreeS", 1500, static (engine, context) => engine.RunEmperorThreeS(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
    }
}
