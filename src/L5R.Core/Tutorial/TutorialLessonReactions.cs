namespace L5R.Core;

/// <summary>The Reactions lesson (curriculum id <c>reactions</c>), from Mendon's script: Mantis (you, Kyuden Kitsune, 8 Honor) against Unicorn (Plains of the Maiden, 13 Honor), in your Attack phase.
/// You attack Province 1 with Tsuruchi Tomaru and Yoritomo Nakoshi; the Unicorn defend with Utaku Ryoko and Utaku Lishan, and Ryoko's Chrysanthemum Blossom reacts to her assignment. At engagement you announce
/// Surrounded!, Khalimpeh-jiak's Engage and Naval Invasion, the Unicorn announce Ryoko's Reaction, and you order them (R-039): Khalimpeh-jiak's target triggers Armor of the Heavens inside the window. Naval Invasion's
/// first Battle action, Strategic Strike performed by Nakoshi (Ranged 6 + 2), destroys Ryoko; the Unicorn's Surety of Purpose bows Tomaru and Lishan reacts to its announcement; out of actions, you lose the battle.
/// Corrections to the script are listed in docs/TUTORIAL.md. TutorialReactionsTests checks every number the words name.</summary>
internal static class TutorialLessonReactions
{
    internal const string Naval = "naval-invasion", SurroundedRecon = "p2-surrounded-recon", Khalimpeh = "wl-khalimpeh-penalty", RyokoPenalty = "ryoko-penalty", LishanPenalty = "lishan-penalty",
        ArmorGain = "armor-heavens-gain", Blossom = "chrysanthemum-blossom-boost", Strike = "strategic-strike-attack", Surety = "surety-of-purpose";

    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;
    private static Func<TutorialLookup, GameCommand, bool> Order(string ability) => (look, command) => command.Type == "reactionOrder" && look.Announced(command.TargetId) == ability;
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.Type == "useAbility" && command.AbilityId == ability;
    private static Func<TutorialLookup, GameCommand, bool> Announce(string ability) => (_, command) => command.Type == "reactionAnnounce" && command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
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

