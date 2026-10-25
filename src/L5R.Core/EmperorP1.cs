namespace L5R.Core;

// ======================================================================================================================================================================================
// ISSUE #503, GROUP P1: the unwired Embers of War / Seeds of Decay cards the blocker review called "wirable now" (WIRE) or "one narrow extension" (SMALL). Every card was read letter by letter against
// its scan (the scan is authoritative; here it matches the catalogue's Oracle text for all of them).
//
// Wired: Asako Megu, Bayushi Nomen - exp2, Hida Bakishi, Hida Osote, Ikoma Hakige, Isawa Koiso, Itoku - exp, Akodo Tamisu, Isawa Sanetomo, Moto Taigo, Tsuruchi Shichiro (personality-wave-96),
// Damning Ceremony, Extending Influence, Mara's Touch, Ochiai's Bravery (spell-wave-26), Seeking the Question (strategy-wave-82), Hotei's Contentment (celestial-wave-10).
//
// New primitives (each checked against docs/rules/EMPEROR_EDITION_CORE_RULES.md; none conflicts with it):
//   * COST-ONLY SLOTS (TargetSlot.CostOnly): a card an action names only to pay a cost with ("destroy your Shugenja with the base Fire keyword, discard a card", Isawa Koiso) is chosen in a slot of its
//     own but is not a TARGET: it is not the command's primary target, opens no targeted-by-action window and is not among the action's targets for a targeting trait.
//   * A FORCE PENALTY AS A COST (CostVerbs.ForcePenalty, "Give this Shugenja -2F:", Mara's Touch): payable only when the wielder has that much Force to lose (core rules, Pay Costs), Cursed Relic's Chi
//     penalty twin.
//   * THE PER-TACTICIAN CAP (core rules Glossary, Tactical / Tactician): "each individual Tactician may perform at most one Tactical action per turn" -- enforced for every performing Personality (counted
//     where the action's effects resolve, so a wholly negated action does not count: Mendon's counter ruling); Moto Taigo's printed trait raises his own cap to three.
//   * A TARGETING TRAIT THAT OWES A DISCARD (Hida Osote): judged as an action is taken (its targets known); the discard is queued on the action and owed when it ends (resolved, or wholly negated),
//     answered through the ordinary forced-discard decision.
//   * A ONE-SHOT "NEXT TIME ANOTHER PLAYER'S ACTION TARGETS HIM" SHIELD (Hida Bakishi): a watch for the turn, spent by the next such announcement, which then negates every effect of that one action
//     on him (NegatesEffect, the Champion of Thunder "all" scope) for as long as that action resolves (its Attack or duel included).
//   * AFTER-ACTION TRAITS WITH A CHOICE (Akodo Tamisu's mandatory "give a Lion Clan Personality +1F", Tsuruchi Shichiro's optional once-per-battle move home, Ochiai's Bravery's "destroy a card without
//     attachments in the unit of the duel's loser"): queued where the action is truly done and answered one at a time through the general Either/Or decision (one option per candidate; a single legal
//     option resolves by itself) BEFORE any Reaction to the same moment (traits first, core rules). A Terrain played from hand (PlayStrategy) queues Tamisu's trait too and it is asked before priority
//     passes on.
//   * THE EQUIP BATTLE EXEMPTION (Isawa Sanetomo): once per battle one of his controller's Phoenix Clan Shugenja at the battlefield may perform the rulebook Equip (Battle) action while bowed or unopposed
//     (a Spell only: the rulebook Equip Battle attaches Spells); the exemption is spent only when it was needed.
//   * "MAY NOT ASSIGN TO, AND WILL NOT MOVE INTO, AN ATTACKING ARMY" for one Personality until the end of the turn (Hotei's Contentment; Goju Asuka's defending-army bar's mirror).
// ======================================================================================================================================================================================
public static class EmperorP1Abilities
{
    public const string HakigeMoveHome = "hakige-move-home", HakigeForce = "hakige-force-bonus";
    public const string NomenMoveHome = "nomen-exp2-move-home";
    public const string MeguBow = "megu-bow-straighten";
    public const string KoisoLimited = "koiso-limited-destroy", KoisoBattle = "koiso-battle-destroy";
    public const string ItokuRing = "itoku-exp-ring-straighten";
    public const string BakishiShield = "bakishi-shield";
    public const string TaigoBow = "taigo-tactical-bow";
    public const string DamningCeremony = "damning-ceremony";
    public const string ExtendingInfluenceCopy = "extending-influence-copy";
    public const string MaraCavalry = "maras-touch-cavalry", MaraAttack = "maras-touch-attack";
    public const string OchiaiDuel = "ochiai-bravery-duel";
    public const string SeekingLook = "seeking-question-look", SeekingEnter = "seeking-question-enter";
    public const string HoteiLock = "hotei-contentment-lock";

    // Effects (Definition.Effect).
    public const string ItokuEffect = "itoku-exp-lion-force";
    public const string OsoteEffect = "osote-sensei-discard-trait";
    public const string TamisuEffect = "tamisu-sensei-lion-bonus";
    public const string SanetomoEffect = "sanetomo-sensei-equip";
    public const string ShichiroEffect = "shichiro-sensei-ranged-home";
    public const string TaigoEffect = "taigo-three-tactical";
    public const string ExtendingInfluenceEffect = "extending-influence-air-honor";
    public const string MaraEffect = "maras-touch-nonhuman-force";
    public const string OchiaiEffect = "ochiai-bravery-fire-duelist";
    public const string HoteiEffect = "hotei-contentment-proclaim";

    // Target categories.
    public const string NonUniqueUnitCard = "p1-non-unique-unit-card-without-attachments";
    public const string UnitCardWithoutAttachments = "p1-unit-card-without-attachments";
    public const string OwnBaseFireShugenja = "p1-own-base-fire-shugenja-other-than-source";
    public const string TaigoCollateral = "p1-taigo-collateral";
    public const string ItokuRingTarget = "p1-own-non-void-ring-bowed-by-last-action";

