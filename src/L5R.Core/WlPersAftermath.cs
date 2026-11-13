namespace L5R.Core;

// Issue #674 ("wire-list", cluster pers-aftermath, personality-wave-132): seventeen Aftermath Personalities wired from their own Aftermath scans and PRINTED text with the engine's existing
// mechanisms. Each is a catalogue entry plus, where the printed effect needs it, a case in the feature's RunEffects hook. Reused outright (no new code):
//   Kaiu Esumi - exp  "Battle: Destroy a target enemy attachment."                            komatsu-destroy-attachment (Hida Komatsu's, as Chikara's)
//   Isidoros / Usui   "After X enters play, lose N Honor."                                      EntryHonorLoss (the Spider and Fudo entry traits)
//   Tametaka          "Mantis Clan players ignore Tametaka's Honor Requirement."                HonorRequirementWaiverClan (Akodo Chiyo's)
// This file carries the rest (ability ids are prefixed "wlpa-"):
//   Akodo Uehara - exp  "Limited: Put a target Terrain from your discard pile into your hand. Discard a card."   wlpa-uehara-terrain (own-card-in-discard, narrowed to a Terrain in the Fate discard
//                       pile; the discard is the engine's queued forced discard, asked once the action has resolved)
//   Bayushi Dijuro      "Siege Battle, Bow: Ranged 3 Attack. If this destroyed a card, give this Province +2 or -2 strength."   wlpa-dijuro-raise / wlpa-dijuro-lower (one printed choice, two abilities
//                       sharing one use: the Siege Volley / Togashi Hizumi shape)
//   Bayushi Wateru      "Ninja Interrupt: After you Recruit Wateru during battle, take an additional Battle action."   wlpa-wateru-additional (an entering-play Reaction, Battle phase only)
//   Daidoji Tametaka    "Battle: Give a target enemy Personality three -1F tokens."             wlpa-tametaka-tokens
//   Daigotsu Chiboshi   "Battle, Gold: Equip a Follower to Chiboshi. You may take an additional Battle action."   wlpa-chiboshi-equip (Hailung's attach-from-hand + Gyoken's grant)
//   Daigotsu Endo       "Limited, Gold: If Endo is dead, Recruit him."                           wlpa-endo-recruit (Kunji - exp's dead-pile recruit, paying all costs)
//   Ide Mutsuken        "Open, Bow: The active player must choose and bow one of his unbowed Personalities."   wlpa-mutsuken-bow (a decider choice made by the active player)
//   Isidoros            "Battle: Straighten your target Follower."                              wlpa-isidoros-straighten (own-follower, Katsuo's)
//   Kitsu Tamao         "Interrupt: After you Recruit Tamao, create a 2F/2C/3PH Lion Clan - Ancestor - Samurai - Spirit Personality."   wlpa-tamao-create (Kagako's entering-play creation)
//   Shiba Heirai        "Battle: If Heirai is opposed, discard a card to give him a Force bonus equal to the card's Focus Value."   wlpa-heirai-boost (Akodo Regulars' discard, gated on Opposed)
//   Shinjo Sujikaro     "Battle, Bow: Melee 3 Attack."                                          wlpa-sujikaro-melee
//   Susumu Takada       "Invest 3: Discard a card to gain Honor equal to its Focus Value."       wlpa-takada-invest (Clay Kshatriya's Invest window, a trait)
//   The Broken Man      "Invest 5: Give The Broken Man two +1F tokens. / Earth Battle: Bow a target enemy card with lower Force."   wlpa-brokenman-invest, wlpa-brokenman-bow
//   Tsuruchi Shusaku    "Repeatable Battle, Gold 2: Give Shusaku +1F."                          wlpa-shusaku-boost (any number of times a turn)
//   Usui                "Earth Battle: Give this Province +2 strength."                         wlpa-usui-province (the current battlefield's province, until the turn ends)
//   Yasuki Jiro         "Political Limited, Gold 3: Dishonor a target Personality."             wlpa-jiro-dishonor (Slanderer's dishonor)
// Readings, for Mendon to confirm or correct, are in docs/decisions/2026-10-06-674-pers-aftermath.md.
public static class WlPersAftermathAbilities
{
    public const string UeharaTerrain = "wlpa-uehara-terrain";
    public const string DijuroRaise = "wlpa-dijuro-raise";
    public const string DijuroLower = "wlpa-dijuro-lower";
    public const string WateruAdditional = "wlpa-wateru-additional";
    public const string TametakaTokens = "wlpa-tametaka-tokens";
    public const string ChiboshiEquip = "wlpa-chiboshi-equip";
    public const string EndoRecruit = "wlpa-endo-recruit";
    public const string MutsukenBow = "wlpa-mutsuken-bow";
    public const string IsidorosStraighten = "wlpa-isidoros-straighten";
    public const string TamaoCreate = "wlpa-tamao-create";
    public const string HeiraiBoost = "wlpa-heirai-boost";
    public const string SujikaroMelee = "wlpa-sujikaro-melee";
    public const string TakadaInvest = "wlpa-takada-invest";
    public const string BrokenManInvest = "wlpa-brokenman-invest";
    public const string BrokenManBow = "wlpa-brokenman-bow";
    public const string ShusakuBoost = "wlpa-shusaku-boost";
    public const string UsuiProvince = "wlpa-usui-province";
    public const string JiroDishonor = "wlpa-jiro-dishonor";

