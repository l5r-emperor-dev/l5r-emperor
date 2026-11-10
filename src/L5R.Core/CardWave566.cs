namespace L5R.Core;

// Card wave (issue #566): a batch of War of Honor / Embers of War / Honor and Treachery / The Shadow's Embrace / Forgotten Legacy cards assessed for wireability and built
// where the printed text (or an explicit ruling from Mendon) resolves cleanly onto the existing engine surface. Declarative AbilityCatalog data lives here; execution,
// validation, decider and performer/attack hooks follow in the same file (a partial MatchEngine), reached from the shared dispatch points by additive hooks -- this file
// never edits CardAbilities.cs's own giant switches. Cards that needed a genuinely new, large primitive (a new phase-boundary reaction window, a new multi-target
// multi-host attach primitive, a new BattleOrLimited Timing combo) were deliberately left out of this wave; see the PR description for the specific reasons.
public static class CardWave566Specs
{
    /// <summary>Desperate Mediation (oracle-l5r-1974): "In turn order, starting with you, each player targets and moves home one of his Personalities at the current
    /// battlefield" -- the acting player first, then the other (DeciderChoice.cs's Blood-of-the-Preserver/Heavily-Engaged two-step shape).</summary>
    public const string DesperateMediationFirst = "desperate-mediation-first", DesperateMediationSecond = "desperate-mediation-second";
    /// <summary>Shameful Rebuke (oracle-l5r-6699): the same two-step "each player moves one of his own Personalities home" shape as Desperate Mediation, but the Honor loss
    /// afterward falls on a player chosen when the card was played (command.TargetPlayerId), not the acting player.</summary>
    public const string ShamefulRebukeFirst = "shameful-rebuke-first", ShamefulRebukeSecond = "shameful-rebuke-second";
    /// <summary>Fury of the Sea (oracle-l5r-2745): "each player who now controls a unit in the current enemy army" -- "enemy" is read the same owner-based way every other
    /// enemy-scoped ability in this catalogue reads it (never a fixed Attacker/Defender side), so under that reading only the Shugenja's controller's opponent can ever
    /// qualify: one step, never a first/second pair.</summary>
    public const string FuryOfTheSeaMove = "fury-of-sea-move";

    public static bool IsDeciderSpec(string spec) => spec is DesperateMediationFirst or DesperateMediationSecond or ShamefulRebukeFirst or ShamefulRebukeSecond or FuryOfTheSeaMove;
    public static bool IsFirstDecider(string spec) => spec is DesperateMediationFirst or ShamefulRebukeFirst;
}