    public static Dictionary<string, AbilityDefinition> AddEmperorP1(this Dictionary<string, AbilityDefinition> all)
    {
        // Ikoma Hakige (oracle-l5r-3607): "Battle: Target another Personality: Move him home or give him a Force bonus equal to his Personal Honor." -- two abilities sharing one use.
        all[HakigeMoveHome] = new("Battle", "Target another Personality: Move him home", "other-personality", UsageKey: "hakige");
        all[HakigeForce] = new("Battle", "Target another Personality: Give him a Force bonus equal to his Personal Honor", "other-personality", UsageKey: "hakige");
        // Bayushi Nomen - exp2 (oracle-l5r-853): "Political Battle: Even if Nomen is not at the current battlefield, bow her and target a Personality: Move him home." (Aoi's shape.)
        all[NomenMoveHome] = new("Battle", "Political Battle: Even if Nomen is not at the current battlefield, bow her and target a Personality: Move him home", "personality",
            ActionKeywords: ["Political"], Costs: ActionCost.BowSource, SourceUnitMayBeAway: true);
        // Asako Megu (oracle-l5r-561): "Battle: Target an enemy Personality: Bow him. You may target and straighten a Personality. If the enemy Personality is Kolat, Ninja, or Shadowlands, gain 2 Honor."
        all[MeguBow] = new("Battle", "Target an enemy Personality: Bow him. You may target and straighten a Personality. If the enemy Personality is Kolat, Ninja, or Shadowlands, gain 2 Honor", "slots",
            Slots: [new("target", "enemy-personality", "An enemy Personality to bow"), new("straighten", "personality", "A Personality to straighten (optional)", 0, 1)]);
        // Isawa Koiso (oracle-l5r-3849): "Limited: Bow Koiso, destroy your Shugenja with the base Fire keyword, discard a card, and target a non-Unique card without attachments in a unit: Destroy it."
        all[KoisoLimited] = new("Limited", "Bow Koiso, destroy your Shugenja with the base Fire keyword, discard a card, and target a non-Unique card without attachments in a unit: Destroy it", "slots",
            Slots: [new("target", NonUniqueUnitCard, "A non-Unique card without attachments in a unit"),
                new("fire", OwnBaseFireShugenja, "Your Shugenja with the base Fire keyword, to destroy (a cost)") { CostOnly = true },
                new("discard", "hand", "A card from your hand, to discard (a cost)") { CostOnly = true }],
            Costs: [new(CostVerb.Bow), new(CostVerbs.Sacrifice, CostSubject.Slot("fire")), new(CostVerbs.Discard, CostSubject.Slot("discard"))]);
        // "Battle: Target a card without attachments in a unit: Destroy it."
        all[KoisoBattle] = new("Battle", "Target a card without attachments in a unit: Destroy it", UnitCardWithoutAttachments);
        // Itoku - exp (oracle-l5r-3972): "Reaction: After an action resolves which bowed your non-Void Ring: Straighten it. You may use each of its abilities an additional time this turn."
        all[ItokuRing] = new("Reaction", "After an action resolves which bowed your non-Void Ring: Straighten it. You may use each of its abilities an additional time this turn", ItokuRingTarget,
            Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Hida Bakishi (oracle-l5r-3075): "Reaction: After an action resolves that Bakishi performed and that destroyed a card at the current battlefield: The next time (this turn) another player's
        // action targets Bakishi, negate its effects on him."
        all[BakishiShield] = new("Reaction", "After an action resolves that Bakishi performed and that destroyed a card at the current battlefield: The next time (this turn) another player's action targets Bakishi, negate its effects on him",
            "none", Trigger: AfterActionReactionTriggers.AfterActionResolved);
        // Moto Taigo (oracle-l5r-10100): "Tactical Battle: Target an enemy Personality: Bow him. If he is Infantry, you may target and destroy a card without attachments in his unit."
        all[TaigoBow] = new("Battle", "Tactical Battle: Target an enemy Personality: Bow him. If he is Infantry, you may target and destroy a card without attachments in his unit", "slots", ActionKeywords: ["Tactical"],
            Slots: [new("target", "enemy-personality", "An enemy Personality to bow"), new("destroy", TaigoCollateral, "If he is Infantry: a card without attachments in his unit, to destroy (optional)", 0, 1)]);
        // Damning Ceremony (oracle-l5r-1808, a Maho Spell): "Limited: Once per game: Give this Personality a permanent Force bonus raising his Force to equal his base Gold Cost. Permanently give him
        // Shadowlands • Nonhuman • Oni. Lose 2 Honor."
        all[DamningCeremony] = new("Limited", "Once per game: Give this Personality a permanent Force bonus raising his Force to equal his base Gold Cost. Permanently give him Shadowlands, Nonhuman and Oni. Lose 2 Honor",
            "none", OncePerGame: true, ActionKeywords: ["Maho"]);
        // Extending Influence (oracle-l5r-2420, an Air Spell): "Open: Destroy this card and target a Personality: The performing Shugenja copies one of the target's abilities which does not copy abilities."
        all[ExtendingInfluenceCopy] = new("Open", "Destroy this card and target a Personality: The performing Shugenja copies one of the target's abilities which does not copy abilities", "personality",
            Performer: "own-wielder", Costs: [new(CostVerbs.Sacrifice)], CopiesAbilities: true);
        // Mara's Touch (oracle-l5r-4833, a Pearl Spell): "Open: Give this Shugenja -2F: He changes shape. Give him Cavalry." / "Battle: Give this Shugenja -2F: Ranged 6 Attack."
        all[MaraCavalry] = new("Open", "Give this Shugenja -2F: He changes shape. Give him Cavalry", "none", Costs: [new(CostVerbs.ForcePenalty, CostSubject.Wielder, Amount: 2)]);
        all[MaraAttack] = new("Battle", "Give this Shugenja -2F: Ranged 6 Attack", "attack", Costs: [new(CostVerbs.ForcePenalty, CostSubject.Wielder, Amount: 2)]);
        // Ochiai's Bravery (oracle-l5r-5666, a Fire Spell): "Battle: If this Shugenja is unbowed, bow this card and target an enemy Personality: This Shugenja duels him. Destroy a card without attachments
        // in the unit of the duel's loser."
        all[OchiaiDuel] = new("Battle", "If this Shugenja is unbowed, bow this card and target an enemy Personality: This Shugenja duels him. Destroy a card without attachments in the unit of the duel's loser",
            "enemy-personality", Performer: "own-wielder", IssuesDuel: true, Costs: ActionCost.BowSource);
        // Seeking the Question (oracle-l5r-6565, a Void Kata Strategy): "Limited: If this card is in play: Look at the top card of any deck. You may put it at the bottom of that deck." and
        // "Limited: Put this card into play. Discard all your other Kata (in play)."
        all[SeekingLook] = new("Limited", "If this card is in play: Look at the top card of any deck. You may put it at the bottom of that deck", "none", Look: EmperorP1Looks.SeekingTheQuestion);
        all[SeekingEnter] = new("Limited", "Put this card into play. Discard all your other Kata (in play)", "none");
        // Hotei's Contentment (oracle-l5r-3484, a Unique Fortune Celestial): "Open: Discard this card and target a Personality: He may not assign to, and will not move into, an attacking army."
        all[HoteiLock] = new("Open", "Discard this card and target a Personality: He may not assign to, and will not move into, an attacking army", "personality", Costs: [new(CostVerbs.DiscardFromPlay)]);
        return all;
    }
}

public static class EmperorP1Looks
{
    /// <summary>Seeking the Question: "Look at the top card of any deck. You may put it at the bottom of that deck." -- any of the four decks.</summary>
    public static LookSpec SeekingTheQuestion { get; } = new("seeking-the-question", "Look at the top card of any deck",
        [new("Fate"), new("Dynasty"), new("Fate", Opponent: true), new("Dynasty", Opponent: true)], 1, [new(LookDisposition.Bottom, 0, 1)]);