    /// <summary>The Broken Man's Earth Battle: an enemy Personality or Follower in play whose Force is lower than his own.</summary>
    public const string EnemyCardLowerForce = "wlpa-enemy-card-lower-force";
    public const string MutsukenSpec = "wlpa-mutsuken-active-bows";
    public const string LionAncestorProxy = "oracle-l5r-9785";

    /// <summary>Takada's and The Broken Man's Invest are traits (no "Reaction:" designator): a rule that bars Reactions does not touch them (Mendon's R4 ruling; TraitReactions.Is asks this).</summary>
    public static bool IsTrait(string abilityId) => abilityId is TakadaInvest or BrokenManInvest;

    public static Dictionary<string, AbilityDefinition> AddWlPersAftermath(this Dictionary<string, AbilityDefinition> all)
    {
        all[UeharaTerrain] = new("Limited", "Target a Terrain in your discard pile: Put it into your hand. Discard a card", "own-card-in-discard");
        // One printed choice (+2 or -2), two abilities that share one use; both are the same Siege Battle Ranged 3 Attack.
        all[DijuroRaise] = new("Battle", "Siege Battle, Bow this card: Ranged 3 Attack. If this destroyed a card, give this Province +2 strength", "attack",
            ActionKeywords: ["Siege"], UsageKey: "wlpa-dijuro", Costs: ActionCost.BowSource);
        all[DijuroLower] = new("Battle", "Siege Battle, Bow this card: Ranged 3 Attack. If this destroyed a card, give this Province -2 strength", "attack",
            ActionKeywords: ["Siege"], UsageKey: "wlpa-dijuro", Costs: ActionCost.BowSource);
        all[WateruAdditional] = new("Reaction", "Ninja Interrupt: After you Recruit Wateru during battle, take an additional Battle action", "none", Trigger: "own-enters-play", ActionKeywords: ["Ninja"]);
        all[TametakaTokens] = new("Battle", "Give a target enemy Personality three -1F tokens", "enemy-personality");
        all[ChiboshiEquip] = new("Battle", "Target a Follower in your hand: Equip it to Chiboshi, paying all costs. You may take an additional Battle action", "attachment-in-discard",
            Cost: GoldCostRule.AttachCost(), Attach: new(AttachSource.Target, AttachPile.OwnHand, AttachWielder.Source, [CardType.Follower]));
        all[EndoRecruit] = new("Limited", "If Endo is dead: Recruit him, paying all costs", "source-if-dead", FromDead: true, Cost: GoldCostRule.TargetRecruit());
        all[MutsukenBow] = new("Open", "Bow this card: The active player must choose and bow one of his unbowed Personalities", "none", Costs: ActionCost.BowSource);
        all[IsidorosStraighten] = new("Battle", "Straighten your target Follower", "own-follower");
        all[TamaoCreate] = new("Reaction", "Interrupt: After you Recruit Tamao, create a 2F/2C/3PH Lion Clan · Ancestor · Samurai · Spirit Personality", "none", Trigger: "own-enters-play",
            Creates: new(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3, Keywords: ["Lion Clan", "Samurai", "Ancestor", "Spirit"], ProxyId: LionAncestorProxy));
        all[HeiraiBoost] = new("Battle", "If Heirai is opposed, discard a card to give him a Force bonus equal to the card's Focus Value", "hand", Costs: ActionCost.DiscardTarget);
        all[SujikaroMelee] = new("Battle", "Bow this card: Melee 3 Attack", "attack", Costs: ActionCost.BowSource);
        all[TakadaInvest] = new("Reaction", "Invest 3 Gold: Discard a card to gain Honor equal to its Focus Value", "hand", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(3), Costs: ActionCost.DiscardTarget);
        all[BrokenManInvest] = new("Reaction", "Invest 5 Gold: Give The Broken Man two +1F tokens", "none", Trigger: "own-enters-play", Cost: GoldCostRule.Fixed(5));
        all[BrokenManBow] = new("Battle", "Earth Battle: Bow a target enemy card with lower Force", EnemyCardLowerForce);
        all[ShusakuBoost] = new("Battle", "Repeatable, 2 Gold: Give Shusaku +1F", "none", Cost: GoldCostRule.Fixed(2), UsesPerTurn: AbilityUseLimit.Unlimited);
        all[UsuiProvince] = new("Battle", "Earth Battle: Give this Province +2 strength", "none");
        all[JiroDishonor] = new("Limited", "Political Limited, 3 Gold: Dishonor a target Personality", "personality", ActionKeywords: ["Political"], Cost: GoldCostRule.Fixed(3));
        return all;
    }
}

