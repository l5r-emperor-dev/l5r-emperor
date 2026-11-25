namespace L5R.Core;

// Emperor Edition remaining-cards pass (issue #492), group D (strategy-wave-61): the Strategies The Law's Strength, The Light of Justice, The Power of One, The Power of a Word,
// The Serpent's Deception, The Slow Death, The Trap is Sprung!, Undetectable Enemy, Unseen Assailant, Useful Connections, Veiled Menace, Wall of Honor, Well Prepared, Words Have
// Strength and (the ones with a longer story, each documented at its own use below) Torch's Flame Flickers, White Shore Plain, The Killing Grounds, The Shadow Court and The
// Sincerity of the Dragon. The declarative data is here (added to AbilityCatalog.All by one hook); the shared engine files carry short, commented hooks into this file.
//
// New primitives:
//   * A "MOVE HOME" COST (CostVerbs.MoveHome): "Move your performing opposed Personality home:" (Wall of Honor) -- a cost, paid whatever happens to the effects.
//   * A WATCH ON EITHER SIDE'S ASSIGNMENT (WatchVeiledMenace): "After each time he is assigned (this turn), his controller loses 1 Honor" -- Social Grace's standing watch, but for
//     an assignment to either side (Midday Shadow Court's), and paying the Honor loss to the Personality's controller.
//   * A ONE-SHOT NEGATION OF THE NEXT BOWING OF A PLAYER'S PERSONALITIES AT A BATTLEFIELD (MatchState.TorchWatches, Torch's Flame Flickers).
public sealed record TorchWatch(int PlayerId, int ProvinceIndex, int Battle, string Source)
{
    /// <summary>The command whose bowing the watch negated: every other bowing of the same effect (a unit bowed as a whole) is negated with it. Reference identity; never serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public object? FiredFor { get; set; }
    public bool Fired { get; set; }
}

/// <summary>The Shadow Court (oracle-l5r-8313): "After the next time this turn another player's action or a battle resolution destroys your Courtier, draw a card, and choose a player,
/// who gains or loses 2 Honor." <paramref name="CardId"/> is the performing Courtier, <paramref name="PlayerId"/> his controller, <paramref name="SourceId"/> the Strategy.</summary>
public sealed record ShadowCourtWatch(string CardId, int PlayerId, int Turn, string SourceId);

/// <summary>The Sincerity of the Dragon (oracle-l5r-8338): "After the next resolution this turn of your action from another Strategy or Ring with a base Focus Value equal to the number,
/// draw a card, and choose a player, who discards a card." <paramref name="Number"/> is the named number.</summary>
public sealed record SincerityWatch(int PlayerId, int Number, int Turn, string SourceId, string? Title = null);