    public static IEnumerable<LookSpec> All => [SeekingTheQuestion];
}

/// <summary>An after-action trait of this group waiting for its decision: Kind "tamisu" (a Lion Clan Personality gets +1F), "shichiro" (move the performer home) or "ochiai" (destroy a card without
/// attachments in the loser's unit). SourceId is the card whose trait it is, SubjectId the Personality it is about, DeciderId who chooses, ActingPlayerId whose action it was.</summary>
public sealed class P1PendingTrait
{
    public string Kind { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string? SubjectId { get; set; }
    public int DeciderId { get; set; }
    public int ActingPlayerId { get; set; }
}

/// <summary>A discard owed by Hida Osote's trait: PlayerId discards when the action named by ActionKey ("player|ability|card") ends.</summary>
public sealed class P1OwedDiscard
{
    public int PlayerId { get; set; }
    public string ActionKey { get; set; } = "";
    public string Reason { get; set; } = "";
}

/// <summary>What group P1 keeps between commands (issue #503).</summary>
public sealed class EmperorP1State
{
    /// <summary>Tactical actions each performing Personality took this turn: "cardId|turn" -> count.</summary>
    public Dictionary<string, int> TacticalActions { get; set; } = [];
    public List<P1OwedDiscard> OwedDiscards { get; set; } = [];
    /// <summary>Forced discards this group queued that still need their decision opened.</summary>
    public int QueuedDiscards { get; set; }
    /// <summary>Hida Bakishi's armed watches ("cardId|turn") and the shields they became ("cardId|actionKey").</summary>
    public List<string> BakishiWatches { get; set; } = [];
    public List<string> BakishiShields { get; set; } = [];
    public List<P1PendingTrait> PendingTraits { get; set; } = [];
    /// <summary>Once-per-battle marks: "shichiro|cardId|battle", "sanetomo|cardId|battle".</summary>
    public List<string> BattleMarks { get; set; } = [];
    /// <summary>Hotei's Contentment: "cardId|turn" of Personalities that will not move into an attacking army.</summary>
    public List<string> HoteiLocks { get; set; } = [];
}

public static class EmperorP1Specs
{
    public const string Tamisu = "p1-tamisu-lion-bonus";
    public const string Shichiro = "p1-shichiro-move-home";
    public const string Ochiai = "p1-ochiai-destroy";
    public static bool Is(string specId) => specId is Tamisu or Shichiro or Ochiai;
    public const string AfterActionOrigin = "p1-after-action";
    public const string NextActionOrigin = "p1-next-action";
}

public sealed partial class MatchEngine
{
    private EmperorP1State P1 => _state.P1;

    // ---- target categories (ValidateAbility's default chain; slot candidates are judged through it too) ----

    private bool EmperorP1TargetAllows(string category, GameCommand command, PlayerState player, CardInstance? source, CardInstance target) => category switch
    {
        // Isawa Koiso: "a non-Unique card without attachments in a unit" / "a card without attachments in a unit" (either side's).
        EmperorP1Abilities.NonUniqueUnitCard => IsUnitCardWithoutAttachments(target) && !Keyword(target, "Unique"),
        EmperorP1Abilities.UnitCardWithoutAttachments => IsUnitCardWithoutAttachments(target),
        // "your Shugenja with the base Fire keyword" -- a Personality of the acting player's whose PRINTED keywords include Fire; not Koiso himself (he is bowed by the same cost).
        EmperorP1Abilities.OwnBaseFireShugenja => target.OwnerId == player.Id && InPlay(target) && Definition(target).Type == CardType.Personality && Keyword(target, "Shugenja")
            && PrintedKeyword(target, "Fire") && target.Id != source?.Id,
        // Moto Taigo: a card without attachments in the first target's unit, while that target is Infantry (no Cavalry).
        EmperorP1Abilities.TaigoCollateral => IsUnitCardWithoutAttachments(target) && SlotCards(command, "target").FirstOrDefault() is { } bowed
            && UnitHostId(target) == UnitHostId(bowed) && !Keyword(bowed, "Cavalry"),
        // Itoku: one of the acting player's own non-Void Rings the last resolved action bowed.
        EmperorP1Abilities.ItokuRingTarget => Definition(target).Type == CardType.Ring && target.OwnerId == player.Id && InPlay(target) && !Keyword(target, "Void")
            && _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == BowedRingKind && a.CardId == target.Id),
        _ => false
    };

    // ---- validation ----

