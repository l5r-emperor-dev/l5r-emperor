namespace L5R.Core;

/// <summary>The Cost and Effect lesson (curriculum id <c>cost-effect</c>), from Mendon's script: Scorpion (you, The Towers of the Yogo, 2 Honor) against Dragon (Monastery of New Thought, 8 Honor), from your Action phase
/// into your attack on the Dragon's Province 1. Mysterious Deaths (its 6 Gold is its only cost; it destroys your Ninja and Togashi Meiyu and costs you 4 Honor), The Merchant's District (a bow cost and a condition) draws
/// At Any Cost (requirements are not costs). In the battle Mirumoto Gobashi's Khalimpeh-jiak (-2F) and Unparalleled Sword (Melee 4) destroy Shosuro Koshiba; Revenge (no costs at all) bows and destroys the Sword,
/// Bayushi Kasumi's Towers ability bows Gobashi and At Any Cost recruits Koshiba back. Corrections to the script are listed in docs/TUTORIAL.md. TutorialCostEffectTests checks every number the words name.</summary>
internal static class TutorialLessonCostEffect
{
    internal const string Deaths = "mysterious-deaths-destroy", District = "market-draw-scorpion", AtAnyCost = "at-any-cost-recruit", Revenge = "revenge-bow", Towers = "towers-of-yogo-bow",
        Sword = "unparalleled-sword-attack", Khalimpeh = "wl-khalimpeh-penalty", Palace = "overlooked-palace-create";

    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.Type == "useAbility" && command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> Dead(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Dead, Zone.DynastyDiscard));
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indexes) => look => indexes.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? showDefinition = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = showDefinition };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null, Func<TutorialLookup, IEnumerable<string>>? targets = null,
        int[]? onlyProvinces = null, string? showDefinition = null) =>
        new()
        {
            Id = id,
            Title = title,
            Body = body,
            Instruction = instruction,
            Allow = allow,
            Accept = accept,
            RefusalHint = hint ?? "Do the move the lesson asks for.",
            Then = then,
            Highlight = highlight,
            Glow = glow,
            Targets = targets,
            OnlyProvinces = onlyProvinces,
            ShowDefinitionId = showDefinition
        };

    private static readonly string[] Battle = [TutorialGlow.BattleBar];
    private const int Foe0 = 1 - TutorialRunner.Seat;

    /// <summary>The Dragon take their turn at a Battle action (they pass), handing the next one back to you.</summary>
    private static void FoePasses(TutorialContext c) => c.Foe(s => s.PriorityPlayerId == TutorialRunner.Seat || s.Phase != Phase.Battle);

    public static readonly TutorialStep[] Steps =
    [
        Note("ce-welcome", "Costs and Effects", "Welcome to the tutorial on Costs and Effects.\n\nIn this tutorial you'll learn the difference between the costs of an action and its effects."),
        Note("ce-costs", "Costs", "Many actions ask you to pay a cost: bow a card, pay Gold, lose Honor, discard a card. A cost must be paid in full for the action's effects to happen, and you may not take an action whose costs you know you can't pay. A card that is already bowed, for example, can't pay \"Bow this card\".\n\nOne difference from the printed rules: in this game you pay an action's costs as you choose it, before it is announced. The Comprehensive Rules announce it first, then pay."),
        Note("ce-effects", "Effects", "Every action has at least one effect, and many have several. Effects happen in the order they're written, and each stands on its own: if one is negated or can't happen, the others still happen.\n\nThe exception is an effect that depends on an earlier one, such as \"You may bow one of your Rings to draw a card\": if the Ring isn't bowed, you don't draw."),
        Note("ce-colons", "Two colons", "An action begins with its designator, such as Limited, Open, Battle or Reaction, followed by a colon. If its text has a second colon, everything between the two colons is the constraints block, and everything after the second is the effects block.\n\nThe constraints block can hold a Reaction's trigger, conditions, targets, performers and costs. Only the parts that tell you to do something, such as bow, pay or lose Honor, are costs."),
        Note("ce-deaths-gold", "The Gold Cost", "Look at Mysterious Deaths. Its Gold Cost, in the coin, is 6.\n\nA Strategy's Gold Cost is a cost of its actions: you pay it when you play the card. On a Personality, Holding or attachment, the Gold Cost is what you pay to bring the card into play.",
            highlight: Hand(TutorialCards.MysteriousDeaths), showDefinition: TutorialCards.MysteriousDeaths, glow: [TutorialGlow.Hand]),
        Note("ce-deaths-colon", "No second colon", "Now read its text: \"Limited: Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all targets. Remove this card from the game. Lose 4 Honor.\"\n\nThere's no second colon, so there's no constraints block: everything after \"Limited:\" is an effect. Paying 6 Gold is its only cost. Losing 4 Honor is an effect, not a cost, so it doesn't stop you from playing the card, even on 2 Honor.",
            highlight: Hand(TutorialCards.MysteriousDeaths), showDefinition: TutorialCards.MysteriousDeaths, glow: [TutorialGlow.Hand]),
        Note("ce-deaths-effects", "Its effects", "Choosing targets is neither a cost nor an effect, so Mysterious Deaths has three effects:\n\n1. Destroy all the targets.\n2. Remove this card from the game.\n3. Lose 4 Honor.\n\nIf any of them is negated, the others still happen. Each player targets their own unit with the lowest total Gold Cost, attachments included, choosing on a tie; a player with no unit to target has nothing destroyed.\n\nYour lowest is the Ninja you made by destroying your Overlooked Palace: it costs 0. The Dragon's is Togashi Meiyu, at 5. Mirumoto Gobashi's unit totals 15 with his Unparalleled Sword and Khalimpeh-jiak.",
            highlight: All(Own(TutorialCards.Ninja), Foe(TutorialCards.TogashiMeiyu)), showDefinition: TutorialCards.MysteriousDeaths),
        Do("ce-deaths-play", "Play it", "Play Mysterious Deaths now to see its cost paid and its effects applied.\n\nPay its 6 Gold from your other Holdings and keep The Merchant's District unbowed: you'll want it in a moment.",
            "Click Mysterious Deaths in your hand, click cards that make 6 Gold, such as Prosperous Village and the Border Keep (not The Merchant's District), then click Play Mysterious Deaths.", Ability(Deaths),
            accept: (look, command) => !(command.SourceIds ?? []).Any(id => look.Is(id, TutorialCards.MerchantsDistrict)),
            hint: "Play Mysterious Deaths without bowing The Merchant's District.", highlight: Hand(TutorialCards.MysteriousDeaths), glow: [TutorialGlow.Hand],
            then: c => c.PassNestedWindows()),
        Note("ce-deaths-done", "Paid, then applied", "You paid 6 Gold, and then its effects happened in order: your Ninja and Togashi Meiyu were destroyed, Mysterious Deaths was removed from the game, and you lost 4 Honor, from 2 to -2.\n\nGold you produce beyond a cost is lost; it doesn't carry over to the next payment.",
            glow: [TutorialGlow.Honor]),
        Do("ce-district", "The Merchant's District", "Personalities and Strategies aren't the only cards with actions. The Merchant's District has both blocks: \"Limited: If you have four or fewer cards in your hand and you are a Scorpion Clan player, bow this card: Draw a card.\"\n\nIts constraints block holds two things:\n• A condition: four or fewer cards in your hand, and you're a Scorpion Clan player. You pay nothing for it, but it must be true or you can't take the action. You hold one card, and you're Scorpion.\n• A cost: bow this card.\n\nIts effects block: draw a card. Use it.",
            "Click The Merchant's District, then click Use The Merchant's District.", Ability(District), hint: "Use The Merchant's District to draw a card.", highlight: Own(TutorialCards.MerchantsDistrict),
            showDefinition: TutorialCards.MerchantsDistrict),
        Note("ce-any-cost", "Requirements aren't costs", "You drew At Any Cost: \"Battle: If you control any units at the current battlefield, choose your performing dead Samurai: Bring him into play at the current battlefield, paying all costs.\"\n\nIts constraints block asks two things of you, and neither is a cost:\n\n• A condition: you control a unit at the battle.\n• A performer: you must have a dead Samurai to choose.\n\nYou don't pay for either, but if either is missing you can't play At Any Cost. Its Gold Cost is 0, so the action itself costs nothing. Its effect brings the Samurai into play \"paying all costs\": his own Gold Cost is the cost of bringing him into play, and you must be able to pay it to play At Any Cost at all.",
            highlight: Hand(TutorialCards.AtAnyCost), showDefinition: TutorialCards.AtAnyCost, glow: [TutorialGlow.Hand],
            then: c => { c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); }),
        Do("ce-declare", "Your attack", "You have no dead Samurai yet, so At Any Cost will have to wait. Let's attack.", "Click the red Declare an attack button.", Move("declareAttack"), glow: Battle),
        Do("ce-assign", "Attack Province 1", "Attack the Dragon's Province 1 with everyone.",
            "Drag Shosuro Koshiba and Bayushi Kasumi to the Dragon's Province 1 (or click each, then the Province), then click Confirm assignments.",
            (_, command) => command.Type == "attack" && (command.Assignments is { Count: > 0 } a ? a.Values.All(p => p == 0) : command.Province == 0),
            accept: (look, command) => command.Assignments is { Count: 2 } a && a.Keys.Any(id => look.Is(id, TutorialCards.ShosuroKoshiba)) && a.Keys.Any(id => look.Is(id, TutorialCards.BayushiKasumi)),
            hint: "Attack the Dragon's Province 1 with Shosuro Koshiba and Bayushi Kasumi.", highlight: All(Own(TutorialCards.ShosuroKoshiba, TutorialCards.BayushiKasumi), FoeProvinces(0)), glow: Battle, onlyProvinces: [0]),
        Do("ce-engage", "Gobashi defends", "Mirumoto Gobashi defends the Province. Choose the battle there.", "Click Fight at province 1.", (_, command) => command.Type == "selectBattle" && command.Province == 0,
            hint: "Fight at the Dragon's Province 1.", highlight: FoeProvinces(0), glow: Battle, onlyProvinces: [0], then: c => c.PassNestedWindows()),
        Note("ce-jiak", "An Engage ability", "As the battle began, Gobashi's Khalimpeh-jiak acted: \"Engage, Bow this card: Give a target enemy Personality or Follower -2F.\"\n\nEngage abilities, like Reactions and Battle, Open and Limited actions, can have costs and effects. Later cards print a bow symbol after the designator in place of the words \"Bow this card\": bowing Khalimpeh-jiak is the cost, and the -2F is the effect. The Dragon used it on Shosuro Koshiba: 3 Force, +3 for his Nightingale Blade, -2, makes 4.",
            highlight: All(Foe(TutorialCards.Khalimpehjiak), Own(TutorialCards.ShosuroKoshiba)), showDefinition: TutorialCards.Khalimpehjiak, glow: Battle),
        Note("ce-defender-first", "The Defender acts first", "Battle actions begin with the Defender. Let's see what the Dragon do.", glow: Battle,
            then: c => { c.FoeUse(Sword, performer: TutorialCards.MirumotoGobashi, target: TutorialCards.ShosuroKoshiba); c.PassNestedWindows(); }),
        Note("ce-sword", "Melee 4", "The Dragon used Gobashi's Unparalleled Sword, \"Battle: Melee 4 Attack\", on Koshiba. A Melee 4 Attack destroys a Personality with 4 Force or less, and Khalimpeh-jiak had brought him down to exactly 4. Koshiba and his Nightingale Blade are gone before he could use it.",
            highlight: Foe(TutorialCards.MirumotoGobashi, TutorialCards.UnparalleledSword), showDefinition: TutorialCards.UnparalleledSword, glow: Battle),
        Do("ce-revenge", "Revenge", "Answer with Revenge: \"Battle: Choose your performing unbowed Samurai and target an enemy card without attachments: Bow it. Destroy it if another player's action has destroyed one of your Personalities since the end of your last Battle action this battle.\"\n\nRevenge has a second colon, but nothing before it is a cost: choosing your performer and your target are requirements, and its Gold Cost is 0. Its effects are to bow the target, then destroy it, because the Dragon's action destroyed Koshiba and you haven't taken a Battle action this battle. Each stands on its own: if the bow were negated, the Sword would still be destroyed.\n\nPlay Revenge, performed by Bayushi Kasumi, on the Unparalleled Sword.",
            "Click Revenge in your hand (Bayushi Kasumi performs it), choose the Unparalleled Sword, then click Play Revenge.", Ability(Revenge),
            accept: (look, command) => look.Is(command.TargetId, TutorialCards.UnparalleledSword) && (command.PerformerId is null || look.Is(command.PerformerId, TutorialCards.BayushiKasumi)),
            hint: "Play Revenge with Bayushi Kasumi on the Unparalleled Sword.", highlight: All(Hand(TutorialCards.Revenge), Own(TutorialCards.BayushiKasumi), Foe(TutorialCards.UnparalleledSword)),
            targets: Foe(TutorialCards.UnparalleledSword), showDefinition: TutorialCards.Revenge, glow: Battle, then: c => { c.PassNestedWindows(); FoePasses(c); }),
        Do("ce-towers", "Kasumi's ability", "The Sword is destroyed and the Dragon passed. Your Stronghold, The Towers of the Yogo, gives each of your Scorpion Clan Personalities the ability \"Battle: Bow this card and target an enemy Follower or Personality without Followers: Bow it.\"\n\nCost: bow this card, Bayushi Kasumi.\nTarget: Mirumoto Gobashi, who has an Item but no Followers. A target isn't a cost.\nEffect: bow him.\n\nA bowed Personality adds no Force to his army. Kasumi is Elite, so she adds hers even while bowed. Use Kasumi's ability on Gobashi.",
            "Click Bayushi Kasumi, choose Mirumoto Gobashi, then click Use Bayushi Kasumi.", Ability(Towers), accept: (look, command) => look.Is(command.TargetId, TutorialCards.MirumotoGobashi),
            hint: "Use Bayushi Kasumi's Towers of the Yogo ability on Mirumoto Gobashi.", highlight: All(Own(TutorialCards.BayushiKasumi, TutorialCards.TowersOfTheYogo), Foe(TutorialCards.MirumotoGobashi)),
            targets: Foe(TutorialCards.MirumotoGobashi), glow: Battle, then: c => { c.PassNestedWindows(); FoePasses(c); }),
        Do("ce-any-cost-play", "At Any Cost", "Now you have a dead Samurai. Bring Shosuro Koshiba back with At Any Cost.\n\nCosts of the action: none.\nRequirements: you have a unit at the battle, Kasumi, and a dead Samurai, Koshiba, to perform it.\nEffect: Koshiba is brought into play here at the battle, as a new card without his Nightingale Blade.\nCost of bringing him into play: his Gold Cost, 5, or 3 with your Clan Discount, since he's Scorpion like you.\n\nPlay it with the Clan Discount.",
            "Click At Any Cost in your hand, choose Shosuro Koshiba, then Clan discount. Click cards that make 3 Gold, such as the Geisha House, then click Play At Any Cost.", Ability(AtAnyCost),
            accept: (look, command) => look.Is(command.TargetId, TutorialCards.ShosuroKoshiba) && command.ClanDiscount && !command.ClanAllegiance,
            hint: "Play At Any Cost on Shosuro Koshiba with the Clan Discount.", highlight: All(Hand(TutorialCards.AtAnyCost), Dead(TutorialCards.ShosuroKoshiba)), showDefinition: TutorialCards.AtAnyCost, glow: Battle,
            then: c => { c.PassNestedWindows(); FoePasses(c); }),
        Do("ce-resolve", "Resolve the battle", "Koshiba is back at the battle. Gobashi is bowed and adds no Force, so your army wins. The battle resolves once both players pass.", "Click Pass battle action.", Move("pass"), glow: Battle,
            then: c => c.Foe(s => s.Phase != Phase.Battle)),
        Note("ce-done", "Congratulations", "You won the battle: the losing army, Mirumoto Gobashi and his Khalimpeh-jiak, was destroyed, and you gained 2 Honor for each of those two cards, from -2 back to 2.\n\nYou've now learned the difference between a cost, something you must pay before an action's effects can happen, and an effect, what happens once it's paid. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    // Province cards: the Dragon's are the script's (Mirumoto Yumaru, Mirumoto Shikei, Traveling Peddler, Incense Mill); yours are Holdings, so no second Koshiba or Kasumi hides in them. Every Province stays face down.
    private static readonly string[] ScorpionDynasty = [TutorialCards.SmallFarm, TutorialCards.LargeFarm, TutorialCards.GeishaHouse, TutorialCards.SmallFarm];
    private static readonly string[] DragonDynasty = [TutorialCards.MirumotoYumaru, TutorialCards.MirumotoShikei, TutorialCards.TravelingPeddler, TutorialCards.IncenseMill];

    /// <summary>You (Scorpion, The Towers of the Yogo, 2 Honor): Border Keep, Bamboo Harvesters, Geisha House, Prosperous Village, The Merchant's District and Overlooked Palace; Shosuro Koshiba with a Nightingale Blade
    /// and Bayushi Kasumi; Mysterious Deaths and Revenge in hand, At Any Cost on top of the Fate deck. The Dragon (Monastery of New Thought, 8 Honor): Gold Mine, Small Farm, Large Farm and Border Keep; Mirumoto Gobashi
    /// with an Unparalleled Sword and Khalimpeh-jiak, and Togashi Meiyu; Superior Reach in hand.</summary>
    private static TutorialSetup Table => new(
        new("Scorpion", TutorialCards.TowersOfTheYogo, ScorpionDynasty, [TutorialCards.MysteriousDeaths, TutorialCards.Revenge, TutorialCards.AtAnyCost],
            [new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters), new(TutorialCards.GeishaHouse), new(TutorialCards.ProsperousVillage), new(TutorialCards.MerchantsDistrict), new(TutorialCards.OverlookedPalace),
             new(TutorialCards.ShosuroKoshiba, Attachments: [TutorialCards.NightingaleBlade]), new(TutorialCards.BayushiKasumi)], Honor: 2,
            Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, ScorpionDynasty, [TutorialCards.Revenge])),
        new("Dragon", TutorialCards.MonasteryOfNewThought, DragonDynasty, [TutorialCards.SuperiorReach],
            [new(TutorialCards.GoldMine), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.BorderKeep),
             new(TutorialCards.MirumotoGobashi, Attachments: [TutorialCards.UnparalleledSword, TutorialCards.Khalimpehjiak]), new(TutorialCards.TogashiMeiyu)], Honor: 8,
            Deck: Filler("Dragon", TutorialCards.MonasteryOfNewThought, DragonDynasty, [TutorialCards.SuperiorReach])),
        TurnsTaken: 3, NoEndDraw: true);

    /// <summary>Your hand of two and the Dragon's one are drawn, every Province stays face down, your turn starts, and the Overlooked Palace has already made your Ninja.</summary>
    private static void Opening(TutorialContext c)
    {
        c.Draw(2); c.FoeDraw(1);
        c.KeepFaceDown(TutorialRunner.Seat, 0, 1, 2, 3); c.KeepFaceDown(Foe0, 0, 1, 2, 3);
        c.StartTurn();
        c.Use(Palace);
    }

    /// <summary>The Dragon defend your attack with Gobashi and always use his Khalimpeh-jiak on Shosuro Koshiba; their Unparalleled Sword is a step's move, and everything else is a pass.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [], Reactions: [(Khalimpeh, TutorialCards.ShosuroKoshiba)]);

    public static readonly TutorialLesson Lesson = new("cost-effect", "Cost and Effect", Table, Steps, FoePlays, Opening: Opening);
}