public static class EmperorRemainingDAbilities
{
    public static Dictionary<string, AbilityDefinition> AddEmperorRemainingD(this Dictionary<string, AbilityDefinition> all)
    {
        // The Slow Death (oracle-l5r-8340): "As a Focus Effect: After this duel ends, give its winner +3F." / "Battle: Target an opposed unit: Straighten it." -- Sudden Movement's
        // Focus Effect; the straighten is the whole unit's (the reading of every "it" that names a unit, Moto Naleesh's bow).
        all["slow-death-focus"] = new("FocusEffect", "As a Focus Effect: After this duel ends, give its winner +3F", "none");
        all["slow-death-straighten"] = new("Battle", "Target an opposed unit: Straighten it", "rd-opposed-unit");
        // Well Prepared (oracle-l5r-9287): "Battle: If you control a Terrain, target a card in a unit: Bow or straighten the target." -- Yoritomo Hama's pair sharing one use.
        all["well-prepared-bow"] = new("Battle", "If you control a Terrain, target a card in a unit: Bow the target", "any-unit-card", UsageKey: "well-prepared");
        all["well-prepared-straighten"] = new("Battle", "If you control a Terrain, target a card in a unit: Straighten the target", "any-unit-card", UsageKey: "well-prepared");
        // Undetectable Enemy (oracle-l5r-8961): "Reaction: After another player's action targets your Oni: Negate its effects." -- Taishuu's whole-action negation, an Oni for a Political action.
        all["undetectable-enemy-negate"] = new("Reaction", "After another player's action targets your Oni: Negate its effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        // Unseen Assailant (oracle-l5r-9011, a Ninja card): "Battle: Bow your performing Ninja: Ranged 3 Attack. If the Ranged Attack targeted a Personality, you may compare it against his Chi."
        all["unseen-assailant-attack"] = new("Battle", "Bow your performing Ninja: Ranged 3 Attack. If the Ranged Attack targeted a Personality, you may compare it against his Chi", "attack",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Ninja"], Costs: ActionCost.BowPerformer);
        // The Trap is Sprung! (oracle-l5r-8394): "Battle: If you have Reconnaissance, choose your performing unbowed Scout Personality, and target an enemy card: Bow it. Bow its unit if
        // you are a Crane Clan player."
        all["trap-sprung-bow"] = new("Battle", "If you have Reconnaissance, choose your performing unbowed Scout Personality, and target an enemy card: Bow it. Bow its unit if you are a Crane Clan player", "enemy-card",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Scout"]);
        // Veiled Menace (oracle-l5r-9130): "Open: Choose your performing unbowed Courtier and target a dishonorable Personality: His controller loses 2 Honor. After each time he is
        // assigned (this turn), his controller loses 1 Honor."
        all["veiled-menace-loss"] = new("Open", "Choose your performing unbowed Courtier and target a dishonorable Personality: His controller loses 2 Honor. After each time he is assigned (this turn), his controller loses 1 Honor",
            "dishonorable-personality", Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"]);
        // Useful Connections (oracle-l5r-9046): "Open: Bow your performing Courtier and target one or more Personalities, up to an amount equal to your Courtier's Chi: Give Cavalry to them
        // and to each of their Followers." -- one or more (the Chi limit is a per-id Require: a slot's count is fixed data).
        all["useful-connections-cavalry"] = new("Open", "Bow your performing Courtier and target one or more Personalities, up to an amount equal to your Courtier's Chi: Give Cavalry to them and to each of their Followers", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Courtier"], Costs: ActionCost.BowPerformer,
            Slots: [new("targets", "personality", "One or more Personalities, up to your Courtier's Chi", 1, 12)]);
        // Wall of Honor (oracle-l5r-9198): "Battle: Move your performing opposed Personality home: Increase or decrease the strength of the current battlefield's province by an amount
        // equal to the number of units on the enemy side there. Gain 2 Honor." -- the move home is a cost; "increase or decrease" is two entries sharing one use (Kusugi's shape).
        all["wall-of-honor-raise"] = new("Battle", "Move your performing opposed Personality home: Increase the strength of the current battlefield's province by an amount equal to the number of units on the enemy side there. Gain 2 Honor", "none",
            Performer: "own-personality-opposed-at-battle", UsageKey: "wall-of-honor", Costs: [new(CostVerbs.MoveHome, CostSubject.Performer)]);
        all["wall-of-honor-lower"] = new("Battle", "Move your performing opposed Personality home: Decrease the strength of the current battlefield's province by an amount equal to the number of units on the enemy side there. Gain 2 Honor", "none",
            Performer: "own-personality-opposed-at-battle", UsageKey: "wall-of-honor", Costs: [new(CostVerbs.MoveHome, CostSubject.Performer)]);
        // The Law's Strength (oracle-l5r-8194, Political): "Battle: Choose your performing Courtier or Magistrate and target a Personality: Move him home. If this did not move him home,
        // destroy him."
        all["laws-strength-move"] = new("Battle", "Choose your performing Courtier or Magistrate and target a Personality: Move him home. If this did not move him home, destroy him", "personality",
            Performer: "rd-own-personality-with-any-keyword-at-battle", RequiredKeywords: ["Courtier", "Magistrate"]);
        // The Light of Justice (oracle-l5r-8199, Imperial Political): "Battle: If any enemy units are at the current battlefield, choose your performing Magistrate Personality at home: Move him to
        // the current battlefield. If he moved, straighten his unit. If any dishonorable Personalities are now opposing him, you may take an additional Battle action."
        all["light-of-justice-move"] = new("Battle", "If any enemy units are at the current battlefield, choose your performing Magistrate Personality at home: Move him to the current battlefield. If he moved, straighten his unit. If any dishonorable Personalities are now opposing him, you may take an additional Battle action", "none",
            Performer: "own-personality-with-keyword", RequiredKeywords: ["Magistrate"], MovesToBattle: true);
        // The Power of One (oracle-l5r-8265): "Battle/Open: Choose your performing Commander: Straighten his unit." / "Battle: Choose your performing unbowed Commander and target one or two
        // enemy units with a combined total Gold Cost equal to or lower than your Commander's unit's total Gold Cost: Bow the targets."
        all["power-one-straighten"] = new("BattleOrOpen", "Choose your performing Commander: Straighten his unit", "none",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Commander"]);
        all["power-one-bow"] = new("Battle", "Choose your performing unbowed Commander and target one or two enemy units with a combined total Gold Cost equal to or lower than your Commander's unit's total Gold Cost: Bow the targets", "slots",
            Performer: "own-unbowed-personality-at-battle-with-keyword", RequiredKeywords: ["Commander"],
            Slots: [new("targets", "enemy-unit", "One or two enemy units", 1, 2)]);
        // The Power of a Word (oracle-l5r-8262, Political): "Reaction: After another player's Battle action targets your Courtier: The player may choose to lose 3 Honor. If he did not choose
        // this, negate the action's effects." / "Battle: Target one to three enemy dishonorable Personalities: The shadow of their shame shall fall upon their shoulders. Move them home."
        all["power-word-reaction"] = new("Reaction", "After another player's Battle action targets your Courtier: The player may choose to lose 3 Honor. If he did not choose this, negate the action's effects", "none", Trigger: ReactionTriggers.TargetedByAction);
        all["power-word-move"] = new("Battle", "Target one to three enemy dishonorable Personalities: Move them home", "slots",
            Slots: [new("targets", "enemy-personality", "One to three enemy dishonorable Personalities", 1, 3, MustBe: TargetStatus.IsDishonorable)]);
        // The Serpent's Deception (oracle-l5r-8311, Fear Kiho): "Battle: Choose your performing Monk and target an enemy Personality whose unit has lower total Force than your Monk's unit:
        // Move the target home. Your Monk feigns his injuries. He contributes Force to his army even if bowed. Lose 3 Honor."
        all["serpents-deception-move"] = new("Battle", "Choose your performing Monk and target an enemy Personality whose unit has lower total Force than your Monk's unit: Move the target home. Your Monk feigns his injuries. He contributes Force to his army even if bowed. Lose 3 Honor", "rd-enemy-personality-unit-force-lt-performer",
            Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Monk"]);
        // Words Have Strength (oracle-l5r-9377, Political): "Reaction: After a battle's resolution, or the resolution of another player's Battle action, if the resolution or action destroyed any
        // of your Courtiers or Magistrates: Choose a player, who loses 3 Honor." Two Reactions (the two triggers); the once-per-resolution limit is a Require.
        all["words-strength-resolution"] = new("Reaction", "After a battle's resolution, if the resolution destroyed any of your Courtiers or Magistrates: Choose a player, who loses 3 Honor", "player", Trigger: MatchEngine.AfterResolutionTriggerId, UsageKey: "words-strength");
        all["words-strength-action"] = new("Reaction", "After the resolution of another player's Battle action, if the action destroyed any of your Courtiers or Magistrates: Choose a player, who loses 3 Honor", "player", Trigger: AfterActionReactionTriggers.AfterActionResolved, UsageKey: "words-strength");
        // Torch's Flame Flickers (oracle-l5r-8639, a Recon card): "Reaction: After engaging, choose your performing Scout at the current battlefield: You have Reconnaissance there. Negate the next card
        // effect that bows one or more of your Personalities who is there (at the time)." -- Watchers in the Dark's Engage-window Reaction (any bow state), and a one-shot watch on the bowing of the
        // player's Personalities at the battlefield (MatchState.TorchWatches, asked where a card effect bows: StalwartNegatesBow).
        all["torch-flame-recon"] = new("Reaction", "After engaging, choose your performing Scout at the current battlefield: You have Reconnaissance there. Negate the next card effect that bows one or more of your Personalities who is there (at the time)", "none",
            Trigger: "after-engaging", Performer: "own-personality-with-keyword-explicitly-at-battle", RequiredKeywords: ["Scout"]);
        // White Shore Plain (oracle-l5r-9323), a Terrain: "While at the current battlefield, each of your Tacticians has the ability, 'Tactical Reaction: Before this battle's resolution: Give this Tactician
        // a Force bonus equal to his Chi.'" -- a controller-wide static ability grant (StaticGrantedAbilityIds), like The Ki-Rin's Path's; the Terrain itself is the generic play-as-Terrain card (its Effect
        // is the grant's marker).
        all["white-shore-plain-boost"] = new("Reaction", "Tactical Reaction: Before this battle's resolution: Give this Tactician a Force bonus equal to his Chi", "none", Trigger: "before-battle-resolves", ActionKeywords: ["Tactical"]);
        // The Shadow Court (oracle-l5r-8313): "Battle: Choose your performing defending Courtier and target an enemy Personality: Bow him. After the next time this turn another player's action or a
        // battle resolution destroys your Courtier, draw a card, and choose a player, who gains or loses 2 Honor." -- "your Courtier" is the performing one (ShadowCourtWatches).
        all["shadow-court-bow"] = new("Battle", "Choose your performing defending Courtier and target an enemy Personality: Bow him. After the next time this turn another player's action or a battle resolution destroys your Courtier, draw a card, and choose a player, who gains or loses 2 Honor", "enemy-personality",
            Performer: "rd-own-defending-personality-with-keyword", RequiredKeywords: ["Courtier"]);
        // The Sincerity of the Dragon (oracle-l5r-8338): "As a Focus Effect: After this duel ends, if you won it, give all your provinces +2 strength." / "Battle/Open: If you are a Dragon Clan player,
        // choose your performing unbowed Dragon Clan Personality: Name a number. After the next resolution this turn of your action from another Strategy or Ring with a base Focus Value equal to the
        // number, draw a card, and choose a player, who discards a card. Remove this card from the game." -- the number is a choice among the Focus Values a card may have (0 to 6), an effect choice;
        // the watch is SincerityWatches. The corrected scan (Mendon, issue #492) adds the Dragon Clan player condition (ValidateEmperorRemainingD) and the removal (ApplyEmperorRemainingDBranchEffect).
        all["sincerity-dragon-focus"] = new("FocusEffect", "As a Focus Effect: After this duel ends, if you won it, give all your provinces +2 strength", "none");
        all["sincerity-dragon-name"] = new("BattleOrOpen", "If you are a Dragon Clan player, choose your performing unbowed Dragon Clan Personality: Name a number. After the next resolution this turn of your action from another Strategy or Ring with a base Focus Value equal to the number, draw a card, and choose a player, who discards a card. Remove this card from the game", "none",
            Performer: "rd-own-unbowed-clan-personality-at-battle", RequiredKeywords: ["Dragon"],
            Branch: new(BranchCondition.Always, Enumerable.Range(0, 7).Select(n => new EffectOption($"name-{n}", $"Name {n}", [$"{MatchEngine.SincerityNameEffect}{n}"])).ToArray(), HonorChoice.None));
        // Twist of Fate (oracle-l5r-8917): "Reaction: Before your Dynasty Phase ends: Take an additional Limited or Open action." -- a window as its player ends the Dynasty Phase (TwistOfFateWindow); the
        // action is taken with the match sitting in the Action Phase for that one action (TwistOfFateActive), so every Limited and Open action there is takes its ordinary shape.
        all["twist-of-fate-grant"] = new("Reaction", "Before your Dynasty Phase ends: Take an additional Limited or Open action", "none", Trigger: MatchEngine.BeforeDynastyEndsTrigger);
        return all;
    }
}

public sealed partial class MatchEngine
{
    internal static string AfterResolutionTriggerId => AfterResolutionTrigger;

    // The Well Prepared / Power of One style "unit" helpers -------------------------------------------------------------------------------------------------------------------

    // "An opposed unit": a Personality (and so his unit) at a battlefield whose other side has at least one unit.
    private bool OpposedUnit(CardInstance card) =>
        Definition(card).Type == CardType.Personality && card.Zone == Zone.Battlefield && card.ProvinceIndex is int province && Army(1 - card.OwnerId, province).Any();

    // ---- validation (ValidateAbility, after the per-id Requires; also run for each candidate of a slot, with ability.Slots null) ----

    private void ValidateEmperorRemainingD(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            // Well Prepared: "If you control a Terrain" -- a condition on taking the action.
            case "well-prepared-bow":
            case "well-prepared-straighten":
                Require(ControlsKeyword(player.Id, "Terrain"), "You do not control a Terrain.");
                break;
            // The Sincerity of the Dragon: "If you are a Dragon Clan player" -- a condition on taking the action (the corrected scan, issue #492).
            case "sincerity-dragon-name":
                Require(player.Clan == "Dragon", "You must be a Dragon Clan player.");
                break;
            // The Trap is Sprung!: "If you have Reconnaissance".
            case "trap-sprung-bow":
                Require(HasReconnaissanceAtCurrentBattlefield(player.Id), "You do not have Reconnaissance at the current battlefield.");
                break;
            // Useful Connections: "up to an amount equal to your Courtier's Chi" (the whole selection; a slot's own candidate is judged alone).
            case "useful-connections-cavalry" when ability.Slots is not null && command.PerformerId is not null:
                Require(SlotCards(command, "targets").Count() <= PersonalityChi(Card(command.PerformerId)), "You may target at most as many Personalities as your Courtier's Chi.");
                break;
            // Wall of Honor: the move home is paid as a cost, so the performer must be able to move (no lock on his movement home).
            case "wall-of-honor-raise":
            case "wall-of-honor-lower":
                if (command.PerformerId is not null) Require(!MovementLocked(Card(command.PerformerId), MovementKinds.Home), "The performing Personality may not move this turn.");
                break;
            // The Light of Justice: "If any enemy units are at the current battlefield" and "your performing Magistrate Personality at home".
            case "light-of-justice-move":
                Require(_state.CurrentBattleProvince is int justiceBattle && Army(1 - player.Id, justiceBattle).Any(), "There are no enemy units at the current battlefield.");
                if (command.PerformerId is not null) Require(Card(command.PerformerId).Zone == Zone.Home, "The performing Magistrate must be at home.");
                break;
            // The Power of One: the combined total Gold Cost of the targeted units is at most the Commander's unit's total Gold Cost. A slot's candidate is judged alone (it must fit by
            // itself), the whole selection together.
            case "power-one-bow" when command.PerformerId is not null:
                {
                    var reach = UnitTotalGoldCost(Card(command.PerformerId), out _);
                    if (ability.Slots is null)
                    {
                        if (command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var candidate) && Definition(candidate).Type == CardType.Personality)
                            Require(UnitTotalGoldCost(candidate, out _) <= reach, "That unit's total Gold Cost is above your Commander's unit's.");
                    }
                    else Require(SlotCards(command, "targets").Sum(c => UnitTotalGoldCost(c, out _)) <= reach, "The targets' combined total Gold Cost is above your Commander's unit's.");
                    break;
                }
            // Words Have Strength: "You may only play one copy of Words Have Strength in reaction to any one resolution."
            case "words-strength-resolution":
                Require(_state.PendingAfterResolution is { WordsHaveStrengthPlayed: false }, "Words Have Strength was already played in reaction to this resolution.");
                break;
            case "words-strength-action":
                Require(_state.LastResolvedAction is { WordsHaveStrengthPlayed: false }, "Words Have Strength was already played in reaction to this resolution.");
                break;
        }
    }

    // ---- Performer categories (the validation switch's fallback and its listing twin) ----

    private bool RemainingDPerformerAllows(AbilityDefinition ability, CardInstance performer, bool actingAsBattle) => ability.Performer switch
    {
        // The Law's Strength: "your performing Courtier or Magistrate" -- any one of the RequiredKeywords, at the battle while acting as a Battle action; bowed or not.
        "rd-own-personality-with-any-keyword-at-battle" => Definition(performer).Type == CardType.Personality && (ability.RequiredKeywords ?? []).Any(k => Keyword(performer, k)) && (!actingAsBattle || AtBattle(performer)),
        // The Shadow Court: "your performing defending Courtier" -- a defending unit (at the battlefield, on the Defender's side) whose Personality has the required keywords; bowed or not.
        "rd-own-defending-personality-with-keyword" => Definition(performer).Type == CardType.Personality && IsDefendingUnit(performer) && HasKeywords(performer, ability.RequiredKeywords),
        // The Sincerity of the Dragon: "your performing unbowed Dragon Clan Personality" (the Clan alignment named by RequiredKeywords), at the battle while acting as a Battle action.
        "rd-own-unbowed-clan-personality-at-battle" => Definition(performer).Type == CardType.Personality && !performer.Bowed && HasClanAffiliation(performer, ability.RequiredKeywords![0]) && (!actingAsBattle || AtBattle(performer)),
        _ => false
    };

    private string?[]? RemainingDPerformers(AbilityDefinition ability, int playerId, bool actingAsBattle) => ability.Performer switch
    {
        "rd-own-personality-with-any-keyword-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality
            && (ability.RequiredKeywords ?? []).Any(k => Keyword(c, k)) && (!actingAsBattle || AtBattle(c))).Select(c => (string?)c.Id).ToArray(),
        "rd-own-defending-personality-with-keyword" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && InPlay(c) && IsDefendingUnit(c) && HasKeywords(c, ability.RequiredKeywords)).Select(c => (string?)c.Id).ToArray(),
        "rd-own-unbowed-clan-personality-at-battle" => _state.Cards.Values.Where(c => c.OwnerId == playerId && Definition(c).Type == CardType.Personality && InPlay(c) && !c.Bowed
            && HasClanAffiliation(c, ability.RequiredKeywords![0]) && (!actingAsBattle || AtBattle(c))).Select(c => (string?)c.Id).ToArray(),
        _ => null
    };

    // ---- Target categories (the validation switch's fallback) ----

    private bool RemainingDTargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target)
    {
        var personality = Definition(target).Type == CardType.Personality;
        return category switch
        {
            "rd-opposed-unit" => OpposedUnit(target),
            // The Serpent's Deception: "an enemy Personality whose unit has lower total Force than your Monk's unit".
            "rd-enemy-personality-unit-force-lt-performer" => personality && target.OwnerId != player.Id && command.PerformerId is not null && UnitTotalForce(target) < UnitTotalForce(Card(command.PerformerId)),
            _ => false
        };
    }

    // ---- execution (RunAbilityEffects' default): null when the id is not this pass's; false when its effects are done; true when a window opened ----

    private bool? RunEmperorRemainingD(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant,
        bool playedFromDiscard, int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        var name = source is null ? "an action" : Definition(source).Name;
        switch (id)
        {
            case "slow-death-straighten":
                StraightenUnit(target!);
                return false;
            case "well-prepared-bow":
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case "well-prepared-straighten":
                Straighten(target!);
                return false;
            case "undetectable-enemy-negate":
                NegateHeldAction(source!);
                return false;
            case "unseen-assailant-attack":
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            // "Bow it. Bow its unit if you are a Crane Clan player." Each bowing is an effect a bow negation may stop card by card.
            case "trap-sprung-bow":
                BowByEffect(target!, player.Id);
                if (player.Clan == "Crane") BowUnitByEffect(target!, player.Id, except: target!.Id); // the rest of its unit bows too (the target itself already did or was prevented)
                return false;
            case "veiled-menace-loss":
                GainHonor(target!.OwnerId, -2, name, source);
                _state.AssignmentWatches.Add(new(target.Id, WatchVeiledMenace, player.Id, _state.TurnNumber, name));
                Emit("watchSet", $"{name}: after each time this turn {Definition(target).Name} is assigned, {Player(target.OwnerId).Name} loses 1 Honor.", player.Id, target.Id);
                return false;
            case "useful-connections-cavalry":
                foreach (var chosen in SlotCards(command, "targets").ToArray())
                {
                    GrantKeyword(chosen, "Cavalry", name, GrantDuration.Turn);
                    foreach (var follower in Attachments(chosen.Id).Where(a => Definition(a).Type == CardType.Follower).ToArray()) GrantKeyword(follower, "Cavalry", name, GrantDuration.Turn);
                }
                return false;
            case "wall-of-honor-raise":
            case "wall-of-honor-lower":
                {
                    var battle = _state.CurrentBattleProvince!.Value;
                    var units = Army(1 - player.Id, battle).Count();
                    if (CurrentProvinceIndex() is int wallProvince) GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, wallProvince, id == "wall-of-honor-raise" ? units : -units, name); // no province, no strength (issue #492, rulings pass R1)
                    GainHonor(player.Id, 2, name, source);
                    return false;
                }
            // "Move him home. If this did not move him home, destroy him": a movement that was negated or locked leaves him at the battlefield, and then he is destroyed.
            case "laws-strength-move":
                MoveHome(target!, false);
                if (InPlay(target!) && target!.Zone == Zone.Battlefield) DestroyTargetCard(target, player.Id);
                return false;
            case "power-one-straighten":
                StraightenUnit(Card(command.PerformerId!));
                return false;
            case "power-one-bow":
                foreach (var unitTarget in SlotCards(command, "targets").ToArray()) BowUnitByEffect(unitTarget, player.Id);
                return false;
            case "power-word-reaction":
                return BeginPowerOfWordChoice(source!);
            case "power-word-move":
                foreach (var shamed in SlotCards(command, "targets").ToArray()) MoveHome(shamed, false);
                return false;
            case "serpents-deception-move":
                {
                    var monk = Card(command.PerformerId!);
                    MoveHome(target!, false);
                    monk.DeepRootsTurn = _state.TurnNumber;
                    Emit("contributesWhileBowed", $"{name}: {Definition(monk).Name} contributes Force to his army even if bowed this turn.", player.Id, monk.Id);
                    GainHonor(player.Id, -3, name, source);
                    return false;
                }
            case "words-strength-resolution":
            case "words-strength-action":
                if (id == "words-strength-resolution" && _state.PendingAfterResolution is { } resolution) resolution.WordsHaveStrengthPlayed = true;
                if (id == "words-strength-action" && _state.LastResolvedAction is { } resolved) resolved.WordsHaveStrengthPlayed = true;
                GainHonor(command.TargetPlayerId!.Value, -3, name, source);
                return false;
            case "twist-of-fate-grant":
                BeginTwistOfFateAction(player, source!);
                return true;
            // Torch's Flame Flickers: Reconnaissance now, and the watch on the next card effect that bows one of the player's Personalities at this battlefield.
            case "torch-flame-recon":
                {
                    GrantReconnaissanceAtCurrentBattlefield(player.Id);
                    _state.TorchWatches.RemoveAll(w => w.Battle != _state.BattleNumber);
                    _state.TorchWatches.Add(new(player.Id, _state.CurrentBattleProvince!.Value, _state.BattleNumber, name));
                    Emit("watchSet", $"{name}: the next card effect that bows one or more of {player.Name}'s Personalities at this battlefield will be negated.", player.Id, source!.Id);
                    return false;
                }
            // White Shore Plain's granted Reaction: the Tactician's own Force bonus, equal to his Chi as it resolves.
            case "white-shore-plain-boost":
                ApplyModifier(source!, source!.Id, id, "Force", PersonalityChi(source));
                return false;
            // The Shadow Court: bow him, then the performing Courtier is watched for the turn.
            case "shadow-court-bow":
                {
                    var courtier = Card(command.PerformerId!);
                    if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                    _state.ShadowCourtWatches.Add(new(courtier.Id, player.Id, _state.TurnNumber, source!.Id));
                    Emit("watchSet", $"{name}: after the next time this turn another player's action or a battle resolution destroys {Definition(courtier).Name}, {player.Name} draws a card and chooses a player who gains or loses 2 Honor.", player.Id, courtier.Id);
                    return false;
                }
            default:
                return null;
        }
    }