    private void ValidateEmperorP1(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        // The per-Tactician cap (core rules): a Tactical action's performing Personality may not have performed his cap of Tactical actions this turn.
        if (IsAction(ability) && !TraitReactions.Is(id) && ActionCarriesKeyword(ability, source, "Tactical") && ActionPerformerIdOrNull(ability, command, source) is { } tactician)
            Require(TacticalActionsThisTurn(tactician) < TacticalCap(Card(tactician)), $"{Definition(Card(tactician)).Name} has already performed {(TacticalCap(Card(tactician)) == 1 ? "a Tactical action" : "his Tactical actions")} this turn.");
        switch (id)
        {
            case EmperorP1Abilities.OchiaiDuel:
                Require(source is { Zone: Zone.Attached } && WielderOf(source) is { Bowed: false }, "This Shugenja must be unbowed.");
                break;
            case EmperorP1Abilities.SeekingLook:
                Require(source is not null && source.Zone == Zone.Home, "Seeking the Question must be in play.");
                break;
            case EmperorP1Abilities.SeekingEnter:
                Require(source is not null && SourceInHand(source, player), "A Kata is put into play from your hand.");
                break;
            case EmperorP1Abilities.BakishiShield:
                Require(source is not null && BakishiApplies(source), "Bakishi did not just perform an action that destroyed a card at the current battlefield.");
                break;
            case "equip-battle":
                // Isawa Sanetomo's exemption covers the rulebook Equip Battle action, which attaches a Spell.
                if (command.PerformerId is { } equipper && _state.Cards.TryGetValue(equipper, out var shugenja) && SanetomoExemptionNeeded(player.Id, shugenja) && command.TargetId is { } spellId)
                    Require(Definition(Card(spellId)).Type == CardType.Spell, "Isawa Sanetomo's exemption lets a Shugenja perform the Equip Battle action only with a Spell.");
                break;
        }
    }

    // ---- execution: null when the id is not this group's; false when its effects are done; true when a decision opened ----

    private bool? RunEmperorP1(string id, AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source, CardInstance? target, bool consumedGrant, bool playedFromDiscard,
        int? opponentHonorBeforeBattleAction, HashSet<string> bowedBeforeAction, List<AdditionalActionGrant> preExistingGrants)
    {
        switch (id)
        {
            case EmperorP1Abilities.HakigeMoveHome: MoveHome(target!, false); return false;
            case EmperorP1Abilities.HakigeForce: ApplyModifier(target!, source!.Id, id, "Force", CardPersonalHonor(target!)); return false;
            case EmperorP1Abilities.NomenMoveHome: MoveHome(target!, false); return false;
            case EmperorP1Abilities.MeguBow:
                {
                    var enemy = SlotCards(command, "target").Single();
                    if (InPlay(enemy) && !StalwartNegatesBow(enemy, player.Id)) enemy.Bowed = true;
                    if (SlotCards(command, "straighten").FirstOrDefault() is { } straightened && InPlay(straightened)) Straighten(straightened);
                    if (MatchesTargetStatus(enemy, TargetStatus.KolatNinjaOrShadowlands)) GainHonorFromPerformer(source!, 2, Definition(source!).Name);
                    return false;
                }
            case EmperorP1Abilities.KoisoLimited:
                {
                    var doomed = SlotCards(command, "target").Single();
                    if (IsUnitCardWithoutAttachments(doomed)) DestroyUnitCard(doomed, player.Id);
                    return false;
                }
            case EmperorP1Abilities.KoisoBattle:
                if (IsUnitCardWithoutAttachments(target!)) DestroyUnitCard(target!, player.Id);
                return false;
            case EmperorP1Abilities.ItokuRing:
                Straighten(target!);
                target!.AbilityUses[SecondUseKey] = _state.TurnNumber; // "You may use each of its abilities an additional time this turn" (The Fires of War's mark, read by AbilityLimits.cs).
                Emit("abilitiesRefreshed", $"{Definition(source!).Name}: {Definition(target).Name} may use each of its abilities an additional time this turn.", player.Id, target.Id);
                return false;
            case EmperorP1Abilities.BakishiShield:
                P1.BakishiWatches.RemoveAll(w => w.StartsWith(source!.Id + "|", StringComparison.Ordinal));
                P1.BakishiWatches.Add($"{source!.Id}|{_state.TurnNumber}");
                Emit("watchSet", $"{Definition(source).Name}: the next time this turn another player's action targets him, its effects on him are negated.", player.Id, source.Id);
                return false;
            case EmperorP1Abilities.TaigoBow:
                {
                    var enemy = SlotCards(command, "target").Single();
                    if (InPlay(enemy) && !StalwartNegatesBow(enemy, player.Id)) enemy.Bowed = true;
                    if (InPlay(enemy) && !Keyword(enemy, "Cavalry") && SlotCards(command, "destroy").FirstOrDefault() is { } collateral && IsUnitCardWithoutAttachments(collateral) && UnitHostId(collateral) == enemy.Id)
                        DestroyUnitCard(collateral, player.Id);
                    return false;
                }
            case EmperorP1Abilities.DamningCeremony:
                {
                    var wielder = WielderOf(source!);
                    if (wielder is not null && InPlay(wielder))
                    {
                        var raise = Math.Max(0, Definition(wielder).GoldCost + Wave821GoldCost(wielder) /* issue #821: Chuda Kaito reads 5 while his Spell resolves */ - CardForce(wielder));
                        if (raise > 0) AddPermanentModifier(wielder, source!.Id, id, "Force", raise);
                        foreach (var keyword in new[] { "Shadowlands", "Nonhuman", "Oni" }) GiveKeywordPermanently(wielder, keyword, source!.Id);
                    }
                    GainHonor(player.Id, -2, Definition(source!).Name, source);
                    return false;
                }
            case EmperorP1Abilities.ExtendingInfluenceCopy:
                {
                    var shugenja = Card(command.PerformerId!);
                    return BeginCopy(shugenja, target!, id, [CopyStep.Ability], player.Id, "ability", bowedBeforeAction, GrantDuration.Turn);
                }
            case EmperorP1Abilities.MaraCavalry:
                if (WielderOf(source!) is { } shaped && InPlay(shaped)) GrantKeyword(shaped, "Cavalry", source!.Id, GrantDuration.Turn);
                return false;
            case EmperorP1Abilities.MaraAttack:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(ability, command, source).Id, consumedGrant);
            case EmperorP1Abilities.SeekingEnter: EnterPlayAsKata(player, source!); return false;
            case EmperorP1Abilities.SeekingLook: BeginLook(ability.Look!, source!, player.Id, "ability", bowedBeforeAction); return true; // the look finishes the action (DeckLook.cs)
            case EmperorP1Abilities.HoteiLock:
                LockMovement(target!, source!.Id, MovementKinds.AssignAttack);
                P1.HoteiLocks.Add($"{target!.Id}|{_state.TurnNumber}");
                Emit("movementLocked", $"{Definition(target).Name} may not assign to, and will not move into, an attacking army this turn (Hotei's Contentment).", player.Id, target.Id);
                return false;
            default:
                return null;
        }
    }

