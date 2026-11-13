namespace L5R.Core;

/// <summary>Creators and user-supplied created-card scans reviewed for issue #503.</summary>
public static class RequestedCreatedCardAbilities
{
    public static Dictionary<string, AbilityDefinition> AddRequestedCreatedCards(this Dictionary<string, AbilityDefinition> all)
    {
        all["fruitless-search-create"] = new("Reaction", "After engaging, if any enemy units are at this battlefield: Create an Emerald Magistrate on your side there until the battle ends", "none",
            Trigger: "after-engaging", Creates: new(CardType.Personality, Force: 4, Chi: 4, PersonalHonor: 3,
                Keywords: ["Imperial", "Samurai", "Duelist", "Magistrate"], ProxyId: "oracle-l5r-9996", AtBattlefield: true, UntilBattleEnds: true));
        all["grateful-reward-create"] = new("Reaction", "After engaging, choose your performing Samurai here: Create a Mounted Ashigaru Follower and attach it to him; if he is a Paragon, gain 2 Honor and draw a card", "none",
            Trigger: "after-engaging", Performer: "own-personality-with-keyword-at-battle", RequiredKeywords: ["Samurai"], // no "unbowed" printed (R-035, issue #1228)
            Creates: new(CardType.Follower, Force: 1, Keywords: ["Ashigaru", "Cavalry"], ProxyId: "oracle-l5r-10010") { HostIsPerformer = true });
        all["sword-for-hire-create"] = new("Limited", "Target another player's Personality: Create an Assassin Duelist, who duels the target; destroy the loser and remove the Assassin after the duel", "enemy-personality-at-any-location",
            ActionKeywords: ["Iaijutsu"], IssuesDuel: true, Creates: new(CardType.Personality, Force: 0, Chi: 3, PersonalHonor: 0,
                Keywords: ["Assassin", "Duelist"], ProxyId: "oracle-l5r-9995"));
        all["kakita-saki-create-armor"] = new("Limited", "Target your Personality: Create Kakita Armor and attach it to him", "own-personality",
            Creates: new(CardType.Item, Force: 2, Chi: 0, Keywords: ["Armor"], ProxyId: "user-scan-kakita-armor"));
        all["kichi-create-badger"] = new("Reaction", "After Kichi enters play: Create Kichi's Badger", "none", Trigger: "own-enters-play",
            Creates: new(CardType.Personality, Force: 3, Chi: 2, PersonalHonor: 3,
                Keywords: ["Mantis Clan", "Nonhuman", "Badger", "Spirit"], ProxyId: "user-scan-kichis-badger"));
        all["kichis-badger-reduce"] = new("Battle", "Target an enemy Personality: Reduce his Force to 0", "enemy-personality");
        all["vanquishers-blades-create"] = new("Reaction", "After this card enters play: Create Readied Blade and attach it to this Personality if legal", "none", Trigger: "own-enters-play",
            Creates: new CreatedCardSpec(CardType.Item, Force: 2, Chi: 1, Keywords: ["Weapon"], ProxyId: "oracle-l5r-9749") { HostIsWielder = true, HostOptional = true });
        all["our-ancestors-call-create"] = new("Battle", "If this Shugenja is opposed, destroy this card: Create Ancestor Spirit at the battlefield, or two if this Shugenja is Water and defending", "none",
            Performer: "own-wielder", Costs: [new(CostVerbs.Sacrifice)],
            Creates: new CreatedCardSpec(CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3,
                Keywords: ["Samurai", "Ancestor", "Spirit"], ProxyId: "user-scan-ancestor-spirit", AtBattlefield: true)
            { ControllerClan = true });
        all["yari-of-air-create"] = new("Limited", "Once per game: Create an Air Yari and attach it to one of your Personalities", "own-personality", OncePerGame: true,
            Creates: new(CardType.Item, Force: 1, Chi: 1, Keywords: ["Air", "Weapon"], ProxyId: "user-scan-air-yari"));
        all["air-yari-penalty"] = new("Battle", "Give each Personality and Follower in the enemy army -1F", "none");
        all["crippled-sensei-focus"] = new("FocusEffect", "After this duel ends, choose a player who loses 2 Honor", "none");
        foreach (var (id, trigger) in new[] {
            ("crippled-sensei-create-action", AfterActionReactionTriggers.AfterActionResolved),
            ("crippled-sensei-create-battle", MatchEngine.AfterResolutionTrigger) })
            all[id] = new("Reaction", "After one of your Personalities is destroyed: Create Maimed Retainer", "none",
                Trigger: trigger, UsageKey: "crippled-sensei-create",
                Creates: new(CardType.Holding, Keywords: ["Maimed", "Retainer"], ProxyId: "user-scan-maimed-retainer"));
        all["maimed-retainer-restore"] = new("BattleOrOpen", "Destroy this card and target a Personality: Straighten and rehonor him", "personality",
            Costs: [new(CostVerbs.Sacrifice)]);
        all["natural-aviary-create"] = new("Limited", "Pay 2 Gold and target your Personality: Create an Aviary Falcon and attach it to him", "own-personality",
            Cost: GoldCostRule.Fixed(2), Creates: new(CardType.Follower, Force: 1,
                Keywords: ["Nonhuman", "Falcon", "Cavalry"], ProxyId: "user-scan-aviary-falcon"));
        return all;
    }
}