    // ---- Torch's Flame Flickers ----

    // StalwartNegatesBow's hook: a card effect is bowing `card`. The first effect that bows one of the watching player's Personalities at the watched battlefield is negated -- and, as the effect
    // is one, so is every other bowing of the same effect at that battlefield (a unit bowed as a whole: the Personality first, then his Followers).
    private bool TorchNegatesBow(CardInstance card)
    {
        if (_state.TorchWatches.Count == 0) return false;
        var host = Card(UnitHostId(card));
        if (host.Zone != Zone.Battlefield || host.ProvinceIndex is not int province || Definition(host).Type != CardType.Personality) return false;
        var effect = (object?)_runningCommand ?? _attackCommandInResolution;
        var negated = false;
        foreach (var watch in _state.TorchWatches.Where(w => w.PlayerId == host.OwnerId && w.ProvinceIndex == province && w.Battle == _state.BattleNumber))
        {
            if (!watch.Fired)
            {
                if (card.Id != host.Id) continue; // only a Personality's bowing triggers it
                watch.Fired = true; watch.FiredFor = effect; negated = true;
            }
            else if (effect is not null && ReferenceEquals(watch.FiredFor, effect)) negated = true;
        }
        if (negated) Emit("bowNegated", $"{Definition(card).Name}'s bowing was negated (Torch's Flame Flickers).", card.OwnerId, card.Id);
        return negated;
    }