    // ---- attacks ----

    private static int? EmperorP1AttackStrength(string abilityId) => abilityId == EmperorP1Abilities.MaraAttack ? 6 : null;
    private static string? EmperorP1AttackKind(string abilityId) => abilityId == EmperorP1Abilities.MaraAttack ? "Ranged" : null;

    // ---- statics: Force and keywords ----

    // PersonalityForce's hook: Itoku's +1F while a Lion Clan player controls him; Mara's Touch's +2F to a Nonhuman wielder.
    private int EmperorP1Force(CardInstance card)
    {
        if (Definition(card).Type != CardType.Personality) return 0;
        var bonus = Definition(card).Effect == EmperorP1Abilities.ItokuEffect && Player(card.OwnerId).Clan == "Lion" ? 1 : 0;
        if (card.Zone != Zone.Province && Keyword(card, "Nonhuman"))
            bonus += 2 * StaticSpells(card).Count(a => Definition(a).Effect == EmperorP1Abilities.MaraEffect);
        return bonus;
    }

    // Keyword's hook: Ochiai's Bravery -- "While this Shugenja is Fire, he has Duelist."
    private bool OchiaiGrantsDuelist(CardInstance card, string keyword) =>
        keyword == "Duelist" && Definition(card).Type == CardType.Personality && card.Zone != Zone.Province
        && StaticSpells(card).Any(a => Definition(a).Effect == EmperorP1Abilities.OchiaiEffect) && Keyword(card, "Fire");

    // ---- entering play ----