public sealed partial class MatchEngine
{
    // ---- target legality ----

    private bool WlPersAftermathTargetAllows(string category, PlayerState player, CardInstance? source, CardInstance target)
    {
        if (category != WlPersAftermathAbilities.EnemyCardLowerForce) return false;
        // "an enemy card with lower Force": a Personality or Follower of another player whose Force is lower than The Broken Man's own.
        return source is not null && target.OwnerId != player.Id && Definition(target).Type is CardType.Personality or CardType.Follower && CardForce(target) < CardForce(source);
    }

    // ---- validation ----

    private void ValidateWlPersAftermath(string id, GameCommand command, PlayerState player, CardInstance? source)
    {
        switch (id)
        {
            case WlPersAftermathAbilities.UeharaTerrain:
                // "a target Terrain from your discard pile": a Strategy with the Terrain keyword in the acting player's Fate discard pile (own-card-in-discard names any own discard card).
                Require(command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var terrain) && terrain.OwnerId == player.Id && terrain.Zone == Zone.FateDiscard
                    && Definition(terrain).Type == CardType.Strategy && Keyword(terrain, "Terrain"), "Choose a Terrain in your discard pile.");
                break;
            case WlPersAftermathAbilities.WateruAdditional:
                Require(_state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null, "Wateru's additional Battle action needs a battle: he must be recruited during one.");
                break;
            case WlPersAftermathAbilities.HeiraiBoost:
                Require(source is not null && Opposed(source), "Heirai is not opposed.");
                break;
            case WlPersAftermathAbilities.UsuiProvince:
                Require(CurrentBattlefieldProvince() is not null, "There is no province at the current battlefield.");
                break;
        }
    }

    // ---- effects ----

    private bool? RunWlPersAftermath(RunHookContext c)
    {
        var (id, command, player, source, target) = (c.id, c.command, c.player, c.source, c.target);
        switch (id)
        {
            case WlPersAftermathAbilities.UeharaTerrain:
                ReturnToHand(target!);
                // "Discard a card." is the effect's second sentence: the queued forced discard is asked as soon as the action has resolved (an empty hand owes nothing).
                _state.PendingForcedDiscards.Add(new(player.Id, "Akodo Uehara"));
                return false;
            case WlPersAftermathAbilities.DijuroRaise or WlPersAftermathAbilities.DijuroLower or WlPersAftermathAbilities.SujikaroMelee:
                return ResolveOrDeferAttack(id, player, target?.Id, ActionPerformer(c.ability, command, source).Id, c.consumedGrant);
            case WlPersAftermathAbilities.WateruAdditional:
                GrantAdditionalAction(player.Id, null);
                return false;
            case WlPersAftermathAbilities.TametakaTokens:
                for (var n = 0; n < 3; n++) CreateToken(target!, "Force", id, "Force", -1);
                return false;
            case WlPersAftermathAbilities.ChiboshiEquip:
                GrantAdditionalAction(player.Id, null);
                return ExecuteAttach(command, c.ability.Attach!, player, source, target);
            case WlPersAftermathAbilities.EndoRecruit:
                {
                    var quote = QuotePurchase(player.Id, source!.Id, command.ClanDiscount, command.ClanAllegiance, fromDeadPile: true);
                    Require(quote.Error is null, quote.Error ?? "");
                    return BringDeadPersonalityIntoPlay(player, source, "Daigotsu Endo", null);
                }
            case WlPersAftermathAbilities.MutsukenBow:
                return BeginDeciderChoice(WlPersAftermathAbilities.MutsukenSpec, command, c.consumedGrant, c.playedFromDiscard, c.opponentHonorBeforeBattleAction, c.bowedBeforeAction, c.preExistingGrants);
            case WlPersAftermathAbilities.IsidorosStraighten:
                Straighten(target!);
                return false;
            case WlPersAftermathAbilities.HeiraiBoost:
                // The discard was paid as the cost; the bonus is the discarded card's Focus Value (its definition is unchanged in the discard pile), until the turn ends.
                ApplyModifier(source!, source!.Id, id, "Force", FocusValueOf(target!));
                return false;
            case WlPersAftermathAbilities.TakadaInvest:
                GainHonor(player.Id, FocusValueOf(target!), "Susumu Takada");
                return false;
            case WlPersAftermathAbilities.BrokenManInvest:
                CreateToken(source!, "Invest", id, "Force", 1);
                CreateToken(source!, "Invest", id, "Force", 1);
                return false;
            case WlPersAftermathAbilities.BrokenManBow:
                if (!StalwartNegatesBow(target!, player.Id)) target!.Bowed = true;
                return false;
            case WlPersAftermathAbilities.ShusakuBoost:
                ApplyModifier(source!, source!.Id, id, "Force", 1);
                return false;
            case WlPersAftermathAbilities.UsuiProvince:
                GiveBattlefieldProvinceStrength(2, "Usui");
                return false;
            case WlPersAftermathAbilities.JiroDishonor:
                Dishonor(target!);
                return false;
            // Tamao's creation is not handled here: the default arm runs the ability's Creates (the published Lion Ancestor scan) inside the entry window.
            default:
                return null;
        }
    }

    // "Give this Province +N strength": the province at the current battlefield, the Defender's, until the turn ends (the rulebook default; Togashi Hizumi's and Siege Volley's shape).
    private void GiveBattlefieldProvinceStrength(int amount, string source)
    {
        if (CurrentBattlefieldProvince() is not { } province) return;
        GiveProvinceStrengthThisTurn(1 - _state.ActivePlayerId, province.Index, amount, source);
    }

    // Dijuro: "If this destroyed a card, give this Province +2 or -2 strength." Called once from the single funnel every Attack resolves through: the target was in play when the Attack
    // resolved and is not now (Bayushi Higaonna's reading of "destroyed a card"). The raise or the lower is the ability the player took.
    private void AfterWlPersAftermathAttack(string abilityId, string? targetId, bool targetInPlayBefore)
    {
        if (abilityId is not (WlPersAftermathAbilities.DijuroRaise or WlPersAftermathAbilities.DijuroLower) || targetId is null || !targetInPlayBefore || InPlay(Card(targetId))) return;
        GiveBattlefieldProvinceStrength(abilityId == WlPersAftermathAbilities.DijuroRaise ? 2 : -2, "Bayushi Dijuro");
    }

    private int? WlPersAftermathAttackStrength(string abilityId) => abilityId switch
    {
        WlPersAftermathAbilities.DijuroRaise or WlPersAftermathAbilities.DijuroLower => 3,
        WlPersAftermathAbilities.SujikaroMelee => 3,
        _ => null
    };

    private static string? WlPersAftermathAttackKind(string abilityId) => abilityId switch
    {
        WlPersAftermathAbilities.DijuroRaise or WlPersAftermathAbilities.DijuroLower => "Ranged",
        WlPersAftermathAbilities.SujikaroMelee => "Melee",
        _ => null
    };

    // ---- Ide Mutsuken's decision: the active player chooses and bows one of his own unbowed Personalities ----

    private (List<string> Candidates, int Min, int Max) WlPersAftermathMutsukenOptions(int deciderId) =>
        (_state.Cards.Values.Where(c => c.OwnerId == deciderId && InPlay(c) && !c.Bowed && Definition(c).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToList(), 1, 1);

    private bool ApplyWlPersAftermathMutsuken(PendingDeciderChoice p)
    {
        if (p.SpecId != WlPersAftermathAbilities.MutsukenSpec) return false;
        foreach (var id in p.Chosen)
        {
            var card = Card(id);
            if (InPlay(card) && !StalwartNegatesBow(card, p.ActingPlayerId)) card.Bowed = true;
        }
        return true;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWlPersAftermathHooks()
    {
        EngineHooks.Abilities.Register("AddWlPersAftermath", 14500, static abilities => abilities.AddWlPersAftermath());
        EngineHooks.RunEffects.Register("RunWlPersAftermath", 14500, static (engine, context) => engine.RunWlPersAftermath(context));
        EngineHooks.TargetAllows.Register("WlPersAftermathTargetAllows", 14500, static (engine, context) => engine.WlPersAftermathTargetAllows(context.ability.Target, context.player, context.source, context.target));
        EngineHooks.ValidateMiddle.Register("ValidateWlPersAftermath", 14500, static (engine, context) => engine.ValidateWlPersAftermath(context.id, context.command, context.player, context.source));
        EngineHooks.AttackStrengthBefore.Register("WlPersAftermathAttackStrength", 14500, static (engine, context) => engine.WlPersAftermathAttackStrength(context.abilityId));
        EngineHooks.AttackKind.Register("WlPersAftermathAttackKind", 14500, static context => WlPersAftermathAttackKind(context.abilityId));
        EngineHooks.BowsTarget.Register("WlPersAftermathBowsTarget", 14500, static context => context.abilityId == WlPersAftermathAbilities.BrokenManBow);
        EngineHooks.DeciderPrompt.Register("WlPersAftermathDeciderPrompt", 14500, static (engine, p) => p.SpecId == WlPersAftermathAbilities.MutsukenSpec
            ? $"{Definition(engine.Card(p.SourceCardId)).Name}: {engine.Player(p.DeciderPlayerId).Name}, choose one of your unbowed Personalities. It is bowed." : null);
        EngineHooks.DeciderOptions.Register("WlPersAftermathDeciderOptions", 14500, static (engine, specId, deciderId, command) => specId == WlPersAftermathAbilities.MutsukenSpec ? engine.WlPersAftermathMutsukenOptions(deciderId) : null);
        EngineHooks.ApplyDecider.Register("ApplyWlPersAftermathDecider", 14500, static (engine, p) => engine.ApplyWlPersAftermathMutsuken(p));
        EngineHooks.DeciderVerb.Register("WlPersAftermathDeciderVerb", 14500, static (engine, specId) => specId == WlPersAftermathAbilities.MutsukenSpec ? "Bow" : null);
    }
}