    // ---- White Shore Plain ----

    private const string WhiteShorePlainEffect = "white-shore-plain";
    internal const string WhiteShorePlainAbilityId = "white-shore-plain-boost";

    // "While at the current battlefield, each of your Tacticians has the ability ...": a Tactician of the Terrain's controller standing at the Terrain's battlefield (StaticGrantedAbilityIds).
    private bool WhiteShorePlainGrants(CardInstance card) =>
        card.Zone == Zone.Battlefield && card.ProvinceIndex is int province && Keyword(card, "Tactician")
        && TerrainsAtBattlefield(province).Any(t => t.OwnerId == card.OwnerId && Definition(t).Effect == WhiteShorePlainEffect);

    // ---- Veiled Menace ----

    /// <summary>Veiled Menace: "After each time he is assigned (this turn), his controller loses 1 Honor" -- an assignment to either side; the watch stays for the turn.</summary>
    public const string WatchVeiledMenace = "veiled-menace";

    private void FireVeiledMenaceAssigned(CardInstance card)
    {
        foreach (var watch in _state.AssignmentWatches.Where(w => w.CardId == card.Id && w.Kind == WatchVeiledMenace && w.Turn == _state.TurnNumber).ToArray())
            ResolveAssignmentWatch(watch, card, "was assigned");
    }