    // ApplyMandatoryEntryEffects' hook: the Sensei discard of this group's Sensei (their Effect field carries their other trait), and Extending Influence's "After this card enters play, if this
    // Shugenja is Air: Each other player loses 1 Honor."
    private void ApplyEmperorP1EntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect is EmperorP1Abilities.OsoteEffect or EmperorP1Abilities.TamisuEffect or EmperorP1Abilities.SanetomoEffect or EmperorP1Abilities.ShichiroEffect)
            DiscardOtherSensei(playerId, card);
        if (Definition(card).Effect == EmperorP1Abilities.ExtendingInfluenceEffect && WielderOf(card) is { } wielder && Keyword(wielder, "Air"))
            foreach (var other in _state.Players.Where(p => p.Id != playerId).ToArray()) GainHonor(other.Id, -1, Definition(card).Name, card);
    }

    // ---- Hotei's Contentment ----

    // FinishProclaim's hook: "Before you gain Honor from the Proclaim action, if the Personality's base PH is 3 or lower: Increase the Honor gain by 1."
    private int HoteiProclaimBonus(int playerId, CardInstance personality) =>
        Definition(personality).PersonalHonor <= 3 && _state.Cards.Values.Any(c => c.OwnerId == playerId && c.Zone == Zone.Celestial && Definition(c).Effect == EmperorP1Abilities.HoteiEffect) ? 1 : 0;

    private bool HoteiLocked(CardInstance card) => P1.HoteiLocks.Contains($"{card.Id}|{_state.TurnNumber}");

    // Assign's hook: "He may not assign to ... an attacking army."
    private void RequireHoteiMayAssign(CardInstance card, bool isDefender) =>
        RequireNotBarred(isDefender || !HoteiLocked(card), $"{Definition(card).Name} assigning to an attacking army", "Hotei's Contentment");

    // MoveToBattlefield's hook: "... and will not move into, an attacking army" -- a move puts the unit on its controller's side, the attacking army when he is the Attacker.
    private bool HoteiNegatesMove(CardInstance card)
    {
        if (!HoteiLocked(card) || card.OwnerId != _state.ActivePlayerId || _state.CurrentBattleProvince is null && _state.UnresolvedBattlefields.Count == 0) return false;
        Emit("moveNegated", $"{Definition(card).Name} will not move into an attacking army (Hotei's Contentment).", card.OwnerId, card.Id);
        return true;
    }

    // ---- the per-Tactician cap ----

    private int TacticalActionsThisTurn(string cardId) => P1.TacticalActions.TryGetValue($"{cardId}|{_state.TurnNumber}", out var n) ? n : 0;
    private static int TacticalCap(CardInstance card) => Definition(card).Effect == EmperorP1Abilities.TaigoEffect ? 3 : 1;

    // RunAbilityEffects' hook (beside the Recon counter): an action whose effects resolve counts for its performing Personality.
    private void CountP1TacticalAction(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (!IsAction(ability) || TraitReactions.Is(command.AbilityId ?? "") || !ActionCarriesKeyword(ability, source, "Tactical") || ActionPerformerIdOrNull(ability, command, source) is not { } performer) return;
        foreach (var stale in P1.TacticalActions.Keys.Where(k => !k.EndsWith($"|{_state.TurnNumber}", StringComparison.Ordinal)).ToArray()) P1.TacticalActions.Remove(stale);
        P1.TacticalActions[$"{performer}|{_state.TurnNumber}"] = TacticalActionsThisTurn(performer) + 1;
    }

    // ---- Isawa Sanetomo ----

    private CardInstance? SanetomoFor(int playerId) => _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == EmperorP1Abilities.SanetomoEffect)
        .OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(c => !P1.BattleMarks.Contains($"sanetomo|{c.Id}|{_state.BattleNumber}"));

    // A Phoenix Clan Shugenja of the player's at the current battlefield who could perform Equip (Battle) only through the exemption (he is bowed, or unopposed), while an unspent Sanetomo is in play.
    private bool SanetomoExemptionNeeded(int playerId, CardInstance performer) =>
        performer.OwnerId == playerId && Definition(performer).Type == CardType.Personality && AtBattle(performer) && Keyword(performer, "Shugenja") && HasClanAffiliation(performer, "Phoenix")
        && (performer.Bowed || !Army(1 - playerId, _state.CurrentBattleProvince!.Value).Any()) && SanetomoFor(playerId) is not null;

    private IEnumerable<string?> SanetomoEquipPerformers(int playerId) =>
        _state.CurrentBattleProvince is null ? [] : _state.Cards.Values.Where(c => SanetomoExemptionNeeded(playerId, c)).OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => (string?)c.Id);

    // ---- announcement: Osote, Bakishi, Sanetomo ----

    // UseAbility's hook, right after validation: the action is taken and its targets are known.
    private void NoteP1Announcement(GameCommand command)
    {
        if (command.AbilityId is not { } abilityId || !AbilityCatalog.All.TryGetValue(abilityId, out var ability)) return;
        if (abilityId == "equip-battle" && command.PerformerId is { } equipper && SanetomoExemptionNeeded(command.PlayerId, Card(equipper)) && SanetomoFor(command.PlayerId) is { } sanetomo)
        {
            P1.BattleMarks.Add($"sanetomo|{sanetomo.Id}|{_state.BattleNumber}");
            Emit("exemptionUsed", $"{Definition(sanetomo).Name}: {Definition(Card(equipper)).Name} performs the Equip Battle action even if bowed and even if unopposed (once this battle).", command.PlayerId, sanetomo.Id);
        }
        if (!IsAction(ability) || TraitReactions.Is(abilityId)) return;
        var targeted = ActionTargetIds(command).Where(_state.Cards.ContainsKey).Distinct().Select(Card).ToArray();
        if (targeted.Length == 0) return;
        var key = ActionKey(command);
        // Hida Bakishi's watch: the next time this turn another player's action targets him.
        foreach (var bakishi in targeted.Where(c => c.OwnerId != command.PlayerId && InPlay(c) && P1.BakishiWatches.Contains($"{c.Id}|{_state.TurnNumber}")).ToArray())
        {
            P1.BakishiWatches.Remove($"{bakishi.Id}|{_state.TurnNumber}");
            P1.BakishiShields.Add($"{bakishi.Id}|{key}");
            Emit("shieldRaised", $"{Definition(bakishi).Name}: the effects of this action on him are negated.", bakishi.OwnerId, bakishi.Id);
        }
        // Hida Osote's trait: the first time each turn another player's action targets one of his controller's Crab Clan Personalities at his location.
        foreach (var osote in _state.Cards.Values.Where(c => InPlay(c) && c.OwnerId != command.PlayerId && Definition(c).Effect == EmperorP1Abilities.OsoteEffect && Definition(c).Type == CardType.Personality)
                     .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray())
        {
            if (osote.AbilityUses.TryGetValue(EmperorP1Abilities.OsoteEffect, out var turn) && turn == _state.TurnNumber) continue;
            if (!targeted.Any(t => t.OwnerId == osote.OwnerId && InPlay(t) && Definition(t).Type == CardType.Personality && HasClanAffiliation(t, "Crab") && SameLocation(t, osote))) continue;
            osote.AbilityUses[EmperorP1Abilities.OsoteEffect] = _state.TurnNumber;
            P1.OwedDiscards.Add(new P1OwedDiscard { PlayerId = command.PlayerId, ActionKey = key, Reason = Definition(osote).Name });
            Emit("discardOwed", $"{Definition(osote).Name}: {Player(command.PlayerId).Name} discards a card after this action ends.", osote.OwnerId, osote.Id);
        }
    }

    // NegatesEffect's hook: Bakishi's shield holds while the action it was raised against resolves.
    private bool BakishiShieldNegates(CardInstance card) =>
        P1.BakishiShields.Count > 0 && CurrentActionKey() is { } key && P1.BakishiShields.Contains($"{card.Id}|{key}");

    // An action ended (resolved, or taken with its effects wholly negated): its owed discards are due, its shields drop. Matched on "player|ability" (an Attack's record names its performer, not its card).
    private void P1ActionEnded(int playerId, string abilityId)
    {
        var prefix = $"{playerId}|{abilityId}|";
        foreach (var owed in P1.OwedDiscards.Where(o => o.ActionKey.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
        {
            P1.OwedDiscards.Remove(owed);
            _state.PendingForcedDiscards.Add(new(owed.PlayerId, owed.Reason));
            P1.QueuedDiscards++;
        }
        P1.BakishiShields.RemoveAll(s => s[(s.IndexOf('|') + 1)..].StartsWith(prefix, StringComparison.Ordinal));
    }

    // ---- after-action Reactions (AfterActionResolvedApplies' hook) ----

    private bool? EmperorP1AfterActionApplies(CardInstance source, string abilityId, int playerId) => abilityId switch
    {
        EmperorP1Abilities.ItokuRing => _state.LastResolvedAction is { } facts && facts.Affected.Any(a => a.Kind == BowedRingKind && Card(a.CardId) is var ring && ring.OwnerId == playerId && InPlay(ring) && !Keyword(ring, "Void")),
        EmperorP1Abilities.BakishiShield => BakishiApplies(source),
        _ => null
    };

    // "After an action resolves that Bakishi performed and that destroyed a card at the current battlefield."
    private bool BakishiApplies(CardInstance bakishi)
    {
        if (_state.LastResolvedAction is not { } facts || _state.CurrentBattleProvince is not int battle) return false;
        if ((facts.PerformerId ?? facts.SourceId) != bakishi.Id) return false;
        return facts.Affected.Any(a => a.Kind == DestroyedKind && (a.FromBattlefield == battle
            || a.HostId is { } host && _state.Cards.TryGetValue(host, out var hostCard) && hostCard.Zone == Zone.Battlefield && hostCard.ProvinceIndex == battle));
    }

    // ---- after-action traits: Tamisu, Shichiro, Ochiai's Bravery ----

    // FinishActionEffectTracking's hook, once the action's facts are recorded.
    private void QueueP1AfterActionTraits(ResolvedActionFacts facts)
    {
        P1ActionEnded(facts.PlayerId, facts.AbilityId);
        if (!AbilityCatalog.All.TryGetValue(facts.AbilityId, out var ability) || !IsAction(ability) || TraitReactions.Is(facts.AbilityId)) return;
        var sourceCard = facts.SourceId is not null && _state.Cards.TryGetValue(facts.SourceId, out var found) ? found : null;
        if (new[] { "Tactical", "Bushido Virtue", "Terrain" }.Any(k => ActionCarriesKeyword(ability, sourceCard, k))) QueueTamisu(facts.PlayerId);
        // Tsuruchi Shichiro: "After a Ranged Attack action resolves that your Mantis Clan Personality performed: Once per battle, you may move him home."
        if (_state.Phase == Phase.Battle && AttackKind(facts.AbilityId) == "Ranged" && (facts.PerformerId ?? facts.SourceId) is { } performerId && _state.Cards.TryGetValue(performerId, out var performer)
            && Definition(performer).Type == CardType.Personality && performer.OwnerId == facts.PlayerId && HasClanAffiliation(performer, "Mantis"))
            foreach (var shichiro in _state.Cards.Values.Where(c => c.OwnerId == facts.PlayerId && InPlay(c) && Definition(c).Effect == EmperorP1Abilities.ShichiroEffect
                         && !P1.BattleMarks.Contains($"shichiro|{c.Id}|{_state.BattleNumber}")).OrderBy(c => c.Id, StringComparer.Ordinal))
                P1.PendingTraits.Add(new P1PendingTrait { Kind = "shichiro", SourceId = shichiro.Id, SubjectId = performer.Id, DeciderId = facts.PlayerId, ActingPlayerId = facts.PlayerId });
    }

    // Akodo Tamisu: "After your Tactical, Bushido Virtue, or Terrain action resolves: Give a Lion Clan Personality +1F." -- a trait of each Tamisu the acting player has in play.
    private void QueueTamisu(int playerId)
    {
        foreach (var tamisu in _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Effect == EmperorP1Abilities.TamisuEffect).OrderBy(c => c.Id, StringComparer.Ordinal))
            P1.PendingTraits.Add(new P1PendingTrait { Kind = "tamisu", SourceId = tamisu.Id, DeciderId = playerId, ActingPlayerId = playerId });
    }

    // PlayStrategy's hook: playing a Terrain from hand is a Terrain action that resolved.
    private void QueueP1TerrainPlayTraits(int playerId) => QueueTamisu(playerId);

    // Ochiai's Bravery's duel outcome: one destruction owed per loser (a tie has two), chosen by the acting player once the duel's action is done.
    private void QueueOchiaiDestruction(List<string> loserIds, int challengerPlayerId)
    {
        var spellId = _state.PendingDuel?.SourceCardId ?? "";
        foreach (var loser in loserIds)
            P1.PendingTraits.Add(new P1PendingTrait { Kind = "ochiai", SourceId = spellId, SubjectId = loser, DeciderId = challengerPlayerId, ActingPlayerId = challengerPlayerId });
    }

    // Opens the next queued trait's decision (true), or finds nothing left (false). A trait with a single legal outcome is applied without asking.
    private bool OpenP1QueuedTraits(int resumePlayerId, string origin, int actingPlayerId)
    {
        while (P1.PendingTraits.Count > 0)
        {
            var next = P1.PendingTraits[0]; P1.PendingTraits.RemoveAt(0);
            if (!_state.Cards.TryGetValue(next.SourceId, out var source)) continue;
            var subject = next.SubjectId is not null && _state.Cards.TryGetValue(next.SubjectId, out var s) ? s : null;
            var spec = next.Kind switch { "tamisu" => EmperorP1Specs.Tamisu, "shichiro" => EmperorP1Specs.Shichiro, _ => EmperorP1Specs.Ochiai };
            if (next.Kind == "tamisu" && !InPlay(source)) continue;
            if (subject is null && next.Kind != "tamisu") continue;
            if (spec == EmperorP1Specs.Tamisu && !TamisuCandidates().Any()) { Emit("traitNothing", $"{Definition(source).Name}: there is no Lion Clan Personality to give +1F.", next.DeciderId, source.Id); continue; }
            if (spec == EmperorP1Specs.Ochiai && (!InPlay(subject!) || UnitCardsWithoutAttachments(subject!).Length == 0)) continue;
            if (origin == EmperorP1Specs.AfterActionOrigin) SuspendOpenWindow();
            if (OpenEitherOr(spec, source, actingPlayerId, next.DeciderId, subject, origin, resumePlayerId: resumePlayerId)) return true;
        }
        return false;
    }

    private IEnumerable<CardInstance> TamisuCandidates() =>
        _state.Cards.Values.Where(c => InPlay(c) && Definition(c).Type == CardType.Personality && HasClanAffiliation(c, "Lion")).OrderBy(c => c.Id, StringComparer.Ordinal);

    // FinishActionEffectTracking's hook, before the after-action Reactions (traits resolve first).
    private bool OpenP1AfterActionTraits(int closesTo, int actingPlayerId) => OpenP1QueuedTraits(closesTo, EmperorP1Specs.AfterActionOrigin, actingPlayerId);

    // NextAction's hook: a trait queued outside an ability (a Terrain played from hand), then the discards this group owes, before priority is used.
    private void OpenEmperorP1Deferred(int priorityPlayerId, int actingPlayerId)
    {
        if (_state.PendingChoice is not null) return;
        if (OpenP1QueuedTraits(priorityPlayerId, EmperorP1Specs.NextActionOrigin, actingPlayerId)) return;
        if (P1.QueuedDiscards > 0)
        {
            P1.QueuedDiscards = 0;
            OpenOverconfidentDrawWindow(priorityPlayerId);
        }
    }

    // ---- the Either/Or hooks ----

    private string EmperorP1EitherOrPrompt(PendingEitherOr p, string source) => p.SpecId switch
    {
        EmperorP1Specs.Tamisu => $"{source}: give a Lion Clan Personality +1F.",
        EmperorP1Specs.Shichiro => $"{source}: you may move {Definition(Card(p.AffectedCardId!)).Name} home (once per battle).",
        _ => $"{source}: destroy a card without attachments in {Definition(Card(p.AffectedCardId!)).Name}'s unit."
    };

    private EitherOrOption[] EmperorP1EitherOrOptions(PendingEitherOr p)
    {
        switch (p.SpecId)
        {
            case EmperorP1Specs.Tamisu:
                return TamisuCandidates().Select(c => new EitherOrOption(c.Id, $"Give {Definition(c).Name} +1F", $"{Definition(c).Name} has +1F until the end of the turn.", EitherOrKind.Choose, 0, true, null)).ToArray();
            case EmperorP1Specs.Shichiro:
                {
                    var performer = Card(p.AffectedCardId!);
                    var name = Definition(performer).Name;
                    return [
                        new("move", $"Move {name} home", $"{name}'s unit moves home.", EitherOrKind.Choose, 0, InPlay(performer) && performer.Zone == Zone.Battlefield, $"{name} is not at a battlefield."),
                    new("decline", "Do not move him", "Nothing happens.", EitherOrKind.Choose, 0, true, null)
                    ];
                }
            default:
                {
                    var loser = Card(p.AffectedCardId!);
                    return (InPlay(loser) ? UnitCardsWithoutAttachments(loser) : []).Select(c => new EitherOrOption(c.Id, $"Destroy {Definition(c).Name}", $"{Definition(c).Name} is destroyed.", EitherOrKind.Choose, 0, true, null)).ToArray();
                }
        }
    }

    private bool ApplyEmperorP1EitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (!EmperorP1Specs.Is(p.SpecId)) return false;
        var name = Definition(source).Name;
        Emit("eitherOrChosen", $"{decider.Name} chose: {option.Label} ({name}).", decider.Id, source.Id);
        switch (p.SpecId)
        {
            case EmperorP1Specs.Tamisu:
                ApplyModifier(Card(option.Key), source.Id, EmperorP1Abilities.TamisuEffect, "Force", 1);
                break;
            case EmperorP1Specs.Shichiro:
                if (option.Key == "move")
                {
                    P1.BattleMarks.Add($"shichiro|{source.Id}|{_state.BattleNumber}");
                    MoveHome(Card(p.AffectedCardId!), false);
                }
                break;
            default:
                {
                    // Part of the duel's action: what it destroys goes on that action's record.
                    DestroyUnitCard(Card(option.Key), p.ActingPlayerId);
                    if (_state.LastResolvedAction is { } facts) { facts.Affected.AddRange(_state.CurrentActionAffected); _state.CurrentActionAffected = []; }
                    break;
                }
        }
        return true;
    }

    // FinishEitherOr's hook: the next queued trait, else the rest of the resolved action's tail (after-action origin) or the priority as it was (next-action origin).
    private bool FinishEmperorP1EitherOr(PendingEitherOr p)
    {
        if (!EmperorP1Specs.Is(p.SpecId)) return false;
        _state.PendingChoice = null;
        if (OpenP1QueuedTraits(p.ResumePlayerId, p.Origin, p.ActingPlayerId)) return true;
        if (p.Origin == EmperorP1Specs.AfterActionOrigin) { ContinueAfterActionTail(p.ActingPlayerId, p.ResumePlayerId); return true; }
        _state.PriorityPlayerId = p.ResumePlayerId; _state.ConsecutivePasses = 0;
        OpenEmperorP1Deferred(p.ResumePlayerId, p.ActingPlayerId);
        return true;
    }
}