public sealed partial class MatchEngine
{
    private string CreateSwordForHireDuelist(AbilityDefinition ability, PlayerState player)
    {
        var assassin = CreatedCards.Resolve(ability.Creates!);
        // The duel is the rest of this same action; keep its entry's mandatory traits
        // without opening a nested entry window over the pending duel.
        CreatePersonality(player.Id, assassin, true, null, out var created);
        return created.Id;
    }

    private void FinishSwordForHireDuel(string abilityId, string challengerId)
    {
        if (abilityId == "sword-for-hire-create" && _state.Cards.TryGetValue(challengerId, out var assassin)
            && assassin.Zone != Zone.RemovedFromGame)
            RemoveFromGame(assassin);
    }

    private void ApplyRequestedCreatedCardsEntryEffects(int playerId, CardInstance card)
    {
        if (Definition(card).Effect == "yari-of-air-entry" && card.AttachedTo is { } wielderId && Keyword(Card(wielderId), "Air"))
            GainHonor(playerId, 1, "Yari of Air", card);
    }

    private void ValidateRequestedCreatedCard(string id, GameCommand command, PlayerState player)
    {
        if (id == "fruitless-search-create")
            Require(_state.CurrentBattleProvince is int battle && Army(1 - player.Id, battle).Any(),
                "Fruitless Search requires enemy units at the current battlefield.");
        if (id == "our-ancestors-call-create")
            Require(command.PerformerId is { } performerId && Opposed(Card(performerId)),
                "Our Ancestor's Call requires an opposed Shugenja.");
    }

    private bool? RunRequestedCreatedCards(string id, AbilityDefinition ability, GameCommand command,
        PlayerState player, CardInstance? source, CardInstance? target)
    {
        switch (id)
        {
            case "grateful-reward-create":
                var samurai = Card(command.PerformerId!);
                return ExecuteCreate(ability, ability.Creates!, command, player, () =>
                {
                    if (Keyword(samurai, "Paragon")) { GainHonor(player.Id, 2, "Grateful Reward", source); Draw(player); }
                });
            case "kichis-badger-reduce":
                ApplyModifier(target!, source!.Id, id, "Force", -CardForce(target!));
                return false;
            case "our-ancestors-call-create":
                var shugenja = Card(command.PerformerId!);
                var count = Keyword(shugenja, "Water") && !IsAttacking(shugenja) ? 2 : 1;
                return ExecuteCreate(ability, ability.Creates! with { Count = count }, command, player);
            case "air-yari-penalty":
                foreach (var enemy in Army(1 - player.Id, _state.CurrentBattleProvince!.Value))
                    foreach (var card in UnitMembers(enemy).Where(c => Definition(c).Type is CardType.Personality or CardType.Follower))
                        ApplyModifier(card, source!.Id, id, "Force", -1);
                return false;
            case "maimed-retainer-restore":
                Straighten(target!); Rehonor(target!);
                return false;
        }
        return null;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before catalogue construction.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterRequestedCreatedCardsHooks()
    {
        EngineHooks.Abilities.Register("AddRequestedCreatedCards", 10010, static abilities => abilities.AddRequestedCreatedCards());
        EngineHooks.ValidateEarly.Register("ValidateRequestedCreatedCard", 10010, static (engine, context) =>
            engine.ValidateRequestedCreatedCard(context.id, context.command, context.player));
        EngineHooks.RunEffects.Register("RunRequestedCreatedCards", 10010, static (engine, context) =>
            engine.RunRequestedCreatedCards(context.id, context.ability, context.command, context.player, context.source, context.target));
    }
}