    // ---- The Power of a Word ----

    // "After another player's Battle action targets your Courtier" (a targeted-by-action reaction).
    private bool PowerOfWordApplies(CardInstance card, ReactionSubject subject, CardInstance affected) =>
        subject.Command.PlayerId != card.OwnerId && IsBattleAction(subject.Command) && affected.OwnerId == card.OwnerId && Definition(affected).Type == CardType.Personality && Keyword(affected, "Courtier");

    // "The player may choose to lose 3 Honor. If he did not choose this, negate the action's effects": the acting player decides (EitherOr.cs).
    private bool BeginPowerOfWordChoice(CardInstance source)
    {
        var acting = OpenReactionSubject()?.Command.PlayerId ?? 1 - source.OwnerId;
        return OpenEitherOr(EitherOrSpecs.PowerOfWordLoseOrNegate, source, acting, acting, null, "window");
    }

    // The refusal negates the held action: the window it was played in is what holds it, so it is read from there.
    private void NegateHeldActionFromEitherOr(PendingEitherOr pending, CardInstance source)
    {
        var current = _state.PendingChoice;
        _state.PendingChoice = pending.Suspended ?? current;
        try { NegateHeldAction(source); }
        finally { _state.PendingChoice = current; }
    }

    // ---- The Words Have Strength facts ----