// This property keeps its original JSON name and tail position in old saved MatchState payloads.
public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1002)]
    public EmperorP1State P1 { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterEmperorP1Hooks()
    {
        EngineHooks.Abilities.Register("AddEmperorP1", 2400, static abilities => abilities.AddEmperorP1());
        EngineHooks.Looks.Register("EmperorP1Looks", 200, static () => EmperorP1Looks.All);
        EngineHooks.AttackStrengthAfter.Register("EmperorP1AttackStrength", 200, static (engine, context) => EmperorP1AttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("EmperorP1AttackKind", 200, static context => EmperorP1AttackKind(context.abilityId));
        EngineHooks.TargetAllows.Register("EmperorP1TargetAllows", 1200, static (engine, context) => engine.EmperorP1TargetAllows(context.ability.Target, context.command, context.player, context.source, context.target));
        EngineHooks.PersonalityForce.Register("EmperorP1Force", 400, static (engine, context) => engine.EmperorP1Force(context.card));
        EngineHooks.ValidateMiddle.Register("ValidateEmperorP1", 1200, static (engine, context) => engine.ValidateEmperorP1(context.id, context.ability, context.command, context.player, context.source));
        EngineHooks.RunEffects.Register("RunEmperorP1", 1400, static (engine, context) => engine.RunEmperorP1(context.id, context.ability, context.command, context.player, context.source, context.target, context.consumedGrant, context.playedFromDiscard, context.opponentHonorBeforeBattleAction, context.bowedBeforeAction, context.preExistingGrants));
        EngineHooks.EitherOrPrompt.Register("EmperorP1EitherOrPrompt", 100, static (engine, p, source, affected) => EmperorP1Specs.Is(p.SpecId) ? engine.EmperorP1EitherOrPrompt(p, source) : null);
        EngineHooks.EitherOrOptions.Register("EmperorP1EitherOrOptions", 100, static (engine, p, affected, source, payable) => EmperorP1Specs.Is(p.SpecId) ? engine.EmperorP1EitherOrOptions(p) : null);
        EngineHooks.ApplyEitherOr.Register("ApplyEmperorP1EitherOr", 100, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyEmperorP1EitherOr(p, option, source, decider));
        EngineHooks.FinishEitherOr.Register("FinishEmperorP1EitherOr", 400, static (engine, p) => engine.FinishEmperorP1EitherOr(p));
    }
}