    public static readonly TutorialStep[] Steps =
    [
        Note("rx-welcome", "Reactions", "Welcome to the tutorial on Reactions.\n\nIn this tutorial you'll learn what triggers a Reaction, when a Reaction window opens, how several Reactions are put in order, and how they resolve.",
            then: c => { c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); }, glow: [TutorialGlow.PhaseBar]),
        Do("rx-declare", "Your attack", "We're going to learn all about Reactions from a single Attack phase: yours!", "Click the red Declare an attack button.", Move("declareAttack"), glow: Battle),
        Do("rx-assign", "Attack Province 1", "Assign everyone to attack the Unicorn player's Province 1.",
            "Drag Tsuruchi Tomaru and Yoritomo Nakoshi to the Unicorn player's Province 1 (or click each, then the Province), then click Confirm assignments.",
            (_, command) => command.Type == "attack" && (command.Assignments is { Count: > 0 } a ? a.Values.All(p => p == 0) : command.Province == 0),
            accept: (look, command) => command.Assignments is { Count: 2 } a && a.Keys.Any(id => look.Is(id, TutorialCards.TsuruchiTomaru)) && a.Keys.Any(id => look.Is(id, TutorialCards.YoritomoNakoshi)),
            hint: "Attack the Unicorn's Province 1 with Tsuruchi Tomaru and Yoritomo Nakoshi.", highlight: All(Own(TutorialCards.TsuruchiTomaru, TutorialCards.YoritomoNakoshi), FoeProvinces(0)), glow: Battle, onlyProvinces: [0]),
        Note("rx-triggers", "Triggers and windows", "Every Reaction says when it may be taken: its trigger, written as \"before\", \"after\" or \"when\" something happens. When that something happens, a Reaction window opens: an opportunity for each player holding a Reaction to that moment to take it.\n\nMany Reactions answer an action being announced or resolving, but others answer moments in the game: a card being assigned, a battle being engaged, a card being targeted."),
        Note("rx-engine", "Costs first", "One difference you may notice if you know the printed rules: in this game you pay an action's costs and make its choices before the action is announced, while the Comprehensive Rules announce it first. Either way, players get to react to it."),
        Note("rx-blossom", "After assigning", "The Unicorn assigned Utaku Ryoko and Utaku Lishan to defend, and Ryoko's Chrysanthemum Blossom reacted: \"Reaction: After this Personality is assigned: Give him a +1F token. Gain 1 Honor.\"\n\nIts trigger is being assigned, so it happened right away, before any battle was chosen. Ryoko has a +1F token, and the Unicorn are on 14 Honor.",
            highlight: Foe(TutorialCards.UtakuRyoko, TutorialCards.ChrysanthemumBlossom), showDefinition: TutorialCards.ChrysanthemumBlossom),
        Note("rx-engaging", "Engaging", "Battles have a special moment of their own. When you choose a battle to fight, it begins: that moment is called engaging. The battle's first round is only for Engage abilities and Reactions to \"after engaging\", with the player whose turn it is going first. Battle actions come after that.",
            glow: Battle),
        Do("rx-engage", "Engage", "Choose the battle at the Unicorn's Province 1.", "Click Fight at province 1.", (_, command) => command.Type == "selectBattle" && command.Province == 0,
            hint: "Fight at the Unicorn's Province 1.", highlight: FoeProvinces(0), glow: Battle, onlyProvinces: [0]),
        Note("rx-ryoko", "An after-engaging Reaction", "Utaku Ryoko reads: \"Reaction: After engaging at Ryoko's battlefield, target a Personality or Follower opposing her: Give it a Force penalty equal to Ryoko's Personal Honor.\"\n\nIts trigger is engaging, and only at her own battlefield. A Reaction may be taken from anywhere unless its text says otherwise, as Ryoko's does.",
            highlight: Foe(TutorialCards.UtakuRyoko), showDefinition: TutorialCards.UtakuRyoko, glow: Battle),
        Note("rx-announce", "Announce, then order", "Both of you hold Reactions to this moment. In this game, when both players can react, the player whose turn it is announces every Reaction they'll play, then the other player does, and then the player whose turn it is chooses the order they all resolve in.\n\nThe Comprehensive Rules do it differently: players take Reactions one at a time, starting with the player whose turn it is, and each resolves before the next is taken.",
            glow: Battle),
        Do("rx-announce-naval", "Naval Invasion", "Remember from the Keywords tutorial that every player has the Naval Invasion Reaction: \"After engaging, once per battle, if you are the Attacker and your current army has more Naval cards than the side opposing it, choose your performing Naval Personality: You have the first opportunity to take a Battle action, which he must perform.\"\n\nYou're the Attacker, and Yoritomo Nakoshi is your one Naval card against none. Announce Naval Invasion with Nakoshi.",
            "Click the Naval Invasion announcement for Yoritomo Nakoshi (\"Yoritomo Nakoshi · Announce: Naval Invasion ...\").", Announce(Naval), accept: (look, command) => look.Is(command.PerformerId, TutorialCards.YoritomoNakoshi),
            hint: "Announce Naval Invasion with Yoritomo Nakoshi.", highlight: Own(TutorialCards.YoritomoNakoshi), glow: Battle),
        Do("rx-announce-jiak", "An Engage ability", "Tsuruchi Tomaru's Khalimpeh-jiak reads: \"Engage, Bow this card: Give a target enemy Personality or Follower -2F.\" An Engage ability is taken at engagement, like a Reaction to \"after engaging\", and in this game it's announced and ordered with them. (The Comprehensive Rules call Engage a designator of its own.) It's on an attachment, so the card must be at the battle.\n\nAnnounce Khalimpeh-jiak.",
            "Click \"Announce: Engage, bow this card ...\".", Announce(Khalimpeh), hint: "Announce Khalimpeh-jiak.", highlight: Own(TutorialCards.Khalimpehjiak), showDefinition: TutorialCards.Khalimpehjiak, glow: Battle),
        Do("rx-announce-surrounded", "Surrounded!", "Surrounded! is a Strategy in your hand: \"Reaction: After engaging, choose your performing unbowed Scout at the current battlefield: You have Reconnaissance there. Ranged Attacks from your card effects during this battle have +2 strength.\"\n\nAnnounce it, performed by Tsuruchi Tomaru, a Scout.",
            "Click \"Tsuruchi Tomaru · Announce: After engaging ...\" (Surrounded!).", Announce(SurroundedRecon), accept: (look, command) => look.Is(command.PerformerId, TutorialCards.TsuruchiTomaru),
            hint: "Announce Surrounded! performed by Tsuruchi Tomaru.", highlight: All(Hand(TutorialCards.Surrounded), Own(TutorialCards.TsuruchiTomaru)), showDefinition: TutorialCards.Surrounded, glow: [TutorialGlow.Hand, TutorialGlow.BattleBar]),
        Do("rx-announce-done", "Three to play", "That's three things to play. Don't worry about the order yet: you'll choose it once both players are done announcing.", "Click Done announcing.", Move("reactionAnnounceDone"),
            hint: "Click Done announcing.", glow: Battle),
        Do("rx-order-surrounded", "Choosing the order", "The Unicorn announced Ryoko's Reaction. Now you choose the order all four resolve in. Order can matter: a Reaction that resolves first can change or even prevent one that comes later.\n\nPut Surrounded! first.",
            "Click Resolve Surrounded! next.", Order(SurroundedRecon), hint: "Put Surrounded! first.", glow: Battle),
        Do("rx-order-jiak", "Second", "Then Khalimpeh-jiak.", "Click Resolve Khalimpeh-jiak next.", Order(Khalimpeh), hint: "Put Khalimpeh-jiak next.", glow: Battle),
        Do("rx-order-naval", "Third", "Then Naval Invasion. Ryoko's Reaction is the only one left, so it resolves last.", "Click Resolve Naval Invasion next.", Order(Naval), hint: "Put Naval Invasion next.", glow: Battle),
        Do("rx-resolve-surrounded", "Resolving", "Now they resolve in that order. Once a Reaction starts, it resolves completely before the next one begins.\n\nPlay Surrounded!: you'll have Reconnaissance here, and your Ranged Attacks have +2 strength for the rest of this battle.",
            "Click Play Surrounded!, then click Play Reaction.", Ability(SurroundedRecon), hint: "Play Surrounded!.", highlight: Own(TutorialCards.TsuruchiTomaru), glow: Battle),
        Do("rx-resolve-jiak", "Khalimpeh-jiak", "Next, Khalimpeh-jiak: bow it to give Utaku Lishan -2F.", "Click Use Khalimpeh-jiak, choose Utaku Lishan, then click Play Reaction.", Ability(Khalimpeh),
            accept: (look, command) => look.Is(command.TargetId, TutorialCards.UtakuLishan), hint: "Use Khalimpeh-jiak on Utaku Lishan.", highlight: All(Own(TutorialCards.Khalimpehjiak), Foe(TutorialCards.UtakuLishan)),
            targets: Foe(TutorialCards.UtakuLishan), then: c => c.PassNestedWindows(), glow: Battle),
        Note("rx-armor", "A Reaction to a Reaction", "Targeting Lishan set off a Reaction of its own: her Armor of the Heavens, \"Political Reaction: After another player targets this Personality with an action: Choose a player, who gains or loses 2 Honor.\" The Unicorn chose themselves and are on 16 Honor.\n\nA Reaction can open a Reaction window of its own. That window is settled before the order you chose carries on. Lishan has -2F.",
            highlight: Foe(TutorialCards.UtakuLishan, TutorialCards.ArmorOfTheHeavens), showDefinition: TutorialCards.ArmorOfTheHeavens, glow: Battle),
        Do("rx-resolve-naval", "Naval Invasion", "Now Naval Invasion resolves, and then Ryoko's Reaction.", "Click \"Use ability · Yoritomo Nakoshi\", then click Use ability.", Ability(Naval), hint: "Play Naval Invasion.", highlight: Own(TutorialCards.YoritomoNakoshi), glow: Battle),
        Note("rx-ryoko-done", "All before the first Battle action", "Ryoko's Reaction resolved last: Tsuruchi Tomaru has a -3F penalty, Ryoko's Personal Honor.\n\nEvery Reaction in the window resolves before anyone takes a Battle action. So even though you're about to shoot Ryoko, her Reaction has already happened.",
            highlight: All(Foe(TutorialCards.UtakuRyoko), Own(TutorialCards.TsuruchiTomaru)), glow: Battle),
        Do("rx-strike", "The first Battle action", "Naval Invasion gives you the first Battle action, and Nakoshi must perform it. Strategic Strike reads: \"Battle: Bow your performing Scout Personality: Ranged 6 Attack.\" With Surrounded! it's a Ranged 8 Attack. Ryoko has 5 Force with her +1F token, so it destroys her.",
            "Click Strategic Strike in your hand (Nakoshi performs it), click Utaku Ryoko on the table, then click Confirm attack.", Ability(Strike),
            accept: (look, command) => look.Is(command.PerformerId, TutorialCards.YoritomoNakoshi) && look.Is(command.TargetId, TutorialCards.UtakuRyoko),
            hint: "Play Strategic Strike with Yoritomo Nakoshi on Utaku Ryoko.", highlight: All(Hand(TutorialCards.StrategicStrike), Own(TutorialCards.YoritomoNakoshi), Foe(TutorialCards.UtakuRyoko)),
            targets: Foe(TutorialCards.UtakuRyoko), glow: Battle,
            then: c => { c.PassNestedWindows(); c.FoeUse(Surety, performer: TutorialCards.UtakuLishan, card: TutorialCards.SuretyOfPurpose, target: TutorialCards.TsuruchiTomaru); c.PassNestedWindows(); }),
        Note("rx-surety", "The Unicorn answer", "The Unicorn played Surety of Purpose, performed by their Paragon, Lishan: \"Choose your performing Paragon and target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor.\" Tomaru's 2 Personal Honor isn't higher than Lishan's 3, so he's bowed, and the Unicorn are on 18 Honor.\n\nLishan also reacted to the Unicorn announcing that Battle action, \"After you announce a Battle action... Give it a Force penalty equal to Lishan's Personal Honor\", on Nakoshi.",
            highlight: Foe(TutorialCards.UtakuLishan), showDefinition: TutorialCards.SuretyOfPurpose, glow: Battle),
        Do("rx-pass", "Out of options", "Oh no. You've used every Reaction and Strategy you had, Nakoshi bowed for Strategic Strike, and Tomaru is bowed: a bowed Personality adds no Force, and his Ranged Attack can't be used. There's nothing left to do but pass.",
            "Click Pass battle action.", Move("pass"), hint: "Pass.", glow: Battle, then: c => c.Foe(s => s.Phase != Phase.Battle)),
        Note("rx-done", "You can't win them all", "The Unicorn won the battle. Your attackers were destroyed, and as the winning Defender the Unicorn gained 2 Honor for each card destroyed: Tomaru, his Khalimpeh-jiak and Nakoshi, 6 in all. Oh well. You can't win them all!\n\nYou've learned what triggers a Reaction, how a Reaction window opens, how announced Reactions are put in order, and how each resolves completely, sometimes setting off Reactions of its own. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    // Your Province cards are the script's; the Unicorn's are Holdings. Every Province stays face down.
    private static readonly string[] MantisDynasty = [TutorialCards.TsuruchiTomaruExp, TutorialCards.YoritomoHiromi, TutorialCards.TravelingPeddler, TutorialCards.ShelteredPort];
    private static readonly string[] UnicornDynasty = [TutorialCards.Stables, TutorialCards.SmallFarm, TutorialCards.SilkWorks, TutorialCards.Stables];

    /// <summary>You (Mantis, Kyuden Kitsune, 8 Honor): Kobune Port, Seaside Bazaar, Colonial Harbor, Border Keep and Bamboo Harvesters; Tsuruchi Tomaru with Khalimpeh-jiak and Yoritomo Nakoshi;
    /// Surrounded! and Strategic Strike in hand. The Unicorn (Plains of the Maiden, 13 Honor): Border Keep, Silk Works, Small Farm, Prosperous Village and Stables; Utaku Ryoko with a Chrysanthemum Blossom and
    /// Utaku Lishan with Armor of the Heavens; Surety of Purpose in hand.</summary>
    private static TutorialSetup Table => new(
        new("Mantis", TutorialCards.KyudenKitsune, MantisDynasty, [TutorialCards.Surrounded, TutorialCards.StrategicStrike],
            [new(TutorialCards.KobunePort), new(TutorialCards.SeasideBazaar), new(TutorialCards.ColonialHarbor), new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters),
             new(TutorialCards.TsuruchiTomaru, Attachments: [TutorialCards.Khalimpehjiak]), new(TutorialCards.YoritomoNakoshi)], Honor: 8,
            Deck: Filler("Mantis", TutorialCards.KyudenKitsune, MantisDynasty, [TutorialCards.StrategicStrike])),
        new("Unicorn", TutorialCards.UnicornStronghold, UnicornDynasty, [TutorialCards.SuretyOfPurpose],
            [new(TutorialCards.BorderKeep), new(TutorialCards.SilkWorks), new(TutorialCards.SmallFarm), new(TutorialCards.ProsperousVillage), new(TutorialCards.Stables),
             new(TutorialCards.UtakuRyoko, Attachments: [TutorialCards.ChrysanthemumBlossom]), new(TutorialCards.UtakuLishan, Attachments: [TutorialCards.ArmorOfTheHeavens])], Honor: 13,
            Deck: Filler("Unicorn", TutorialCards.UnicornStronghold, UnicornDynasty, [TutorialCards.SuretyOfPurpose])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void Opening(TutorialContext c)
    {
        c.Draw(2); c.FoeDraw(1);
        c.KeepFaceDown(TutorialRunner.Seat, 0, 1, 2, 3); c.KeepFaceDown(1 - TutorialRunner.Seat, 0, 1, 2, 3);
        c.StartTurn();
    }

    /// <summary>The Unicorn defend your attack, and always take their Reactions: Ryoko's Chrysanthemum Blossom, Ryoko's penalty on Tomaru, Lishan's Armor of the Heavens (gaining 2 Honor) and Lishan's penalty on Nakoshi.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [],
        Reactions: [(Blossom, null), (RyokoPenalty, TutorialCards.TsuruchiTomaru), (ArmorGain, null), (LishanPenalty, TutorialCards.YoritomoNakoshi)]);

    public static readonly TutorialLesson Lesson = new("reactions", "Reactions", Table, Steps, FoePlays, Opening: Opening);
}