    // "If the resolution destroyed any of your Courtiers or Magistrates": read off the cards the battle's resolution destroyed (now dead).
    private bool ResolutionDestroyedCourtierOrMagistrate(PendingAfterResolution facts, int playerId) =>
        facts.DestroyedCardIds.Any(id => _state.Cards.TryGetValue(id, out var c) && c.OwnerId == playerId && Definition(c).Type == CardType.Personality
            && c.Zone == Zone.Dead && (Keyword(c, "Courtier") || Keyword(c, "Magistrate")));

    private bool ActionDestroyedCourtierOrMagistrate(ResolvedActionFacts facts, int playerId) =>
        facts.Affected.Any(a => a.Kind == DestroyedKind && Card(a.CardId) is { } c && c.OwnerId == playerId && Definition(c).Type == CardType.Personality
            && (Keyword(c, "Courtier") || Keyword(c, "Magistrate")));

    // ---- Twist of Fate ----

    internal const string BeforeDynastyEndsTrigger = "before-dynasty-ends";
    private const string TwistOfFateWindow = "twistOfFateReaction";

    private static bool IsTwistOfFateReaction(AbilityDefinition ability) => ability.Timing == "Reaction" && ability.Trigger == BeforeDynastyEndsTrigger;

    // The one place a Dynasty Phase ends by its player's choice (the endDynasty command, and a pass in the Dynasty Phase): "Before your Dynasty Phase ends" -- a window in which he may use a Reaction
    // that names it. It opens only when he holds one he could use now, and only once a turn unless he uses one (a second copy is a second Reaction). True when the window is open.
    private bool BeginTwistOfFateWindow()
    {
        if (_state.Phase is not (Phase.Dynasty or Phase.DynastyDiscard) || _state.PendingChoice is not null || _state.TwistOfFateActive || _state.TwistOfFateDeclinedTurn == _state.TurnNumber) return false;
        var playerId = _state.ActivePlayerId;
        var window = new PendingChoice(TwistOfFateWindow, playerId, null, "Before your Dynasty Phase ends: you may use a Reaction, or pass.");
        _state.PendingChoice = window;
        bool usable;
        try { usable = AbilityActions(playerId).Any(o => o.Enabled); }
        finally { _state.PendingChoice = null; }
        if (!usable) return false;
        _state.PendingChoice = window;
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
        Emit("reactionWindow", "Before-Dynasty-Phase-ends reaction window opened.", playerId);
        return true;
    }

    // The window's answers: a Reaction (an action like any other), or the pass that lets the Dynasty Phase end.
    private void ResolveTwistOfFateWindow(GameCommand command)
    {
        Require(command.Type is "useAbility" or "declineReaction" or "pass", "Use a Reaction, or pass, before your Dynasty Phase ends.");
        if (command.Type == "useAbility") { UseAbility(command); return; }
        _state.TwistOfFateDeclinedTurn = _state.TurnNumber;
        _state.PendingChoice = null;
        EndDynastyPhase(); // issue #667: the Attack Phase when the order puts it after the Dynasty Phase
    }

    // Twist of Fate's effect: the Strategy is spent, the window closes, and the match steps into the Action Phase for exactly one additional action of its player's (a Limited or an Open one -- and, being
    // in that phase, only those). The Reaction's own tail is not run: it would hand the priority to the other player.
    private void BeginTwistOfFateAction(PlayerState player, CardInstance source)
    {
        DiscardFate(player, source);
        _state.PendingChoice = null;
        _state.TwistOfFateActive = true; _state.TwistOfFateReturnPhase = _state.Phase;
        _state.Phase = Phase.Action;
        _state.PriorityPlayerId = player.Id; _state.ConsecutivePasses = 0;
        GrantAdditionalAction(player.Id, null);
        Emit("twistOfFate", $"{Definition(source).Name}: {player.Name} takes an additional Limited or Open action before his Dynasty Phase ends.", player.Id, source.Id);
    }

    // The additional action is over -- taken, or passed -- and the Dynasty Phase carries on where it was.
    private void EndTwistOfFateAction()
    {
        _state.TwistOfFateActive = false;
        _state.AdditionalActions.RemoveAll(g => g.PlayerId == _state.ActivePlayerId && Forfeitable(g));
        _state.Phase = _state.TwistOfFateReturnPhase;
        _state.PriorityPlayerId = _state.ActivePlayerId; _state.ConsecutivePasses = 0;
        Emit("twistOfFateEnded", "The additional action is over: the Dynasty Phase carries on.", _state.ActivePlayerId);
    }

    // Apply's hook after a command completed: the first command taken during the additional action that leaves nothing open ends it (an action that opens a window or a decision waits for it).
    private void FinishTwistOfFateAction(bool wasActive)
    {
        if (!wasActive || !_state.TwistOfFateActive || _state.WinnerId is not null) return;
        if (HasOpenDecisionState()) return;
        EndTwistOfFateAction();
    }

    // ---- The Killing Grounds ----

    private const string KillingGroundsEffect = "killing-grounds";
    // Mendon's ruling (issue #492, R4): "First time for each player" -- the trait is spent once PER PLAYER, so the key carries the passing player's id.
    private static string KillingGroundsFiredKey(int playerId) => $"killing-grounds:fired:{playerId}";

