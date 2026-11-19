namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group B (strategy-wave-59). The declarative ability data lives here (added to AbilityCatalog.All by one hook); each card's printed
// text and the reading taken are at its entry. The execution, validation and listing code follows in the same file (a partial MatchEngine), reached from CardAbilities.cs by small
// additive hooks. Cards of the group that cannot be faithfully wired are recorded in tools/content/ee_remB_blockers.json and are not published.
public static class EmperorRemainingBSpecs
{
    /// <summary>Heavily Engaged: "Each player targets an unbowed Personality he controls there, if possible. Bow all targets." -- the acting player first, then the other (DeciderChoice.cs).</summary>
    public const string HeavilyEngagedFirst = "heavily-engaged-first", HeavilyEngagedSecond = "heavily-engaged-second";
    /// <summary>Mysterious Deaths: "Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all targets."</summary>
    public const string MysteriousDeathsFirst = "mysterious-deaths-first", MysteriousDeathsSecond = "mysterious-deaths-second";
    /// <summary>My Enemy's Mercy: "The enemy leader chooses to either bow him or move him home." (EitherOr.cs)</summary>
    public const string MercyBowOrHome = "my-enemys-mercy-bow-or-home";
    /// <summary>Remember Your Ancestors: "His controller may destroy him. If he did not choose this, discard a card at random from his hand, and choose a player, who gains or loses 3 Honor." The
    /// target's controller decides first; a refusal hands the second choice (the player who gains or loses) to the acting player (EitherOr.cs, chained).</summary>
    public const string AncestorsDestroy = "remember-ancestors-destroy", AncestorsHonor = "remember-ancestors-honor";

    public static bool IsDeciderSpec(string spec) => spec is HeavilyEngagedFirst or HeavilyEngagedSecond or MysteriousDeathsFirst or MysteriousDeathsSecond;
    public static bool IsFirstDecider(string spec) => spec is HeavilyEngagedFirst or MysteriousDeathsFirst;
}