public static class CardWave566Abilities
{
    public static Dictionary<string, AbilityDefinition> AddCardWave566(this Dictionary<string, AbilityDefinition> all)
    {
        // Fury of the Sea (oracle-l5r-2745, a Water Spell): "Battle: If this Shugenja is unbowed, bow him unless he is Water: In turn order, each player who now controls
        // a unit in the current enemy army targets and moves home one of his units there." The bow is a conditional cost (waived for a Water wielder), handled inline in
        // RunCardWave566 rather than declared (the same non-declarative shape Step Through the Void's own "unless this Shugenja is Void" uses).
        all["fury-of-sea-move"] = new("Battle", "If this Shugenja is unbowed, bow him unless he is Water: In turn order, each player who now controls a unit in the current enemy army targets and moves home one of his units there",
            "none", Performer: "own-unbowed-wielder-at-battle");

        // Tempest of Flame (oracle-l5r-7866, a Fire Void Spell): "Attaches to a Fire or Void Shugenja paying 1 less Gold. This Personality has +2F. Battle: Bow this
        // Shugenja: Ranged 5 Attack." The attach restriction/discount are Effect-keyed edits to Equip/EquipGoldCost (MatchEngine.cs/GoldQuotes.cs, mirroring the existing
        // Purify the Soul/Katagi precedents); the static +2F is a registered PersonalityForce hook (mirroring Strength of the Tsunami's own tsunami-force term) below.
        all["tempest-of-flame-attack"] = new("Battle", "Bow this Shugenja: Ranged 5 Attack", "attack", Performer: "own-wielder", Costs: [new(CostVerb.Bow, CostSubject.Wielder)]);

        // A Warrior's Wisdom (oracle-l5r-98): "Battle: Give your target performing Monk a Force bonus equal to his Chi (until the turn ends). You may take an additional
        // Battle action." -- the target doubles as the performer (Bayushi Suwabe's own shape), so Target is "none".
        all["warriors-wisdom-boost"] = new("Battle", "Target your performing Monk: Give him a Force bonus equal to his Chi (until the turn ends). You may take an additional Battle action",
            "none", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Monk"]);

        // Desperate Mediation (oracle-l5r-1974, Political): "Battle: In turn order, starting with you, each player targets and moves home one of his Personalities at
        // the current battlefield. Then, gain Honor equal to the number of Personalities this moved."
        all["desperate-mediation-move"] = new("Battle", "In turn order, starting with you, each player targets and moves home one of his Personalities at the current battlefield. Then, gain Honor equal to the number of Personalities this moved",
            "none", ActionKeywords: ["Political"]);

        // Game of Sincerity (oracle-l5r-2769, Political): "Battle: Dishonor your performing unbowed Personality and target a Personality: Dishonor him. His controller
        // loses 2 Honor. Neither Personality contributes Force in this battle's resolution."
        all["game-of-sincerity-dishonor"] = new("Battle", "Dishonor your performing unbowed Personality and target a Personality: Dishonor him. His controller loses 2 Honor. Neither Personality contributes Force in this battle's resolution",
            "personality", Performer: "own-unbowed-personality-at-battle", ActionKeywords: ["Political"]);

        // Inexorable (oracle-l5r-3739): printed "Reaction: After another player announces a Reaction during the Combat Segment: Its effects will not negate other
        // effects." Mendon's ruling (this wave): it negates the WHOLE announced Reaction, unconditionally, not only a Reaction that would itself have negated
        // something -- the same whole-action negation Yoritomo Sakuma's card uses (ActionNegation.cs's NegateDeferredAction), applied to any Reaction of an opponent's
        // during a Combat Segment (Phase.Battle, the established "Combat Segment" reading -- temple-of-death-straighten-bow's own precedent).
        all["inexorable-negate"] = new("Reaction", "After another player announces a Reaction during the Combat Segment: Negate that Reaction", "none", Trigger: "before-action-resolves");

        // Inspirational Address (oracle-l5r-3767): "Battle: Choose your performing Personality and target a Follower in your hand or discard pile: Attach it to him,
        // paying 1 less Gold." AttachAction.cs's own file header names this card as one AttachSpec away from buildable; Harsh Taskmaster's own attach-from-hand-or-discard
        // shape is the exact template.
        all["inspirational-address-attach"] = new("Battle", "Choose your performing Personality and target a Follower in your hand or discard pile: Attach it to him, paying 1 less Gold",
            "attachment-in-discard", Performer: "own-personality-at-battle", Attach: new(AttachSource.Target, AttachPile.OwnHandOrDiscard, AttachWielder.Performer, [CardType.Follower]),
            Cost: GoldCostRule.AttachCost(-1));

        // Shameful Death (oracle-l5r-6697): "Battle: Choose your performing unbowed Magistrate Personality and target an enemy dishonorable Personality: Reduce his
        // Force to 0 (until the turn ends). Give your Magistrate a Force bonus equal to the amount of Force the Personality lost (until the turn ends)." The Force-to-0
        // pattern is kaido-force-zero/weak-poison-force-zero's own shape.
        all["shameful-death-reduce"] = new("Battle", "Choose your performing unbowed Magistrate Personality and target an enemy dishonorable Personality: Reduce his Force to 0 (until the turn ends). Give your Magistrate a Force bonus equal to the amount of Force the Personality lost (until the turn ends)",
            "enemy-personality", TargetMustBe: TargetStatus.IsDishonorable, Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Magistrate"]);

        // Shameful Rebuke (oracle-l5r-6699, Political): "Battle: In turn order, starting with you, each player targets and moves one of his Personalities at the current
        // battlefield home. Then, choose a player who loses Honor equal to the number of Personalities this moved." The chosen player is an ordinary up-front target
        // (Target "player", the same immediate-choice shape Rhetoric's own redirect uses) -- the amount is only known once both movers have resolved, but who is chosen
        // does not depend on it, so no third decider step is needed.
        all["shameful-rebuke-move"] = new("Battle", "In turn order, starting with you, each player targets and moves one of his Personalities at the current battlefield home. Then, choose a player who loses Honor equal to the number of Personalities this moved",
            "player", ActionKeywords: ["Political"]);

        // Striking Through the Void (oracle-l5r-7587, Kiho Void): "Battle/Open: Choose your performing unbowed Monk and target one to three Personalities controlled by
        // other players: Give one of them -1F (until the turn ends). Give a second different Personality you targeted -2F (until the turn ends). Give a third different
        // Personality you targeted -3F (until the turn ends)." The escalating penalty follows the order the targets were picked in (RunCardWave566).
        all["striking-void-reduce"] = new("BattleOrOpen", "Choose your performing unbowed Monk and target one to three Personalities controlled by other players: Give one of them -1F (until the turn ends). Give a second different Personality you targeted -2F (until the turn ends). Give a third different Personality you targeted -3F (until the turn ends)",
            "slots", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"],
            Slots: [new("targets", "enemy-personality", "One to three Personalities controlled by other players", 1, 3)]);

        // The Lion's Charge (oracle-l5r-8200): "Battle: Bow your performing Personality or Follower: Ranged 2 Attack, with +2 strength if your Personality or your
        // Follower's Personality is a Paragon. Gain 1 Honor." (Akio's "Bow your performing Personality or Follower" shape.)
        all["lions-charge-attack"] = new("Battle", "Bow your performing Personality or Follower: Ranged 2 Attack, with +2 strength if your Personality or your Follower's Personality is a Paragon. Gain 1 Honor",
            "attack", Performer: "own-unbowed-unit-at-battle", Costs: ActionCost.BowPerformer);

        // Diamond Mine (oracle-l5r-1997, a plain Holding): "Bow this card: Produce 5 Gold." -- pure GoldProduction, no Abilities entry needed (Archery Range/Slanderer's
        // own precedent: the printed Bow-for-Gold line is text only, mechanically carried by CardDefinition.GoldProduction alone).

        // The Seekers' Temple (oracle-l5r-8308, Unique Holding): "Open: Destroy this Holding to search your Fate deck for a Ring, show it, and put it in your hand.
        // Shuffle your Fate deck." -- Yung's own YungRing search spec, one line (versatile-farm-search's own shape).
        all["seekers-temple-search"] = new("Open", "Destroy this Holding to search your Fate deck for a Ring, show it, and put it in your hand", "none",
            Search: SearchCatalog.YungRing, Costs: [new(CostVerbs.Sacrifice)]);

        // Wanted for Questioning (oracle-l5r-9208, added mid-wave per Mendon's ruling that "takes him into custody" is flavor text, not a mechanic): "Battle: Choose your
        // performing unbowed Magistrate and target an enemy Personality: Your Magistrate takes him into custody. If he is dishonorable, destroy his attachments. Move him
        // home."
        all["wanted-questioning-move"] = new("Battle", "Choose your performing unbowed Magistrate and target an enemy Personality: If he is dishonorable, destroy his attachments. Move him home",
            "enemy-personality", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Magistrate"], Move: new(MoveSpec.Target, MoveSpec.Home));

        // Humble Farm (oracle-l5r-9797, added mid-wave once Mendon confirmed the "Humble Farmers" user-supplied scan is a genuine 2F Ashigaru match): "Bow this card:
        // Produce 2 Gold. Battle/Open: Destroy this Holding to create a 2F Ashigaru Follower and attach it to your target Personality." Needs no RunEffects case of its own
        // (CardAbilities.cs's own generic "any ability with a Creates spec" fallback instantiates the scan), the same shape Kakita Saki's/Natural Aviary's own creators use.
        all["humble-farm-create"] = new("BattleOrOpen", "Destroy this Holding to create Humble Farmers and attach it to your target Personality", "own-personality",
            Costs: [new(CostVerbs.Sacrifice)], Creates: new(CardType.Follower, Force: 2, Keywords: ["Ashigaru"], ProxyId: "user-scan-humble-farmers"));

        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- execution -------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunCardWave566(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target,
        bool consumedGrant, bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case "fury-of-sea-move":
                {
                    var wielder = Card(command.PerformerId!);
                    if (!Keyword(wielder, "Water") && !StalwartNegatesBow(wielder, player.Id)) wielder.Bowed = true;
                    return BeginDeciderChoice(CardWave566Specs.FuryOfTheSeaMove, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
                }
            case "tempest-of-flame-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return true;
                return false;
            case "warriors-wisdom-boost":
                {
                    var performer = ActionPerformer(ability, command, source);
                    ApplyModifier(performer, source!.Id, id, "Force", PersonalityChi(performer));
                    GrantAdditionalAction(player.Id, null);
                    return false;
                }
            case "desperate-mediation-move":
                return BeginDeciderChoice(CardWave566Specs.DesperateMediationFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            case "game-of-sincerity-dishonor":
                {
                    var performer = ActionPerformer(ability, command, source);
                    Dishonor(performer);
                    Dishonor(target!);
                    GainHonor(target!.OwnerId, -2, Definition(source!).Name, source);
                    NegateForceContribution(performer, Definition(source!).Name);
                    NegateForceContribution(target, Definition(source!).Name);
                    return false;
                }
            case "inexorable-negate":
                NegateDeferredAction(source!);
                return false;
            case "inspirational-address-attach":
                // The played Strategy is discarded as the attach's own entry-reaction window opens (Stockpiled Vengeance's own precedent: a deferred attach skips
                // RunAbilityEffects' generic "spend the hand-played card" tail, so it is spent here instead).
                if (ExecuteAttach(command, ability.Attach!, player, source, target)) { SpendHandPlayedCard(player, source); return true; }
                return false;
            case "shameful-death-reduce":
                {
                    var performer = ActionPerformer(ability, command, source);
                    var lost = CardForce(target!);
                    ApplyModifier(target!, source!.Id, id, "Force", -lost);
                    ApplyModifier(performer, source.Id, id, "Force", lost);
                    return false;
                }
            case "shameful-rebuke-move":
                return BeginDeciderChoice(CardWave566Specs.ShamefulRebukeFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            case "striking-void-reduce":
                {
                    var targets = SlotCards(command, "targets").ToArray();
                    for (var i = 0; i < targets.Length; i++) ApplyModifier(targets[i], source!.Id, id, "Force", -(i + 1));
                    return false;
                }
            case "lions-charge-attack":
                if (ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant)) return true;
                return false;
            case "seekers-temple-search":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case "wanted-questioning-move":
                if (target!.Dishonorable) foreach (var attachment in Attachments(target.Id).ToArray()) DestroyAttachment(attachment);
                ExecuteMove(command, ability, source, target);
                return false;
            default:
                return null;
        }
    }

    // ---- The Lion's Charge: Ranged Attack strength/kind (Registered AttackStrength/AttackKind hooks) ---------------------------------------------------------------

    private int? CardWave566AttackStrengthBefore(string abilityId, PlayerState player, CardInstance? target, string? sourceId)
    {
        if (abilityId == "tempest-of-flame-attack") return 5;
        if (abilityId != "lions-charge-attack") return null;
        // "+2 strength if your Personality or your Follower's Personality is a Paragon": UnitHostId reads a lone Personality performer as its own host.
        return 2 + (sourceId is not null && Keyword(Card(UnitHostId(Card(sourceId))), "Paragon") ? 2 : 0);
    }

    private static string? CardWave566AttackKind(string abilityId) => abilityId is "tempest-of-flame-attack" or "lions-charge-attack" ? "Ranged" : null;

    // The Lion's Charge: "Gain 1 Honor" is an effect of the Attack's TARGETING (Carpenter Castle's own shape), whether it hits, is negated, or is dodged.
    private void CardWave566AttackTargeted(string abilityId, int playerId, string? targetId)
    {
        if (targetId is not null && abilityId == "lions-charge-attack") GainHonor(playerId, 1, "The Lion's Charge");
    }

    // ---- Performer: Fury of the Sea's own wielder, unbowed, at the current battlefield (Registered PerformerAllows/PerformerCandidates hooks) --------------------------

    private bool CardWave566PerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) =>
        ability.Performer == "own-unbowed-wielder-at-battle" && CardWave566WielderEligible(performer, actingAsBattle);

    private bool CardWave566WielderEligible(CardInstance performer, bool actingAsBattle) =>
        _state.Cards.Values.Any(spell => spell.Zone == Zone.Attached && spell.AttachedTo == performer.Id && Definition(spell).Type == CardType.Spell)
        && !performer.Bowed && (!actingAsBattle || AtBattle(performer));

    private string?[]? CardWave566PerformerCandidates(AbilityDefinition ability, int playerId, bool actingAsBattle)
    {
        if (ability.Performer != "own-unbowed-wielder-at-battle") return null;
        return _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && CardWave566WielderEligible(c, actingAsBattle))
            .Select(c => (string?)c.Id).Order(StringComparer.Ordinal).ToArray();
    }

    // ---- Inexorable: whole-Reaction negation, before-action-reaction eligibility (a direct hook in CardAbilities.cs's own BeforeActionReactionApplies, mirroring the
    // established P2BeforeActionApplies/P3BeforeActionApplies precedent) -------------------------------------------------------------------------------------------

    private bool? CardWave566BeforeActionApplies(CardInstance card, string abilityId, GameCommand command)
    {
        if (abilityId != "inexorable-negate") return null;
        if (card.Zone != Zone.Hand || Definition(card).Type != CardType.Strategy || command.AbilityId is null) return false;
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        return acting.Timing == "Reaction" && command.PlayerId != card.OwnerId && _state.Phase == Phase.Battle;
    }

    // ---- Tempest of Flame: the static +2F to its wielder (Registered PersonalityForce hook, mirroring Strength of the Tsunami's own tsunami-force term) -----------------

    private int CardWave566PersonalityForce(CardInstance card) =>
        Attachments(card.Id).Where(c => Definition(c).Type == CardType.Spell && Definition(c).Effect == "tempest-of-flame").Sum(_ => 2); // "This Personality has +2F" (R-085): the subject is any host, a Shugenja or not.

    // ---- Desperate Mediation / Shameful Rebuke / Fury of the Sea: decider hooks (DeciderChoice.cs's extension points) -------------------------------------------------

    private string CardWave566DeciderPrompt(PendingDeciderChoice p) => p.SpecId switch
    {
        CardWave566Specs.DesperateMediationFirst or CardWave566Specs.DesperateMediationSecond or CardWave566Specs.ShamefulRebukeFirst or CardWave566Specs.ShamefulRebukeSecond =>
            $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose one of your Personalities at the current battlefield. It moves home.",
        _ => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose one of your units in the enemy army. It moves home.",
    };

    // Desperate Mediation / Shameful Rebuke: the decider's own Personalities at the current battlefield (any bow state -- the card does not print "unbowed"), mandatory
    // if any exists (Heavily Engaged's own "if possible" shape: 0 candidates auto-skips since Min<=Candidates.Count trivially, but a real candidate is not optional).
    // Fury of the Sea: the SAME battlefield's units, but only the performer's own opponent (see CardWave566Specs.FuryOfTheSeaMove's own doc comment on "enemy army").
    private (List<string> Candidates, int Min, int Max) CardWave566DeciderOptions(string specId, int deciderId)
    {
        if (specId == CardWave566Specs.FuryOfTheSeaMove)
        {
            var opponentUnits = _state.CurrentBattleProvince is int furyBattle ? Army(deciderId, furyBattle).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [];
            return (opponentUnits, 1, 1);
        }
        var here = _state.CurrentBattleProvince is int province ? Army(deciderId, province).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [];
        return (here, 1, 1);
    }

    // Desperate Mediation / Shameful Rebuke: the first decider is the acting player (the ability's own performer), not necessarily _state.ActivePlayerId (Blood of the
    // Preserver's own precedent for why this override is needed). Fury of the Sea: only the Shugenja's controller's OPPONENT can ever qualify under the owner-based
    // "enemy army" reading (CardWave566Specs.FuryOfTheSeaMove's own doc comment), so its one decider is always the other player.
    private int? CardWave566DeciderIdBefore(string specId, GameCommand command) => specId switch
    {
        CardWave566Specs.DesperateMediationFirst or CardWave566Specs.ShamefulRebukeFirst => command.PlayerId,
        CardWave566Specs.FuryOfTheSeaMove => 1 - command.PlayerId,
        _ => null,
    };

    private void CardWave566ApplyDeciderChoice(PendingDeciderChoice p)
    {
        var source = Card(p.SourceCardId); var name = Definition(source).Name;
        if (p.SpecId == CardWave566Specs.FuryOfTheSeaMove)
        {
            foreach (var id in p.Chosen) if (InPlay(Card(id))) MoveHome(Card(id), false);
            return;
        }
        var chosen = p.EarlierChosen.Concat(p.Chosen).Distinct().ToList();
        if (CardWave566Specs.IsFirstDecider(p.SpecId))
        {
            var second = p.SpecId == CardWave566Specs.DesperateMediationFirst ? CardWave566Specs.DesperateMediationSecond : CardWave566Specs.ShamefulRebukeSecond;
            var other = 1 - p.ActingPlayerId;
            var (candidates, min, max) = CardWave566DeciderOptions(second, other);
            var next = new PendingDeciderChoice { SpecId = second, SourceCardId = p.SourceCardId, ActingPlayerId = p.ActingPlayerId, DeciderPlayerId = other, Candidates = candidates, Min = min, Max = max, EarlierChosen = chosen, Paused = p.Paused };
            if (candidates.Count <= min)
            {
                Emit("deciderAutoResolved", candidates.Count > 0 ? $"{name}: {Player(other).Name} has one choice." : $"{name}: {Player(other).Name} has nothing to choose.", other, source.Id);
                next.Chosen = candidates.Take(max).ToList();
                CardWave566ApplyDeciderChoice(next);
                return;
            }
            OpenDeciderChoice(next);
            return;
        }
        foreach (var id in chosen) if (InPlay(Card(id))) MoveHome(Card(id), false);
        if (p.SpecId == CardWave566Specs.DesperateMediationSecond) GainHonor(p.ActingPlayerId, chosen.Count, name, source);
        else if (p.Paused.Command.TargetPlayerId is int rebukedPlayer) GainHonor(rebukedPlayer, -chosen.Count, name, source);
    }
}

public sealed partial class MatchEngine
{
    private bool RegisteredApplyCardWave566DeciderChoice(PendingDeciderChoice p)
    {
        if (!CardWave566Specs.IsDeciderSpec(p.SpecId)) return false;
        CardWave566ApplyDeciderChoice(p);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterCardWave566Hooks()
    {
        EngineHooks.Abilities.Register("AddCardWave566", 5660, static abilities => abilities.AddCardWave566());
        EngineHooks.RunEffects.Register("RunCardWave566", 5660, static (engine, context) => engine.RunCardWave566(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.AttackStrengthBefore.Register("CardWave566AttackStrength", 5660, static (engine, context) => engine.CardWave566AttackStrengthBefore(context.abilityId, context.player, context.target, context.sourceId));
        EngineHooks.AttackKind.Register("CardWave566AttackKind", 5660, static context => CardWave566AttackKind(context.abilityId));
        EngineHooks.PerformerAllows.Register("CardWave566PerformerAllows", 5660, static (engine, context) => engine.CardWave566PerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.PerformerCandidates.Register("CardWave566PerformerCandidates", 5660, static (engine, context) => engine.CardWave566PerformerCandidates(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.PersonalityForce.Register("CardWave566PersonalityForce", 5660, static (engine, context) => engine.CardWave566PersonalityForce(context.card));
        EngineHooks.DeciderPrompt.Register("CardWave566DeciderPrompt", 5660, static (engine, p) => CardWave566Specs.IsDeciderSpec(p.SpecId) ? engine.CardWave566DeciderPrompt(p) : null);
        EngineHooks.DeciderOptions.Register("CardWave566DeciderOptions", 5660, static (engine, specId, deciderId, command) => CardWave566Specs.IsDeciderSpec(specId) ? engine.CardWave566DeciderOptions(specId, deciderId) : null);
        EngineHooks.DeciderVerb.Register("CardWave566DeciderVerb", 5660, static (engine, specId) => CardWave566Specs.IsDeciderSpec(specId) ? "Move home" : null);
        EngineHooks.DeciderIdBefore.Register("CardWave566DeciderIdBefore", 5660, static (engine, specId, command) => engine.CardWave566DeciderIdBefore(specId, command));
        EngineHooks.ApplyDecider.Register("ApplyCardWave566DeciderChoice", 5660, static (engine, p) => engine.RegisteredApplyCardWave566DeciderChoice(p));
    }
}