    // Pass()'s hook, in the Combat Segment: "After the first time, while this card remains in play, that a player passes instead of announcing a Battle action: He chooses and destroys one of his
    // Personalities at the current battlefield." The first pass BY EACH PLAYER while the Terrain stands spends that player's time (Mendon's ruling, R4). One Personality is destroyed without asking; with several the passing player is asked
    // (killingGroundsSelect) and the pass completes once he has chosen. True when a decision is open (the pass waits); false when the pass goes on.
    private bool BeginKillingGroundsOnPass()
    {
        if (_state.PendingChoice is not null || _state.CurrentBattleProvince is not int battle) return false;
        var grounds = TerrainsAtBattlefield(battle).FirstOrDefault(t => Definition(t).Effect == KillingGroundsEffect && !t.AbilityUses.ContainsKey(KillingGroundsFiredKey(_state.PriorityPlayerId)));
        if (grounds is null) return false;
        var passer = _state.PriorityPlayerId;
        grounds.AbilityUses[KillingGroundsFiredKey(passer)] = _state.TurnNumber;
        var own = Army(passer, battle).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Emit("killingGrounds", $"{Definition(grounds).Name}: {Player(passer).Name} passed instead of announcing a Battle action and must destroy one of his Personalities at this battlefield.", passer, grounds.Id);
        if (own.Length == 0) return false;
        if (own.Length == 1) { DestroyKillingGroundsVictim(own[0], grounds); return false; }
        _state.PendingChoice = new("killingGroundsSelect", passer, grounds.Id, $"{Definition(grounds).Name}: choose and destroy one of your Personalities at this battlefield.");
        _state.PriorityPlayerId = passer; _state.ConsecutivePasses = 0;
        return true;
    }

    // The Terrain's trait, not an action: nobody performs it, so there is no acting player (and nothing an action's destruction is negated by applies).
    private void DestroyKillingGroundsVictim(CardInstance victim, CardInstance grounds)
    {
        Emit("killingGroundsDestroyed", $"{Definition(grounds).Name}: {Definition(victim).Name} is destroyed.", victim.OwnerId, victim.Id);
        DestroyUnits([victim]);
    }

    private void ResolveKillingGroundsSelect(GameCommand command)
    {
        var choice = _state.PendingChoice!;
        Require(command.Type == "killingGroundsSelect" && command.PlayerId == choice.PlayerId && command.TargetId is not null && _state.CurrentBattleProvince is not null, "Choose one of your Personalities at this battlefield.");
        var victim = Card(command.TargetId);
        Require(victim.OwnerId == command.PlayerId && Army(command.PlayerId, _state.CurrentBattleProvince!.Value).Any(c => c.Id == victim.Id), "Choose one of your own Personalities at this battlefield.");
        _state.PendingChoice = null;
        DestroyKillingGroundsVictim(victim, Card(choice.CardId!));
        FinishBattlePass();
    }

    private IEnumerable<ActionOption> KillingGroundsActions(int playerId)
    {
        if (_state.PendingChoice is not { Type: "killingGroundsSelect" } || _state.CurrentBattleProvince is not int battle) yield break;
        foreach (var card in Army(playerId, battle).OrderBy(c => c.Id, StringComparer.Ordinal))
            yield return new("killingGroundsSelect", $"Destroy {Definition(card).Name}", true, null, new GameCommand("killingGroundsSelect", playerId, _state.Version, TargetId: card.Id), card.Id);
    }

    // ---- The Shadow Court ----

    // "After the next time this turn another player's action ... destroys your Courtier": the traits are asked where an action is truly done (FinishActionEffectTracking), before any Reaction to
    // the same moment. The watch is spent, its controller draws, and he chooses a player and a direction (EitherOrSpecs.ShadowCourtHonor). True when the decision is open.
    private bool OpenShadowCourtTrait(int closesTo)
    {
        if (_state.ShadowCourtWatches.Count == 0 || _state.LastResolvedAction is not { } facts) return false;
        var watch = _state.ShadowCourtWatches.FirstOrDefault(w => w.Turn == _state.TurnNumber && Card(w.CardId).OwnerId != facts.PlayerId
            && facts.Affected.Any(a => a.Kind == DestroyedKind && a.CardId == w.CardId));
        if (watch is null) return false;
        _state.ShadowCourtWatches.Remove(watch);
        Draw(Player(watch.PlayerId));
        SuspendOpenWindow();
        return OpenEitherOr(EitherOrSpecs.ShadowCourtHonor, Card(watch.SourceId), facts.PlayerId, watch.PlayerId, null, ShadowsEmbraceAfterActionOrigin, resumePlayerId: closesTo);
    }

    private const string ShadowCourtResolutionOrigin = "shadow-court-resolution";

    // "...or a battle resolution destroys your Courtier": asked as the resolution is done, before its Reactions (a trait). The resolution carries on from FinishEmperorRemainingDEitherOr.
    private bool OpenShadowCourtResolutionTrait(PendingAfterResolution facts)
    {
        if (_state.ShadowCourtWatches.Count == 0) return false;
        var watch = _state.ShadowCourtWatches.FirstOrDefault(w => w.Turn == _state.TurnNumber && facts.DestroyedCardIds.Contains(w.CardId) && Card(w.CardId).Zone == Zone.Dead);
        if (watch is null) return false;
        _state.ShadowCourtWatches.Remove(watch);
        Draw(Player(watch.PlayerId));
        _state.PendingShadowCourtResolution = facts;
        return OpenEitherOr(EitherOrSpecs.ShadowCourtHonor, Card(watch.SourceId), facts.Attacker, watch.PlayerId, null, ShadowCourtResolutionOrigin, resumePlayerId: facts.Attacker);
    }

    // ---- The Sincerity of the Dragon ----

    internal const string SincerityNameEffect = "sincerity-name:";

    // ApplyBranchEffect's arm: naming the number sets the watch for the turn.
    private bool ApplyEmperorRemainingDBranchEffect(string effect, GameCommand command)
    {
        if (!effect.StartsWith(SincerityNameEffect, StringComparison.Ordinal)) return false;
        var number = int.Parse(effect[SincerityNameEffect.Length..]);
        var source = Card(command.CardId);
        _state.SincerityWatches.RemoveAll(w => w.Turn != _state.TurnNumber);
        _state.SincerityWatches.Add(new(command.PlayerId, number, _state.TurnNumber, source.Id));
        Player(source.OwnerId).Hand.Remove(source.Id); RemoveFromGame(source); // "Remove this card from the game" (the corrected scan, issue #492): part of the action's resolution, so it never reaches the discard pile.
        Emit("watchSet", $"{Definition(source).Name}: {Player(command.PlayerId).Name} named {number}. After the next resolution this turn of his action from another Strategy or Ring with a base Focus Value of {number}, he draws a card and chooses a player who discards a card.", command.PlayerId, source.Id);
        return true;
    }