public static class EmperorRemainingBAbilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingB(this Dictionary<string, AbilityDefinition> all)
    {
        // Heart of Rokugan (oracle-l5r-3016, a Terrain): "Before this battle resolves: Give each of your Human Personalities and Followers at the current battlefield +1F, and give each
        // Nonhuman Personality and Follower there -1F." -- a mandatory member of the before-resolution set. Human is "without the Nonhuman keyword" (core rules); "there" names no owner,
        // so the -1F is every Nonhuman's at the battlefield, either side's, and the +1F is the Terrain's controller's own Humans. "Battle: If there are no Terrains ... Put this card into
        // play there" is the generic Terrain play rule.
        all["heart-of-rokugan-sweep"] = new("BeforeResolution", "Before this battle resolves: Give each of your Human Personalities and Followers at the current battlefield +1F, and give each Nonhuman Personality and Follower there -1F",
            "none", Trigger: "before-battle-resolves");
        // Hard Pressed (oracle-l5r-2972, a Terrain): "Before this battle's resolution: Move a unit home from the current battlefield. You may bow it." -- the Terrain's controller chooses the
        // unit (either side's, mandatory while any unit stands there); a unit is moved through its Personality (core rules, Units), and "bow it" bows that Personality.
        all["hard-pressed-move"] = new("BeforeResolution", "Before this battle's resolution: Move a unit home from the current battlefield. You may bow it",
            "none", Trigger: "before-battle-resolves");
        // Ogre Savagery (oracle-l5r-5681, Fear): "Battle: Choose your performing unbowed Nonhuman Shadowlands Personality and target an enemy Personality: Bow him."
        all["ogre-savagery-bow"] = new("Battle", "Choose your performing unbowed Nonhuman Shadowlands Personality and target an enemy Personality: Bow him", "enemy-personality",
            Performer: "own-unbowed-nonhuman-shadowlands-at-battle");
        // Relentless Conviction (oracle-l5r-6247, Unique): "Battle: Target a dishonorable Personality: Destroy him." -- either side's.
        all["relentless-conviction-destroy"] = new("Battle", "Target a dishonorable Personality: Destroy him", "dishonorable-personality");
        // Murderous Intent (oracle-l5r-5444): "Battle/Open: Choose your performing Paragon: Give him +1F for each of his keywords except 'Soul of' keywords. Lose 4 Honor." The keywords are
        // counted as the action resolves: each distinct keyword the Paragon has now, printed or granted (Keyword()), a "Soul of ..." keyword excepted.
        all["murderous-intent-boost"] = new("BattleOrOpen", "Choose your performing Paragon: Give him +1F for each of his keywords except \"Soul of\" keywords. Lose 4 Honor", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Paragon"]);
        // In the Heart of Battle (oracle-l5r-3725, Bushido Virtue, Duty): "Battle/Open: Choose your performing Samurai: Straighten him and give him +3F. Destroy him before this turn ends."
        // -- the delayed destruction is the one every "until before this turn ends" card shares (DelayedDestruction.cs): carried out at the top of EndTurn if he is still in play.
        all["heart-of-battle-surge"] = new("BattleOrOpen", "Choose your performing Samurai: Straighten him and give him +3F. Destroy him before this turn ends", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Samurai"]);
        // Guided by Honor (oracle-l5r-2919): "Battle: If any enemy units are at the current battlefield, choose your performing unbowed Paragon: Move him to the current battlefield. If he moved,
        // you may target and bow an enemy Personality with equal or lower Personal Honor." The condition sits before the colon, so it gates taking the action (Moving and Unmoving's own reading);
        // the optional target is a slot of 0 to 1 cards chosen with the action (Running Battle's precedent), used only if he moved. An action that moves a Personality to the battle (MovesToBattle).
        all["guided-by-honor-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing unbowed Paragon: Move him to the current battlefield. If he moved, you may target and bow an enemy Personality with equal or lower Personal Honor",
            "slots", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Paragon"],
            Slots: [new("target", "enemy-personality-honor-lte-performer", "An enemy Personality with equal or lower Personal Honor (optional)", 0, 1)],
            Move: new(MoveSpec.Performer, MoveSpec.CurrentBattlefield), MovesToBattle: true);
        // Nerve Strike (oracle-l5r-5532, Kiho): "Battle: Bow your performing Monk and target an enemy Personality with equal or lower Chi: Reduce his Force and the Force of each of his Followers to
        // 0. Cards in that unit may not perform actions until this battle ends." -- the lock is scoped to the battle (ActionLock.cs, duration "Battle").
        all["nerve-strike-freeze"] = new("Battle", "Bow your performing Monk and target an enemy Personality with equal or lower Chi: Reduce his Force and the Force of each of his Followers to 0. Cards in that unit may not perform actions until this battle ends",
            "enemy-personality-chi-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"], Costs: ActionCost.BowPerformer);
        // Heavily Engaged (oracle-l5r-3037): "Battle: Even if you control no units at the current battlefield: Each player targets an unbowed Personality he controls there, if possible. Bow all
        // targets." (core rules: the card's text wins over the Rule of Presence.) Each player's choice is a decision of his own (DeciderChoice.cs), the acting player's first.
        all["heavily-engaged-bow"] = new("Battle", "Even if you control no units at the current battlefield: Each player targets an unbowed Personality he controls there, if possible. Bow all targets", "none", Absent: true);
        // Mysterious Deaths (oracle-l5r-5468, Unique, Kolat): "Limited: Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all
        // targets. Remove this card from the game. Lose 4 Honor."
        all["mysterious-deaths-destroy"] = new("Limited", "Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all targets. Remove this card from the game. Lose 4 Honor", "none");
        // My Enemy's Mercy (oracle-l5r-5457, Bushido Virtue, Compassion): "Battle: Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: The enemy
        // leader chooses to either bow him or move him home. Gain 2 Honor." "Him" is read as the target (the enemy Personality: the enemy's own leader picks between two lesser evils for him);
        // the enemy leader is the other player of the battle (core rules: Leader).
        all["my-enemys-mercy-choose"] = new("Battle", "Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: The enemy leader chooses to either bow him or move him home. Gain 2 Honor",
            "enemy-personality-honor-lte-performer", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Samurai"]);
        // Opportunistic Advance (oracle-l5r-5766): "Battle: Choose your performing unbowed Commander and target another Personality in his army: Move the target to a battlefield." -- another Personality of
        // the Commander's army (his controller's at the current battlefield); the battlefield is chosen among the other unresolved ones (Paralyzing Touch's "a different battlefield").
        all["opportunistic-advance-move"] = new("Battle", "Choose your performing unbowed Commander and target another Personality in his army: Move the target to a battlefield", "own-personality-in-performer-army",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Commander"], Move: new(MoveSpec.Target, MoveSpec.AnyUnresolved));
        // Introspection (oracle-l5r-3793): "Reaction: After one of your Rings enters play: Search your Fate deck for a card. Put it in your hand." Yung's trigger, played from a hand.
        all["introspection-search"] = new("Reaction", "After one of your Rings enters play: Search your Fate deck for a card. Put it in your hand", "none",
            Trigger: ReactionTriggers.RingEntersPlay, Search: SearchCatalog.Introspection);
        // Martial Instruction (oracle-l5r-4843, Kiho): "Open: Bow your performing Monk and remove this card from the game: Search your Fate deck for a Kiho Battle Strategy, show it, and put it in your
        // hand." A Battle Strategy is a Strategy with a printed Battle action (Battle or Battle/Open).
        all["martial-instruction-search"] = new("Open", "Bow your performing Monk and remove this card from the game: Search your Fate deck for a Kiho Battle Strategy, show it, and put it in your hand", "none",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Monk"], Costs: [new(CostVerb.Bow, CostSubject.Performer), new(CostVerbs.Remove)], Search: SearchCatalog.MartialInstruction);
        // Meeting the Keepers (oracle-l5r-5018, Void): "Reaction: After the resolution of an action from one of your Rings: Search your Dynasty deck for an Event, show it, and put it on top of the deck
        // after shuffling." and "Open: Bow your target Ring: Draw two cards."
        all["keepers-search"] = new("Reaction", "After the resolution of an action from one of your Rings: Search your Dynasty deck for an Event, show it, and put it on top of the deck after shuffling", "none",
            Trigger: AfterActionReactionTriggers.AfterActionResolved, Search: SearchCatalog.MeetingTheKeepers);
        all["keepers-draw"] = new("Open", "Bow your target Ring: Draw two cards", "own-unbowed-ring", Costs: ActionCost.BowTarget);
        // Ninja Tricks (oracle-l5r-5585, Ninja): "Reaction: After an action on a Strategy is announced, bow your performing Ninja Personality at the current battlefield: Negate the action's effects.
        // Lose 2 Honor." A Reaction played from a hand in the before-action window (any player's action whose source is a Strategy); the performing Ninja is bowed as the cost.
        all["ninja-tricks-negate"] = new("Reaction", "After an action on a Strategy is announced, bow your performing Ninja Personality at the current battlefield: Negate the action's effects. Lose 2 Honor", "none",
            Trigger: "before-action-resolves", Performer: "own-unbowed-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Ninja"], Costs: ActionCost.BowPerformer);
        // Oyo's Second Lesson (oracle-l5r-5853, Kiho): "Battle/Open: Choose your performing dead Henshin and target your Personality: Your Henshin is reborn. Overlay him onto your Personality. Draw a card. You may
        // take an additional action." The Henshin is a dead Personality of the acting player's (the performer need not be in play: the one exception to "choose your own card in play to perform"), the overlay is the
        // Experienced overlay's own transfer (TransferOverlayState: his card takes the target's place with his state, attachments and tokens, and the target's card leaves the game without entering play).
        all["oyos-second-lesson-reborn"] = new("BattleOrOpen", "Choose your performing dead Henshin and target your Personality: Your Henshin is reborn. Overlay him onto your Personality. Draw a card. You may take an additional action",
            "own-personality", Performer: "own-dead-personality-with-keyword", RequiredKeywords: ["Henshin"]);
        // Hidden Defenses (oracle-l5r-3254): "Battle: If you control no Personalities (in play), target one or two Personalities at any location: Bow them. Negate their straightening." and "Reaction: After
        // engaging, if any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved." With no Personality of yours in play you control no unit, so
        // the Battle action needs no presence (Absent; the card's text wins over the Rule of Presence, as for Heavily Engaged). "Negate their straightening" states no duration: it lasts until end of turn (core rules,
        // Duration), the straighten lock of Toshimo's Battle action. The Reaction is Moving and Unmoving's shape: the performer moves himself (a Reaction is not a Battle action, so he may stand anywhere).
        all["hidden-defenses-bow"] = new("Battle", "If you control no Personalities (in play), target one or two Personalities at any location: Bow them. Negate their straightening", "slots", Absent: true,
            Slots: [new("targets", "personality-at-any-location", "One or two Personalities at any location", 1, 2)]);
        all["hidden-defenses-move"] = new("Reaction", "After engaging, if any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved", "none",
            Trigger: "after-engaging", Performer: "own-personality");
        // Hundred-Fold Cut (oracle-l5r-3513): "Reaction: After another player's action targets your performing Kensai, bow or destroy one of his Weapons: Negate his destruction from the action's effects." and "Battle:
        // Bow your performing Kensai or one of his Weapons: Draw two cards." The Kensai is the one the action targets (Know No Fear's reading of "performing"); "his Weapons" are the Kensai's own; the two
        // ways to pay are two ids sharing one use, the Weapon being the Reaction's target. The Battle half's performer is the Kensai or an unbowed Weapon of his (Sutigu's cost choice), bowed as the cost.
        all["hundred-fold-cut-bow"] = new("Reaction", "After another player's action targets your performing Kensai, bow one of his Weapons: Negate his destruction from the action's effects", "kensai-unbowed-weapon-of-held-target",
            Trigger: ReactionTriggers.TargetedByAction, UsageKey: "hundred-fold-cut-response", Costs: ActionCost.BowTarget);
        all["hundred-fold-cut-destroy"] = new("Reaction", "After another player's action targets your performing Kensai, destroy one of his Weapons: Negate his destruction from the action's effects", "kensai-weapon-of-held-target",
            Trigger: ReactionTriggers.TargetedByAction, UsageKey: "hundred-fold-cut-response", Costs: [new(CostVerbs.Sacrifice, CostSubject.Target)]);
        all["hundred-fold-cut-draw"] = new("Battle", "Bow your performing Kensai or one of his Weapons: Draw two cards", "none", Performer: "own-kensai-or-unbowed-weapon-at-battle", Costs: ActionCost.BowPerformer);
        // Remember Your Ancestors (oracle-l5r-6253, Political): "Battle: Bow your performing Courtier at any location and target an enemy attacking Personality: His controller may destroy him. If he did not choose
        // this, discard a card at random from his hand, and choose a player, who gains or loses 3 Honor." Kitsuki Methods' shape: the target's controller decides (EitherOr.cs); a refusal discards a random card
        // of his hand (none when it is empty), then the acting player chooses the player (the four answers every "choose a player who gains or loses N" card gives).
        all["remember-ancestors-choose"] = new("Battle", "Bow your performing Courtier at any location and target an enemy attacking Personality: His controller may destroy him. If he did not choose this, discard a card at random from his hand, and choose a player, who gains or loses 3 Honor",
            "enemy-attacking", Performer: "own-unbowed-personality-with-keyword-at-any-location", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer, ActionKeywords: ["Political"]);
        // He's Mine! (oracle-l5r-3008): "Reaction: Before your target Personality enters a duel, bow him and choose your performing unbowed Samurai at the same location: Your Samurai enters the duel
        // instead. Bow him after the duel ends." Issue #492, Mendon's ruling R5: "The substitute may only take the place of a participant in an accepted (or mandatory) duel. He's Mine! cannot be
        // used to move refusal effects to a new target." So it is a "before the duel begins" Reaction (the window a duel opens once it is accepted, or at once when it cannot be refused), its
        // target the controller's own participant (bowed as the cost), its performer the Samurai who takes his place (EmperorRulingR5.cs); he is bowed once the duel has ended (MatchState.HesMineSubstitutes).
        all["hes-mine-substitute"] = new("Reaction", "Before your target Personality enters a duel, bow him and choose your performing unbowed Samurai at the same location: Your Samurai enters the duel instead. Bow him after the duel ends",
            "duel-own-participant", Trigger: "before-duel-begins", Performer: "own-unbowed-samurai-with-duel-target", Costs: ActionCost.BowTarget);
        // Plans Within Plans (oracle-l5r-5984): "Open: At your next opportunity to take an Open action, you may instead take a Limited action." An opportunity is a turn to act in the Action Phase (a command
        // taken with nothing pending); the next one after this card resolves is the one the Limited action may replace, whoever's turn it is (a Limited action is otherwise its active player's). Whatever the
        // player does with that opportunity -- a Limited action, an Open action, a pass -- spends it.
        all["plans-within-plans-arm"] = new("Open", "At your next opportunity to take an Open action, you may instead take a Limited action", "none");
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- Performer categories (both switches: validation in ValidateAbility, listing in AbilityActions) ------------------------------------------------------------------------------
    private bool EmperorRemainingBPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // Ogre Savagery: "your performing unbowed Nonhuman Shadowlands Personality" -- Shadowlands is a Clan alignment, not a printed keyword (HasClanAffiliation).
        "own-unbowed-nonhuman-shadowlands-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && Keyword(performer, "Nonhuman") && HasClanAffiliation(performer, "Shadowlands")
            && (!actingAsBattle || AtBattle(performer)),
        // Oyo's Second Lesson: "your performing dead Henshin" -- a dead Personality of the acting player's (a Henshin has the keyword printed).
        "own-dead-personality-with-keyword" => Definition(performer).Type == CardType.Personality && performer.Zone == Zone.Dead && HasKeywords(performer, ability.RequiredKeywords),
        // Hundred-Fold Cut's Battle action: "your performing Kensai or one of his Weapons" -- an unbowed Kensai at the battlefield, or an unbowed Weapon attached to a Kensai there.
        "own-kensai-or-unbowed-weapon-at-battle" => !performer.Bowed && AtBattle(performer) && (Definition(performer).Type == CardType.Personality && Keyword(performer, "Kensai")
            || Definition(performer).Type == CardType.Item && Keyword(performer, "Weapon") && performer.Zone == Zone.Attached && Card(performer.AttachedTo) is { } kensai && Keyword(kensai, "Kensai")),
        // He's Mine!: "your performing unbowed Samurai at the same location" as the controller's own participant in the accepted (or mandatory) duel (EmperorRulingR5.cs).
        "own-unbowed-samurai-with-duel-target" => HesMineDuelPerformerAllows(performer),
        _ => false
    };

    private string?[]? EmperorRemainingBPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer switch
    {
        "own-unbowed-nonhuman-shadowlands-at-battle" or "own-dead-personality-with-keyword" or "own-kensai-or-unbowed-weapon-at-battle" =>
            _state.Cards.Values.Where(c => c.OwnerId == playerId && (InPlay(c) || c.Zone == Zone.Dead) && EmperorRemainingBPerformerAllows(ability, c, actingAsBattle)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id).ToArray(),
        _ => null
    };

    // ---- Target categories (ShadowsEmbraceBatch2TargetAllows' default) --------------------------------------------------------------------------------------------------------------
    private bool EmperorRemainingBTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        return category switch
        {
            // Opportunistic Advance: "another Personality in his army" -- a Personality of the performing Commander's controller at the current battlefield, not the Commander himself.
            "own-personality-in-performer-army" => personality && target.OwnerId == player.Id && target.Id != command.PerformerId && AtBattle(target),
            // Hundred-Fold Cut: one of the Weapons of the Kensai the held action targets (the bowing way needs it unbowed).
            "kensai-unbowed-weapon-of-held-target" => KensaiWeaponOfHeldTarget(target) && !target.Bowed,
            "kensai-weapon-of-held-target" => KensaiWeaponOfHeldTarget(target),
            // He's Mine!: the reactor's own participant in the accepted (or mandatory) duel, unbowed (he is bowed as the cost).
            "duel-own-participant" => personality && !target.Bowed && OwnDuelParticipant(player.Id)?.Id == target.Id,
            _ => false
        };
    }

    // ---- the per-card conditions of taking the action (ValidateAbility, after the per-id Requires) ---------------------------------------------------------------------------------------
    private void ValidateEmperorRemainingB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Guided by Honor: "If any enemy units are at the current battlefield".
            case "guided-by-honor-move":
                Require(_state.CurrentBattleProvince is int current && Army(1 - player.Id, current).Any(), "Guided by Honor requires enemy units at the current battlefield.");
                break;
            // Hidden Defenses: "If you control no Personalities (in play)"; its Reaction: "if any enemy units are at the current battlefield".
            case "hidden-defenses-bow":
                Require(!_state.Cards.Values.Any(c => c.OwnerId == player.Id && InPlay(c) && Definition(c).Type == CardType.Personality), "Hidden Defenses requires that you control no Personalities.");
                break;
            case "hidden-defenses-move":
                Require(_state.CurrentBattleProvince is int engaged && Army(1 - player.Id, engaged).Any(), "Hidden Defenses requires enemy units at the current battlefield.");
                break;
                // Mysterious Deaths is a Limited action of the Action Phase like any other (ValidateAbility's timing check); nothing more is required to take it.
        }
    }

    // ---- execution (RunAbilityEffects' default): null when the id is not this pass's; false when its effects are done; true when a window or choice opened and the action waits ----------------
    private bool? RunEmperorRemainingB(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case "ogre-savagery-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "relentless-conviction-destroy":
                DestroyTargetCard(target!, player.Id);
                return false;
            // Murderous Intent: +1F per keyword now (the Honor loss is an effect of the action, so a negation stops it with the rest).
            case "murderous-intent-boost":
                {
                    var paragon = Card(command.PerformerId!);
                    var count = KeywordCountExceptSoulOf(paragon);
                    if (count > 0) ApplyModifier(paragon, source!.Id, id, "Force", count);
                    Emit("murderousIntent", $"{Definition(paragon).Name} has {count} keyword{(count == 1 ? "" : "s")} besides \"Soul of\" keywords: +{count}F.", player.Id, paragon.Id);
                    GainHonor(player.Id, -4, Definition(source!).Name, source);
                    return false;
                }
            // In the Heart of Battle.
            case "heart-of-battle-surge":
                {
                    var samurai = Card(command.PerformerId!);
                    Straighten(samurai);
                    ApplyModifier(samurai, source!.Id, id, "Force", 3);
                    ScheduleDelayedEffect(samurai, DelayedKinds.Destroy, DelayUntil.EndOfTurn, null, Definition(source!).Name, source.Id); // the shared delay store (issue #492, EmperorRulingR2.cs)
                    Emit("destructionScheduled", $"{Definition(samurai).Name} will be destroyed before this turn ends.", player.Id, samurai.Id);
                    return false;
                }
            // Guided by Honor: the move (his own; a negated or blocked move leaves him where he was), then the optional bow only if he really moved.
            case "guided-by-honor-move":
                {
                    var paragon = Card(command.PerformerId!);
                    var wasAtBattle = AtBattle(paragon);
                    ExecuteMove(command, ability, source, target);
                    var moved = !wasAtBattle && AtBattle(paragon);
                    if (moved && SlotCards(command, "target").FirstOrDefault() is { } enemy && InPlay(enemy) && !StalwartNegatesBow(enemy, player.Id)) enemy.Bowed = true;
                    return false;
                }
            // Nerve Strike.
            case "nerve-strike-freeze":
                NerveStrike(target!, source!, id);
                return false;
            // Heavily Engaged / Mysterious Deaths: each player's decision (DeciderChoice.cs).
            case "heavily-engaged-bow":
                return BeginDeciderChoice(EmperorRemainingBSpecs.HeavilyEngagedFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            case "mysterious-deaths-destroy":
                return BeginDeciderChoice(EmperorRemainingBSpecs.MysteriousDeathsFirst, command, consumedGrant, playedFromDiscard, opponentHonorBeforeBattleAction, bowedBeforeAction, preExistingGrants);
            // My Enemy's Mercy: the Honor is gained whatever the enemy leader chooses; the choice then belongs to the enemy leader (EitherOr.cs).
            case "my-enemys-mercy-choose":
                {
                    GainHonor(player.Id, 2, Definition(source!).Name, source);
                    if (NegatesEffect(target!, "all"))
                    {
                        Emit("effectsNegated", $"{Definition(target!).Name}: the action's effects were negated before they resolved.", target!.OwnerId, target.Id);
                        return false;
                    }
                    SpendHandPlayedCard(player, source);
                    if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                    return OpenEitherOr(EmperorRemainingBSpecs.MercyBowOrHome, source!, player.Id, 1 - player.Id, target, "action", bowedBeforeAction,
                        stayWithActor: _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g)), performerId: command.PerformerId);
                }
            // Introspection / Meeting the Keepers' Reaction: a Strategy played from a hand is spent first; the window it was played in is set aside until the search is over (ReactionsAreActions.cs).
            case "introspection-search":
            case "keepers-search":
                SpendHandPlayedCard(player, source);
                SuspendOpenWindow();
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            // Martial Instruction: the Monk was bowed and the card removed as the costs.
            case "martial-instruction-search":
                BeginSearch(ability.Search!, source!, player.Id, "ability", bowedBeforeAction);
                return true;
            case "keepers-draw":
                Draw(player); Draw(player);
                return false;
            // Ninja Tricks: the deferred action's effects are negated (a Protected action's are not), and its player loses 2 Honor either way.
            case "ninja-tricks-negate":
                NegateDeferredAction(source!);
                GainHonor(player.Id, -2, Definition(source!).Name, source);
                return false;
            // Oyo's Second Lesson: the dead Henshin takes the target's place (overlay), then the draw and the additional action.
            case "oyos-second-lesson-reborn":
                {
                    var henshin = Card(command.PerformerId!);
                    var host = target!;
                    if (henshin.Zone == Zone.Dead && InPlay(host) && host.OwnerId == player.Id && Definition(host).Type == CardType.Personality)
                    {
                        var hostName = Definition(host).Name;
                        ResetCardState(henshin);
                        TransferOverlayState(host, henshin);
                        RemoveFromGame(host);
                        Emit("personalityOverlayed", $"{Definition(henshin).Name} is reborn: overlaid onto {hostName}.", player.Id, henshin.Id);
                    }
                    Draw(player);
                    GrantAdditionalAction(player.Id, null);
                    return false;
                }
            // Hidden Defenses: bow them and lock their straightening for the turn (each effect independent of the other).
            case "hidden-defenses-bow":
                foreach (var victim in SlotCards(command, "targets").ToArray())
                {
                    if (InPlay(victim) && !StalwartNegatesBow(victim, player.Id)) victim.Bowed = true;
                    if (InPlay(victim)) LockStraightening(victim);
                }
                return false;
            case "hidden-defenses-move":
                {
                    var mover = Card(command.PerformerId!);
                    var battlefield = _state.CurrentBattleProvince!.Value;
                    var wasThere = AtBattle(mover);
                    if (!wasThere && TryMoveToBattlefield(mover, battlefield) && AtBattle(mover)) Straighten(mover);
                    return false;
                }
            // Hundred-Fold Cut: the Weapon was bowed or destroyed as the cost; the held action's destruction of the Kensai is negated. The Battle half draws two.
            case "hundred-fold-cut-bow":
            case "hundred-fold-cut-destroy":
                NegateEffects(Card(HeldAction().TargetId!), "destroy");
                return false;
            case "hundred-fold-cut-draw":
                Draw(player); Draw(player);
                return false;
            // Remember Your Ancestors: the target's controller decides (EitherOr.cs).
            case "remember-ancestors-choose":
                if (NegatesEffect(target!, "all"))
                {
                    Emit("effectsNegated", $"{Definition(target!).Name}: the action's effects were negated before they resolved.", target!.OwnerId, target.Id);
                    return false;
                }
                SpendHandPlayedCard(player, source);
                if (!consumedGrant) _state.AdditionalActions.RemoveAll(preExistingGrants.Contains);
                return OpenEitherOr(EmperorRemainingBSpecs.AncestorsDestroy, source!, player.Id, target!.OwnerId, target, "action", bowedBeforeAction,
                    stayWithActor: _state.AdditionalActions.Any(g => g.PlayerId == player.Id && Forfeitable(g)), performerId: command.PerformerId);
            // He's Mine!: the Personality bowed as the cost is replaced in the duel by the chosen Samurai, who bows when the duel has ended.
            case "hes-mine-substitute":
                {
                    var samurai = Card(command.PerformerId!);
                    SubstituteDuelParticipant(target!, samurai, source!);
                    _state.HesMineSubstitutes.Add(samurai.Id);
                    return false;
                }
            // Plans Within Plans.
            case "plans-within-plans-arm":
                player.PlansWithinPlansArmed = true; player.PlansWithinPlansSets++;
                Emit("plansWithinPlans", $"{player.Name}: at their next opportunity to take an Open action they may instead take a Limited action.", player.Id, source?.Id);
                return false;
            // Opportunistic Advance: the target moves to the chosen battlefield.
            case "opportunistic-advance-move":
                ExecuteMove(command, ability, source, target);
                return false;
            default:
                return null;
        }
    }

    // ---- Plans Within Plans ----
    // A Limited action instead of an Open one: the player holds the armed card's option at an opportunity of the Action Phase (nothing pending; the top-level priority check already limits the commands to his turn to act).
    private bool LimitedInsteadOfOpen(int playerId) => Player(playerId).PlansWithinPlansArmed && _state.Phase == Phase.Action && _state.PendingChoice is null;

    // Before a command: the number of resolutions so far when this command is the armed player's opportunity, else null.
    private int? PlansOpportunitySets(GameCommand command) =>
        command.Type is not ("concede" or "orderSeppuku") && command.PlayerId == _state.PriorityPlayerId && LimitedInsteadOfOpen(command.PlayerId) ? Player(command.PlayerId).PlansWithinPlansSets : null;

    // After it: the opportunity is spent (a copy played at it re-armed the card and keeps it).
    private void SpendPlansWithinPlans(GameCommand command, int? setsBefore)
    {
        if (setsBefore is not int before || _state.WinnerId is not null) return;
        var player = Player(command.PlayerId);
        if (player.PlansWithinPlansArmed && player.PlansWithinPlansSets == before)
        {
            player.PlansWithinPlansArmed = false;
            Emit("plansWithinPlansSpent", $"{player.Name}'s opportunity to take a Limited action instead of an Open one is over.", player.Id);
        }
    }

    // ---- Hundred-Fold Cut ----
    // "One of his Weapons": a Weapon attached to the Kensai the held action targets.
    private bool KensaiWeaponOfHeldTarget(CardInstance target) =>
        target.Zone == Zone.Attached && Definition(target).Type == CardType.Item && Keyword(target, "Weapon")
        && OpenReactionSubject() is { TargetId: { } heldId } && target.AttachedTo == heldId;

    private IEnumerable<CardInstance> KensaiWeapons(CardInstance kensai, bool unbowedOnly) =>
        Attachments(kensai.Id).Where(a => Definition(a).Type == CardType.Item && Keyword(a, "Weapon") && (!unbowedOnly || !a.Bowed)).OrderBy(a => a.Id, StringComparer.Ordinal);

    // "After another player's action targets your performing Kensai": an action that could destroy him (an Attack always could), against a Kensai of the reactor's with a Weapon to pay with.
    private bool HundredFoldCutApplies(CardInstance card, string abilityId, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Kensai")
        && (subject.IsAttack || DestroysTarget(subject.Command.AbilityId!)) && KensaiWeapons(affected, unbowedOnly: abilityId == "hundred-fold-cut-bow").Any();

    // ---- He's Mine! (the substitution itself is in EmperorRulingR5.cs) ----
    // FinishDuelResolution's hook: the substitute of a duel that has ended is bowed.
    private void ApplyHesMineBows(string challengerId, string challengedId)
    {
        foreach (var id in _state.HesMineSubstitutes.Where(i => i == challengedId || i == challengerId).ToArray())
        {
            _state.HesMineSubstitutes.Remove(id);
            var samurai = Card(id);
            if (InPlay(samurai) && !samurai.Bowed) { samurai.Bowed = true; Emit("hesMineBow", $"{Definition(samurai).Name} bows: the duel he entered has ended.", samurai.OwnerId, samurai.Id); }
        }
    }

    // EndTurn's hook: a substitute whose challenge came to no duel is not bowed.
    private void ResolveEmperorRemainingBTurnEnd() => _state.HesMineSubstitutes.Clear();

    // ---- Remember Your Ancestors (EitherOr.cs hooks) ----
    private EitherOrOption[] AncestorsOptions(PendingEitherOr p, CardInstance? affected)
    {
        var name = Definition(affected!).Name;
        if (p.SpecId == EmperorRemainingBSpecs.AncestorsDestroy)
            return [
                new("destroy", $"Destroy {name}", $"{name} is destroyed. Nothing further happens.", EitherOrKind.Avoid, 0, InPlay(affected!), $"{name} is no longer in play."),
                new("refuse", "Do not destroy him", $"A card is discarded at random from your hand, and {Player(p.ActingPlayerId).Name} chooses a player, who gains or loses 3 Honor.", EitherOrKind.Accept, 0, true, null)
            ];
        return [
            new("self-gain", "You gain 3 Honor", "You gain 3 Honor.", EitherOrKind.Choose, 0, true, null),
            new("opponent-gain", "Your opponent gains 3 Honor", "Your opponent gains 3 Honor.", EitherOrKind.Choose, 0, true, null),
            new("self-lose", "You lose 3 Honor", "You lose 3 Honor.", EitherOrKind.Choose, 0, true, null),
            new("opponent-lose", "Your opponent loses 3 Honor", "Your opponent loses 3 Honor.", EitherOrKind.Choose, 0, true, null)
        ];
    }

    private void ApplyAncestors(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? affected)
    {
        var source = Card(p.SourceCardId); var name = Definition(source).Name;
        if (p.SpecId == EmperorRemainingBSpecs.AncestorsDestroy)
        {
            if (option.Key == "destroy")
            {
                Emit("eitherOrChosen", $"{decider.Name} chose to destroy {Definition(affected!).Name} ({name}).", decider.Id, affected!.Id);
                if (InPlay(affected)) DestroyUnits([affected], false, p.ActingPlayerId);
                return;
            }
            Emit("eitherOrChosen", $"{decider.Name} did not destroy {Definition(affected!).Name} ({name}).", decider.Id, source.Id);
            if (decider.Hand.Count > 0 && !P2HandDiscardBlocked(decider.Id, p.ActingPlayerId, true, name)) // issue #503: Pillowbook, The Scorpion Wall is Finished
            {
                var hand = decider.Hand.ToList();
                var random = new StableRandom(_state.RngState);
                random.Shuffle(hand);
                _state.RngState = random.State;
                DiscardFate(decider, Card(hand[0]));
            }
            OpenEitherOr(EmperorRemainingBSpecs.AncestorsHonor, source, p.ActingPlayerId, p.ActingPlayerId, affected, p.Origin, p.BowedBefore, p.StayWithActor, p.PerformerId);
            return;
        }
        var self = option.Key.StartsWith("self", StringComparison.Ordinal);
        var amount = option.Key.EndsWith("gain", StringComparison.Ordinal) ? 3 : -3;
        var chosenPlayer = self ? p.ActingPlayerId : 1 - p.ActingPlayerId;
        Emit("eitherOrChosen", $"{decider.Name} chose: {option.Label} ({name}).", decider.Id, source.Id);
        var courtier = p.PerformerId is not null && _state.Cards.TryGetValue(p.PerformerId, out var named) ? named : source;
        ApplyPlayerHonorChoice(courtier, chosenPlayer, amount);
    }

    // ---- Ninja Tricks / Meeting the Keepers: their triggers -----------------------------------------------------------------------------------------------------------------------------
    // A Strategy in a hand that may answer an action (the before-action window otherwise reads only cards in play).
    private static bool IsHandBeforeActionReactor(CardInstance card) =>
        Definition(card).Type == CardType.Strategy && (Definition(card).Abilities ?? []).Contains("ninja-tricks-negate");

    // "After an action on a Strategy is announced": any player's action whose source is a Strategy (not the very card being played), with a Ninja of the reactor's own, unbowed, at the current battlefield to
    // perform the Reaction. The Reaction itself is an action on a Strategy: another copy may answer it.
    private bool NinjaTricksApplies(CardInstance card, GameCommand command)
    {
        if (card.Zone != Zone.Hand || Definition(card).Type != CardType.Strategy || command.AbilityId is null || command.CardId == card.Id) return false;
        if (!AbilityCatalog.All.TryGetValue(command.AbilityId, out var acting) || !IsAction(acting)) return false;
        if (command.CardId is null || !_state.Cards.TryGetValue(command.CardId, out var actingSource) || Definition(actingSource).Type != CardType.Strategy) return false;
        var owner = card.OwnerId;
        return _state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null && Player(owner).Hand.Contains(card.Id)
            && _state.Cards.Values.Any(c => c.OwnerId == owner && Definition(c).Type == CardType.Personality && !c.Bowed && AtBattle(c) && Keyword(c, "Ninja"));
    }

    // "After the resolution of an action from one of your Rings": the resolved action is one of the printed actions of a Ring of the reactor's. Putting the Ring into play (its "-recruit"
    // entry, issue #1290, Mendon's ruling) is not a use of the Ring's ability, so it gives the Reaction nothing to answer; nor does a fact about the Ring that no action of its caused.
    private bool RingActionResolved(ResolvedActionFacts facts, int playerId) =>
        facts.PlayerId == playerId && facts.SourceId is { } sourceId && _state.Cards.TryGetValue(sourceId, out var ring) && Definition(ring).Type == CardType.Ring && ring.OwnerId == playerId
        && (Definition(ring).Abilities ?? []).Contains(facts.AbilityId) && !facts.AbilityId.EndsWith("-recruit", StringComparison.Ordinal);

    // "+1F for each of his keywords except 'Soul of' keywords": the distinct keywords the card has now, printed or granted, less any it has lost.
    private int KeywordCountExceptSoulOf(CardInstance card) =>
        (Definition(card).Keywords ?? []).Concat(card.GrantedKeywords.Select(g => g.Keyword)).Distinct(StringComparer.Ordinal)
            .Count(k => !k.StartsWith("Soul of", StringComparison.OrdinalIgnoreCase) && Keyword(card, k));

    // ---- Nerve Strike -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string ActionLockUntilBattleEnd = "Battle";

    private void NerveStrike(CardInstance target, CardInstance source, string id)
    {
        var unit = Attachments(target.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray();
        foreach (var follower in unit) if (CardForce(follower) > 0) ApplyModifier(follower, source.Id, id, "Force", -CardForce(follower));
        if (CardForce(target) > 0) ApplyModifier(target, source.Id, id, "Force", -CardForce(target));
        foreach (var card in Attachments(target.Id).Prepend(target).ToArray()) LockActions(card, source.Id, ActionLockUntilBattleEnd);
        Emit("nerveStrike", $"{Definition(target).Name}'s Force and the Force of each of his Followers is 0, and the cards in his unit may not perform actions until this battle ends.", target.OwnerId, target.Id);
    }

    // CompleteBattleTransition's hook: the lock lapses with the battle.
    private void EndBattleActionLocks()
    {
        foreach (var card in _state.Cards.Values) card.ActionLocks.RemoveAll(l => l.Duration == ActionLockUntilBattleEnd);
    }

    // ---- Heart of Rokugan ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private void HeartOfRokuganSweep(CardInstance terrain, PendingBeforeBattleResolve pending)
    {
        foreach (var personality in _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == pending.ProvinceIndex && Definition(c).Type == CardType.Personality)
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
            foreach (var card in Attachments(personality.Id).Where(a => Definition(a).Type == CardType.Follower).Prepend(personality).ToArray())
            {
                if (!InPlay(card)) continue;
                if (Keyword(card, "Nonhuman")) ApplyModifier(card, terrain.Id, "heart-of-rokugan-sweep", "Force", -1);
                else if (card.OwnerId == terrain.OwnerId) ApplyModifier(card, terrain.Id, "heart-of-rokugan-sweep", "Force", 1);
            }
        Emit("beforeBattleEffect", $"{Definition(terrain).Name}: each Human of {Player(terrain.OwnerId).Name}'s at this battlefield has +1F and each Nonhuman there -1F.", terrain.OwnerId, terrain.Id);
    }

    // ---- Hard Pressed: its before-resolution trait, answered through "beforeBattleOptional" ------------------------------------------------------------------------------------------------
    private bool IsHardPressedChoice(PendingBeforeBattleResolve pending) =>
        pending.ActiveSourceId is { } itemId && _state.Cards.TryGetValue(itemId, out var source) && BeforeResolutionAbilityId(source) == "hard-pressed-move";

    // "A unit at the current battlefield": the Personality that leads it, either side's.
    private IEnumerable<CardInstance> HardPressedCandidates(int provinceIndex) =>
        _state.Cards.Values.Where(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == provinceIndex && Definition(c).Type == CardType.Personality).OrderBy(c => c.Id, StringComparer.Ordinal);

    // Opens the choice, or (no unit stands there) does nothing. True when the choice is open.
    private bool BeginHardPressed(CardInstance terrain, PendingBeforeBattleResolve pending)
    {
        if (!HardPressedCandidates(pending.ProvinceIndex).Any())
        {
            Emit("beforeBattleEffect", $"{Definition(terrain).Name}: no unit stands at this battlefield.", terrain.OwnerId, terrain.Id);
            return false;
        }
        _state.PendingChoice = new("beforeBattleOptional", terrain.OwnerId, terrain.Id, $"Before this battle resolves: move a unit home from this battlefield, and you may bow it ({Definition(terrain).Name}).");
        _state.PriorityPlayerId = terrain.OwnerId;
        return true;
    }

    // "use" names the unit (TargetId); CardIds = [that id] adds "you may bow it". There is no declining: the move is mandatory.
    private void ResolveHardPressed(GameCommand command, PendingBeforeBattleResolve pending, int playerId)
    {
        Require(command.Choice == "use", "Hard Pressed must move a unit home.");
        var legal = HardPressedCandidates(pending.ProvinceIndex).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        Require(command.TargetId is not null && legal.Contains(command.TargetId), "Choose a unit at this battlefield.");
        var bow = command.CardIds is { Length: > 0 };
        Require(!bow || command.CardIds is [var same] && same == command.TargetId, "Bow only the unit that moved.");
        var unit = Card(command.TargetId);
        MoveHome(unit, false);
        Emit("beforeBattleEffect", $"Hard Pressed: {Definition(unit).Name}'s unit was moved home.", playerId, unit.Id);
        // "You may bow it": "it" is the unit, so every card of it bows, each unless its own bowing is prevented (issue #492, ruling R5).
        if (bow && InPlay(unit) && BowUnitByEffect(unit, playerId) > 0) Emit("beforeBattleEffect", $"Hard Pressed: {Definition(unit).Name}'s unit was bowed.", playerId, unit.Id);
    }

    private IEnumerable<ActionOption> HardPressedOptions(int playerId, string? sourceId)
    {
        if (_state.PendingBeforeBattleResolve is not { } pending || _state.PendingChoice?.PlayerId != playerId) yield break;
        foreach (var unit in HardPressedCandidates(pending.ProvinceIndex))
        {
            var name = Definition(unit).Name;
            yield return new("beforeBattleOptional", $"Move {name} home", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: unit.Id, Choice: "use"), unit.Id);
            yield return new("beforeBattleOptional", $"Move {name} home and bow him", true, null, new GameCommand("beforeBattleOptional", playerId, _state.Version, TargetId: unit.Id, CardIds: [unit.Id], Choice: "use"), unit.Id);
        }
    }

    // ---- Heavily Engaged / Mysterious Deaths (DeciderChoice.cs hooks) ---------------------------------------------------------------------------------------------------------------
    private string EmperorRemainingBDeciderPrompt(PendingDeciderChoice p) => p.SpecId switch
    {
        EmperorRemainingBSpecs.HeavilyEngagedFirst or EmperorRemainingBSpecs.HeavilyEngagedSecond =>
            $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose an unbowed Personality you control at the current battlefield. It is bowed.",
        _ => $"{Definition(Card(p.SourceCardId)).Name}: {Player(p.DeciderPlayerId).Name}, choose a unit you control with the lowest total Gold Cost of all your units. It is destroyed."
    };

    // Heavily Engaged: the decider's unbowed Personalities at the current battlefield. Mysterious Deaths: the units (Personalities in play, at home or a battlefield) whose total Gold Cost
    // (the Personality and everything attached, UnitTotalGoldCost) is the lowest of all the decider's units.
    private (List<string> Candidates, int Min, int Max) EmperorRemainingBDeciderOptions(string specId, int deciderId)
    {
        if (specId is EmperorRemainingBSpecs.HeavilyEngagedFirst or EmperorRemainingBSpecs.HeavilyEngagedSecond)
            return (_state.CurrentBattleProvince is int battle ? Army(deciderId, battle).Where(c => !c.Bowed).Select(c => c.Id).Order(StringComparer.Ordinal).ToList() : [], 1, 1);
        var units = _state.Cards.Values.Where(c => c.OwnerId == deciderId && InPlay(c) && Definition(c).Type == CardType.Personality).ToArray();
        if (units.Length == 0) return ([], 1, 1);
        var lowest = units.Min(c => UnitTotalGoldCost(c, out _));
        return (units.Where(c => UnitTotalGoldCost(c, out _) == lowest).Select(c => c.Id).Order(StringComparer.Ordinal).ToList(), 1, 1);
    }

    private void ApplyEmperorRemainingBDeciderChoice(PendingDeciderChoice p)
    {
        var source = Card(p.SourceCardId); var name = Definition(source).Name;
        var chosen = p.EarlierChosen.Concat(p.Chosen).Distinct().ToList();
        if (EmperorRemainingBSpecs.IsFirstDecider(p.SpecId))
        {
            // The first player's target is named; the other player's is asked next (with nothing to choose, nothing is asked).
            var second = p.SpecId == EmperorRemainingBSpecs.HeavilyEngagedFirst ? EmperorRemainingBSpecs.HeavilyEngagedSecond : EmperorRemainingBSpecs.MysteriousDeathsSecond;
            var other = 1 - p.ActingPlayerId;
            var (candidates, min, max) = EmperorRemainingBDeciderOptions(second, other);
            var next = new PendingDeciderChoice
            {
                SpecId = second,
                SourceCardId = p.SourceCardId,
                ActingPlayerId = p.ActingPlayerId,
                DeciderPlayerId = other,
                Candidates = candidates,
                Min = min,
                Max = max,
                EarlierChosen = chosen,
                Paused = p.Paused
            };
            // Nothing to decide (no candidate, or only the one a mandatory choice needs): announced and applied, as for the first decider (BeginDeciderChoice).
            if (candidates.Count <= min)
            {
                if (candidates.Count > 0) Emit("deciderAutoResolved", $"{name}: {Player(other).Name} has one choice.", other, source.Id);
                else Emit("deciderAutoResolved", $"{name}: {Player(other).Name} has nothing to choose.", other, source.Id);
                next.Chosen = candidates.Take(max).ToList();
                ApplyEmperorRemainingBDeciderChoice(next);
                return;
            }
            OpenDeciderChoice(next);
            return;
        }
        if (p.SpecId == EmperorRemainingBSpecs.HeavilyEngagedSecond)
        {
            foreach (var id in chosen)
            {
                var card = Card(id);
                if (InPlay(card) && !StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
            }
            Emit("heavilyEngaged", $"{name}: {chosen.Count} Personalit{(chosen.Count == 1 ? "y was" : "ies were")} bowed.", p.ActingPlayerId, source.Id);
            return;
        }
        // Mysterious Deaths: "Destroy all targets. Remove this card from the game. Lose 4 Honor."
        DestroyUnits(chosen.Select(Card).Where(InPlay).ToArray(), false, p.ActingPlayerId);
        if (source.Zone is Zone.FateDiscard) RemoveFromGame(source);
        GainHonor(p.ActingPlayerId, -4, name, source);
    }

    // ---- My Enemy's Mercy (EitherOr.cs hooks) --------------------------------------------------------------------------------------------------------------------------------------
    private EitherOrOption[] MercyOptions(CardInstance? affected)
    {
        var name = Definition(affected!).Name;
        return [
            new("bow", $"Bow {name}", $"{name} bows.", EitherOrKind.Choose, 0, InPlay(affected!) && !affected!.Bowed, $"{name} is already bowed."),
            new("home", $"Move {name} home", $"{name}'s unit moves home.", EitherOrKind.Choose, 0, affected!.Zone == Zone.Battlefield, $"{name} is not at a battlefield.")
        ];
    }

    private void ApplyMercy(PendingEitherOr p, EitherOrOption option, PlayerState decider, CardInstance? affected)
    {
        var source = Card(p.SourceCardId);
        Emit("eitherOrChosen", $"{decider.Name} chose: {option.Label} ({Definition(source).Name}).", decider.Id, affected!.Id);
        if (!InPlay(affected)) return;
        if (option.Key == "bow") { if (!StalwartNegatesBow(affected, p.ActingPlayerId)) affected.Bowed = true; }
        else if (affected.Zone == Zone.Battlefield) MoveHome(affected, false);
    }
}

public sealed partial class MatchEngine
{
    private bool RegisteredApplyEmperorRemainingBDeciderChoice(PendingDeciderChoice p)
    {
        if (!(EmperorRemainingBSpecs.IsDeciderSpec(p.SpecId))) return false;
        ApplyEmperorRemainingBDeciderChoice(p);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingBHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingB", 500, static abilities => abilities.AddEmperorRemainingB());
        EngineHooks.PerformerAllows.Register("EmperorRemainingBPerformerAllows", 200, static (engine, context) => engine.EmperorRemainingBPerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.PerformerCandidates.Register("EmperorRemainingBPerformers", 800, static (engine, context) => engine.EmperorRemainingBPerformers(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.ValidateEarly.Register("ValidateEmperorRemainingB", 800, static (engine, context) => engine.ValidateEmperorRemainingB(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorRemainingB", 1100, static (engine, context) => engine.RunEmperorRemainingB(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.DeciderPrompt.Register("EmperorRemainingBDeciderPrompt", 100, static (engine, p) => EmperorRemainingBSpecs.IsDeciderSpec(p.SpecId) ? engine.EmperorRemainingBDeciderPrompt(p) : null);
        EngineHooks.DeciderOptions.Register("EmperorRemainingBDeciderOptions", 100, static (engine, specId, deciderId, command) => EmperorRemainingBSpecs.IsDeciderSpec(specId) ? engine.EmperorRemainingBDeciderOptions(specId, deciderId) : null);
        EngineHooks.ApplyDecider.Register("ApplyEmperorRemainingBDeciderChoice", 100, static (engine, p) => engine.RegisteredApplyEmperorRemainingBDeciderChoice(p));
        EngineHooks.DeciderIdAfter.Register("IsFirstDecider", 100, static (engine, specId, command) => EmperorRemainingBSpecs.IsFirstDecider(specId) ? command.PlayerId : null);
    }
}