    // "After the next resolution this turn of your action from another Strategy or Ring with a base Focus Value equal to the number": asked where an action is truly done, as a trait.
    private bool OpenSincerityTrait(int closesTo)
    {
        if (_state.SincerityWatches.Count == 0 || _state.LastResolvedAction is not { } facts || facts.SourceId is not { } sourceId || !_state.Cards.TryGetValue(sourceId, out var acted)) return false;
        if (Definition(acted).Type is not (CardType.Strategy or CardType.Ring)) return false;
        var watch = _state.SincerityWatches.FirstOrDefault(w => w.Turn == _state.TurnNumber && w.PlayerId == facts.PlayerId && w.SourceId != acted.Id && (w.Title is null ? w.Number == Definition(acted).Focus : w.Title == Definition(acted).Name)); // The Honesty of the Phoenix (ruling pass R2) names a title instead of a number
        if (watch is null) return false;
        _state.SincerityWatches.Remove(watch);
        Draw(Player(watch.PlayerId));
        SuspendOpenWindow();
        return OpenEitherOr(EitherOrSpecs.SincerityDiscard, Card(watch.SourceId), facts.PlayerId, watch.PlayerId, null, ShadowsEmbraceAfterActionOrigin, resumePlayerId: closesTo);
    }

    // ---- the two decisions (EitherOr.cs) ----

    private EitherOrOption[] EmperorRemainingDEitherOrOptions(PendingEitherOr p) => p.SpecId switch
    {
        EitherOrSpecs.ShadowCourtHonor =>
        [
            new("self-gain", "You gain 2 Honor", "You gain 2 Honor.", EitherOrKind.Choose, 0, true, null),
            new("opponent-lose", "Your opponent loses 2 Honor", "Your opponent loses 2 Honor.", EitherOrKind.Choose, 0, true, null),
            new("opponent-gain", "Your opponent gains 2 Honor", "Your opponent gains 2 Honor.", EitherOrKind.Choose, 0, true, null),
            new("self-lose", "You lose 2 Honor", "You lose 2 Honor.", EitherOrKind.Choose, 0, true, null),
        ],
        EitherOrSpecs.SincerityDiscard =>
        [
            new("self", "You discard a card", "You discard a card of your choice.", EitherOrKind.Choose, 0, true, null),
            new("opponent", "Your opponent discards a card", "Your opponent discards a card of his choice.", EitherOrKind.Choose, 0, true, null),
        ],
        _ => throw new RuleViolation("Unknown choice.")
    };

    private void ApplyEmperorRemainingDEitherOr(PendingEitherOr p, EitherOrOption option, PlayerState decider)
    {
        var source = Card(p.SourceCardId);
        var name = Definition(source).Name;
        var chosen = option.Key.StartsWith("self", StringComparison.Ordinal) ? decider.Id : 1 - decider.Id;
        Emit("eitherOrChosen", $"{decider.Name} chose: {option.Label} ({name}).", decider.Id, source.Id);
        if (p.SpecId == EitherOrSpecs.ShadowCourtHonor) GainHonor(chosen, option.Key.EndsWith("gain", StringComparison.Ordinal) ? 2 : -2, name, source);
        else if (!P2HandDiscardBlocked(chosen, source.OwnerId, false, name)) _state.PendingForcedDiscards.Add(new(chosen, name)); // issue #503: Pillowbook
    }

    // FinishEitherOr's hook: after a decision of this group is answered the resolved action's own tail carries on -- more watches due at the same moment first, then a discard the answer owed.
    private bool FinishEmperorRemainingDEitherOr(PendingEitherOr p)
    {
        if (p.SpecId is not (EitherOrSpecs.ShadowCourtHonor or EitherOrSpecs.SincerityDiscard)) return false;
        _state.PendingChoice = null;
        if (p.Origin == ShadowCourtResolutionOrigin)
        {
            var facts = _state.PendingShadowCourtResolution!;
            if (OpenShadowCourtResolutionTrait(facts)) return true;
            _state.PendingShadowCourtResolution = null;
            if (OpenAfterResolutionWindow(facts)) return true;
            ContinueAfterResolution(facts.ProvinceIndex, facts.Attacker, facts.AttackerWon, facts.AttackerIds.Where(_state.Cards.ContainsKey).Select(Card).ToArray());
            return true;
        }
        if (OpenShadowCourtTrait(p.ResumePlayerId) || OpenSincerityTrait(p.ResumePlayerId)) return true;
        if (OpenOverconfidentDrawWindow(p.ResumePlayerId)) return true;
        ContinueAfterActionTail(p.ActingPlayerId, p.ResumePlayerId);
        return true;
    }
}

public sealed partial class MatchEngine
{
    private bool HandleRegisteredTwistOfFateReaction(string type, GameCommand command)
    {
        if (type != "twistOfFateReaction") return false;
        ResolveTwistOfFateWindow(command);
        return true;
    }
    private bool HandleRegisteredKillingGroundsSelect(string type, GameCommand command)
    {
        if (type != "killingGroundsSelect") return false;
        ResolveKillingGroundsSelect(command);
        return true;
    }
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorRemainingDHooks()
    {
        EngineHooks.Abilities.Register("AddEmperorRemainingD", 700, static abilities => abilities.AddEmperorRemainingD());
        EngineHooks.PerformerAllows.Register("RemainingDPerformerAllows", 300, static (engine, context) => engine.RemainingDPerformerAllows(context.ability, context.performer, context.actingAsBattle));
        EngineHooks.TargetAllows.Register("RemainingDTargetAllows", 300, static (engine, context) => engine.RemainingDTargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PerformerCandidates.Register("RemainingDPerformers", 300, static (engine, context) => engine.RemainingDPerformers(context.ability, context.playerId, context.actingAsBattle));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorRemainingD", 500, static (engine, context) => engine.ValidateEmperorRemainingD(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorRemainingD", 1200, static (engine, context) => engine.RunEmperorRemainingD(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.PendingChoice.Register("HandleRegisteredTwistOfFateReaction", 3100, static (engine, type, command) => engine.HandleRegisteredTwistOfFateReaction(type, command));
        EngineHooks.PendingChoice.Register("HandleRegisteredKillingGroundsSelect", 3200, static (engine, type, command) => engine.HandleRegisteredKillingGroundsSelect(type, command));
        EngineHooks.FinishEitherOr.Register("FinishEmperorRemainingDEitherOr", 100, static (engine, p) => engine.FinishEmperorRemainingDEitherOr(p));
    }
}
