namespace L5R.Core;

/// <summary>The Keywords lesson (issue #1078, curriculum id <c>keywords</c>), the first of two. Keywords change the table a lot, so the lesson sets the table up three times: Crane (you) against Scorpion for the Clan and
/// Seppuku keywords; Spider (you) against Unicorn for Kensai, Cavalry, Naval, Tactician, Elite and Conqueror, played through one attack on Province 1; and Spider against a lone Stalwart Phoenix Personality.
/// The engine plays every rule: the script only chooses what is offered. Every number in the words is checked by TutorialKeywordsTests.</summary>
internal static class TutorialLessonKeywords
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? card = null) =>
        (look, command) => command.Type == type && (card is null || look.Is(command.CardId, card));
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? showDefinition = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = showDefinition };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null, Func<TutorialLookup, IEnumerable<string>>? targets = null,
        int[]? onlyProvinces = null) =>
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
            OnlyProvinces = onlyProvinces
        };

    private static readonly string[] Bar = [TutorialGlow.PhaseBar], Battle = [TutorialGlow.BattleBar];

    private static readonly string[] CranePersonalities = [TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi, TutorialCards.KakitaMaratai];
    private static readonly string[] SeppukuPersonalities = [TutorialCards.DojiDainagon, TutorialCards.AsahinaKonomi, TutorialCards.DaidojiKenshi];
    private static readonly string[] SpiderPersonalities = [TutorialCards.DaigotsuShiraume, TutorialCards.DaigotsuShikenuro, TutorialCards.DaigotsuHotako, TutorialCards.Ohaba];

    private static bool AttackedProvince(GameCommand command, int province) => command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == province) : command.Province == province;

    /// <summary>One of your three Personalities that may commit seppuku (not Kakita Maratai, who is none of Samurai, Courtier and Shugenja).</summary>
    private static TutorialStep Seppuku(string id, string title, string body, string instruction) =>
        Do(id, title, body, instruction, Move("orderSeppuku"), accept: (look, command) => SeppukuPersonalities.Any(d => look.Is(command.TargetId ?? command.CardId, d)),
            hint: "Choose one of the Personalities that may commit seppuku.", highlight: Own(SeppukuPersonalities), targets: Own(SeppukuPersonalities));

    public static readonly TutorialStep[] Steps =
    [
        // ---- Crane (you) against Scorpion: Clan and Seppuku --------------------------------------------------------------------------------------------------------------------------------------------
        Note("welcome", "Keywords", "Welcome to the tutorial on Keywords. It will teach you the difference between functional keywords and descriptive keywords, and how the functional keywords operate."),
        Note("what-are-keywords", "What a keyword is", "Keywords are the bold words at the top of a card's text box, above the dividing line. Many cards have them.\n\nSome keywords have a game rule attached to them: those are functional keywords. The rest are descriptive keywords. A descriptive keyword can still have a game effect, but it has no rules of its own: a card's ability has to act on it."),
        Note("functional-first", "Functional keywords", "We begin with the functional keywords, to show how they work."),
        Note("clan", "Clan", "The first keyword is a Clan. There are nine Clan alignments: Crab, Crane, Dragon, Lion, Mantis, Phoenix, Scorpion, Spider and Unicorn. Each Clan has several Strongholds, and your Stronghold gives you your Clan alignment.\n\nMany Personalities have a keyword for their Clan, such as Crane Clan. A Personality may have more than one Clan affiliation. Doji Dainagon is a Crane Clan Personality. The Imperial keyword is a conditional Clan alignment if a player is using Journey's End Keep (Experienced).",
            highlight: Own(TutorialCards.DojiDainagon)),
        Note("clan-benefits", "Clan alignment", "Mouse over each of your four Personalities: each has the Crane Clan keyword. When you recruit a Personality whose Clan alignment is the same as yours, you have three options:\n\n1. Pay 2 less Gold (Clan discount). You may not Proclaim him.\n2. Pay full price, and Proclaim him: gain Honor equal to his base Personal Honor.\n3. Pay 2 more Gold (Clan allegiance) to ignore his Honor Requirement.\n\nYou may use more than one of these features at a time.",
            highlight: Own(CranePersonalities)),
        Note("seppuku-intro", "Samurai, Courtier and Shugenja", "Your Personalities have other keywords, some of them functional. Samurai, Courtier and Shugenja are three of them. When a Personality with one of these keywords is dishonorable, you may order him to commit seppuku, ritual suicide, as an Open action.\n\nYour opponent is about to dishonor all four of your Personalities.",
            then: c => c.Dishonor(CranePersonalities), highlight: Own(CranePersonalities)),
        Seppuku("seppuku-1", "Seppuku", "Rotating Personalities so they appear upside down indicates that they are dishonorable.\n\nEach Personality is now dishonorable. Help save the Clan's reputation: order a Personality to commit seppuku. Kakita Maratai has no seppuku option, because she is none of a Samurai, a Courtier or a Shugenja.",
            "Click one of Doji Dainagon, Asahina Konomi and Daidoji Kenshi, then click Order seppuku."),
        Seppuku("seppuku-2", "Seppuku", "Seppuku is an Open ability that every player has, and it is repeatable. Order a second Personality to commit seppuku.", "Click another Personality, then click Order seppuku."),
        Seppuku("seppuku-3", "Seppuku", "And the last Personality that may.", "Click the last Personality, then click Order seppuku."),
        Note("seppuku-result", "Seppuku", "When a Personality commits seppuku, he is rehonored, then destroyed. After a dishonorable Personality is destroyed, his controller loses Honor equal to his base Personal Honor. Seppuku rehonors him first, so you lose nothing. Kakita Maratai is still dishonorable: if she were destroyed, you would lose her Honor.\n\nNow Asahina Konomi comes back to play, honorable, with a Spell attached.\n\n*Click on the Dead placard in the bottom left corner of your screen to view your dead Personalities and their status.",
            then: c => c.PutInPlay(TutorialCards.AsahinaKonomi, TutorialCards.ScentOfDishonor), highlight: Own(TutorialCards.KakitaMaratai)),
        Note("shugenja", "Shugenja", "Samurai and Courtier are otherwise descriptive keywords that many card effects rely on. A Shugenja has one more rule: Shugenja are the only Personalities that may attach Spells. Konomi is a Shugenja, and Scent of Dishonor is a Spell.",
            highlight: Own(TutorialCards.AsahinaKonomi, TutorialCards.ScentOfDishonor)),
        Note("air", "Descriptive keywords", "Asahina Konomi has the Air keyword. It is descriptive: the rules say nothing about Air except that it is an element keyword. Cards act on it. Scent of Dishonor's Open ability says \"Bow this Shugenja unless he is Air\", so an Air Shugenja like Konomi can use it without bowing.",
            then: c => c.Reset(SpiderTable, SpiderOpening), highlight: Own(TutorialCards.AsahinaKonomi, TutorialCards.ScentOfDishonor)),

        // ---- Spider (you) against Unicorn: one attack ----------------------------------------------------------------------------------------------------------------------------------------------
        Note("battle-keywords", "Battle keywords", "The table has changed: you are the Spider Clan, and the Unicorn are defending. Now for the keywords that matter in battle:\n\nKensai\nInfantry and Cavalry\nNaval\nTactician\nElite\nConqueror",
            highlight: All(Own(SpiderPersonalities), Foe(TutorialCards.MotoTaha, TutorialCards.BayushiKasumi))),
        Note("kensai", "Kensai", "Look at the Kensai, Ohaba. A Personality may attach only one Weapon at a time. A Kensai may attach two, and Ohaba carries Blade of Perfection and Hunger, both Weapons.\n\nHunger says you must declare an attack each turn and assign its Personality to attack if possible, so Ohaba is going to war.",
            highlight: Own(TutorialCards.Ohaba, TutorialCards.BladeOfPerfection, TutorialCards.Hunger)),
        Note("cavalry", "Infantry and Cavalry", "Next is Cavalry. A card with Cavalry assigns during the Cavalry Maneuvers if the Personality and every Follower in its unit also has the Cavalry keyword. Otherwise, it assigns during the Infantry Maneuvers. Notice that Infantry isn't a keyword. It is the default state of a card unless it has the Cavalry keyword.\n\nHorse Archers and Moto Taha both have Cavalry, so they'll assign during Cavalry Maneuvers and they make up a Cavalry Unit.",
            highlight: Foe(TutorialCards.MotoTaha, TutorialCards.HorseArchers)),
        Do("pass-to-attack", "Go to the Attack phase", "Both of you have what you need in hand. Pass to the Attack phase.", "Click Pass action.", Move("pass"), glow: Bar),
        Do("declare-attack", "Declare an attack", "Now declare the attack.", "Click the red Declare an attack button.", Move("declareAttack")),
        Do("assign-attackers", "Assign your attackers", "Assign all four of your Personalities to attack the Unicorn player's Province 1.",
            "Click each of your Personalities and then Province 1, or drag them to it, then click Confirm assignments.",
            (_, command) => command.Type == "attack" && AttackedProvince(command, 0),
            accept: (look, command) => SpiderPersonalities.All(d => look.Own(d, Zone.Home).Any(id => command.Assignments is { } a && a.TryGetValue(id, out var at) && at == 0)),
            hint: "Attack the Unicorn player's Province 1 with all four Personalities.", highlight: All(Own(SpiderPersonalities), FoeProvinces(0)), glow: Battle, onlyProvinces: [0]),
        Note("maneuvers", "Assignment order", "Units are assigned in this order:\n\n1. Infantry Maneuvers: the attacker assigns any of his units.\n2. Infantry Maneuvers: the defender assigns any of his units.\n3. Cavalry Maneuvers: the attacker assigns his Cavalry units.\n4. Cavalry Maneuvers: the defender assigns his Cavalry units.\n\nA Cavalry Unit may assign during either Infantry or Cavalry Maneuvers; an Infantry unit only during Infantry Maneuvers. Bayushi Kasumi has assigned to defend in Infantry Maneuvers, and Moto Taha now assigns to defend as the defender's Cavalry.",
            highlight: Foe(TutorialCards.BayushiKasumi, TutorialCards.MotoTaha), glow: Battle),
        Do("engage", "Engagement", "Select the battle at Province 1 to resolve it.", "Click Fight at province 1.", (_, command) => command is { Type: "selectBattle", Province: 0 },
            hint: "Resolve the battle at Province 1.", highlight: FoeProvinces(0), glow: Battle),
        Do("naval", "Naval", "Naval is the next keyword. After engaging, once per battle, if the attacker has more Naval cards in his army than the defender (Personalities and Followers both count), he may choose one of his Naval Personalities to take the first action in the battle. That Personality must perform it. Daigotsu Hotako has Naval and the Unicorn player has none.",
            "Click Naval Invasion in the sidebar, choose Daigotsu Hotako, then confirm.", Ability("naval-invasion"), accept: (look, command) => look.Is(command.PerformerId ?? command.CardId, TutorialCards.DaigotsuHotako),
            hint: "Use Naval Invasion with Daigotsu Hotako.", then: c => { c.Foe(); c.Auto("declineReaction"); c.Foe(); }, highlight: Own(TutorialCards.DaigotsuHotako), glow: Battle),
        Do("paragon", "Shoot first", "Hotako is a Paragon, which is what A Paragon's Strength needs. Shoot the Horse Archers before they can shoot back!",
            "Click A Paragon's Strength, choose Daigotsu Hotako, then choose Horse Archers to target.", Ability("paragon-strength-attack"),
            accept: (look, command) => look.Is(command.PerformerId, TutorialCards.DaigotsuHotako) && look.Is(command.TargetId, TutorialCards.HorseArchers),
            hint: "Play A Paragon's Strength with Daigotsu Hotako against the Horse Archers.", highlight: All(Hand(TutorialCards.AParagonsStrength), Own(TutorialCards.DaigotsuHotako), Foe(TutorialCards.HorseArchers)), glow: Battle),
        Note("tactician", "Tactician", "Tactician is the next keyword. Moto Taha is a Tactician. An action with the Tactical keyword can only be performed by a Tactician, and each Tactician can perform only one Tactical action each turn. Control the Board has a Tactical ability, and it is in your hand so you can see one.\n\nEvery player also has Tactical Advantage, a Tactical Battle ability: \"Discard a card and choose your performing Tactician: Give him a Force bonus equal to the Focus Value of the discarded card.\"",
            highlight: Foe(TutorialCards.MotoTaha), glow: Battle),
        Note("tactical-advantage", "Tactical Advantage", "The Unicorn player discards a card with a Focus Value of 5 to give Moto Taha +5F.",
            then: c => c.FoeTacticalAdvantage(TutorialCards.MotoTaha, TutorialCards.Focus), highlight: Foe(TutorialCards.MotoTaha), glow: Battle),
        Note("overwhelmed", "Still not enough", "It is not enough to save the province: Moto Taha is still overwhelmed. Before you pass, one more keyword.", highlight: Foe(TutorialCards.MotoTaha), glow: Battle),
        Do("unorthodox", "Elite", "Use Unorthodox Attack to bow Bayushi Kasumi. A bowed Personality does not contribute his Force to his army.",
            "Click Unorthodox Attack, choose one of your Samurai, then choose Bayushi Kasumi.", Ability("unorthodox-attack"),
            accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiKasumi), hint: "Play Unorthodox Attack on Bayushi Kasumi.", then: c => c.Foe(),
            highlight: All(Hand(TutorialCards.UnorthodoxAttack), Foe(TutorialCards.BayushiKasumi)), glow: Battle, targets: Foe(TutorialCards.BayushiKasumi)),
        Note("elite", "Elite", "His Force didn't go down. Bayushi Kasumi has the Elite keyword: a card with Elite contributes its Force to its army even when it is bowed.", highlight: Foe(TutorialCards.BayushiKasumi), glow: Battle),
        Do("pass-battle", "Winning the battle", "You still have more than enough Force to destroy the province.", "Click Pass battle action to proceed to the battle's resolution.", Move("pass"), then: c => c.Foe(), glow: Battle),
        Do("conqueror", "Conqueror", "Two of your Personalities have Conqueror. When your Conqueror survives a battle as the attacker, his unit is straightened and does not bow from the battle's resolution. Each player may do this once per turn, so choose wisely.",
            "Select one of your Conquerors to keep from bowing.", (_, command) => command.Type == "chooseConqueror" || command.AbilityId == "conquest",
            hint: "Choose one of your Conquerors.", highlight: Own(TutorialCards.DaigotsuShiraume, TutorialCards.DaigotsuShikenuro), glow: Battle),

        // ---- Spider (you) against a lone Stalwart Personality -------------------------------------------------------------------------------------------------------------------------------------
        Note("conqueror-result", "Conqueror", "The Conqueror you chose did not bow after combat. The other bowed with the rest of your army.", then: c => c.Reset(StalwartTable, StalwartOpening),
            highlight: Own(TutorialCards.DaigotsuShiraume, TutorialCards.DaigotsuShikenuro), glow: Battle),
        Note("stalwart-intro", "Stalwart", "The table has changed again. Stalwart Personalities are hard to bow: while a Personality has Stalwart, his first bowing each turn from the effects of other players' cards is negated. Shiba Jikaro is Stalwart. Try to bow him with Paid Off.", highlight: Foe(TutorialCards.ShibaJikaro)),
        Do("paid-off-1", "Paid Off", "Paid Off bows or straightens a Personality. Use it to bow Shiba Jikaro.", "Click Paid Off, choose Shiba Jikaro and Bow, then pay.",
            Ability("paid-bow"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.ShibaJikaro), hint: "Play Paid Off to bow Shiba Jikaro.", highlight: All(Hand(TutorialCards.PaidOff), Foe(TutorialCards.ShibaJikaro)), targets: Foe(TutorialCards.ShibaJikaro)),
        Do("paid-off-2", "Only the first", "That didn't work: Stalwart negated the first bowing. It will work the second time. Use your second Paid Off.", "Click Paid Off, choose Shiba Jikaro and Bow, then pay.",
            Ability("paid-bow"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.ShibaJikaro), hint: "Play your second Paid Off to bow Shiba Jikaro.", highlight: All(Hand(TutorialCards.PaidOff), Foe(TutorialCards.ShibaJikaro)), targets: Foe(TutorialCards.ShibaJikaro)),
        Note("congratulations", "Congratulations", "You have now learned many of the keywords in Legend of the Five Rings. Select the Next Tutorial to continue learning.", button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Crane (you, Shinden Asahina, 8 Honor): Doji Dainagon, Asahina Konomi, Daidoji Kenshi and Kakita Maratai in play. Scorpion: nothing in play. Nobody draws.</summary>
    private static TutorialSetup CraneTable => new(
        new("Crane", TutorialCards.FoeStronghold, [], [],
            [new(TutorialCards.DojiDainagon), new(TutorialCards.AsahinaKonomi), new(TutorialCards.DaidojiKenshi), new(TutorialCards.KakitaMaratai)], Honor: 8),
        new("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm], [], [], Honor: 5,
            Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.SmallFarm], [TutorialCards.SneakAttack])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void CraneOpening(TutorialContext c) { foreach (var player in new[] { 0, 1 }) c.KeepFaceDown(player, 0, 1, 2, 3); c.StartTurn(); }

    /// <summary>Spider (you, The Spider's Web, 10 Honor): Daigotsu Shiraume, Daigotsu Shikenuro, Daigotsu Hotako (Experienced 3) and Ohaba with Blade of Perfection and Hunger, and A Paragon's Strength,
    /// Control the Board and Unorthodox Attack in hand. Unicorn (Plains of the Maiden, 8 Honor): Moto Taha with Horse Archers, and Bayushi Kasumi, and a Focus in hand.</summary>
    private static TutorialSetup SpiderTable => new(
        new("Spider", TutorialCards.SpiderStronghold, [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.AParagonsStrength, TutorialCards.ControlTheBoard, TutorialCards.UnorthodoxAttack],
            [new(TutorialCards.DaigotsuShiraume), new(TutorialCards.DaigotsuShikenuro), new(TutorialCards.DaigotsuHotako), new(TutorialCards.Ohaba, Attachments: [TutorialCards.BladeOfPerfection, TutorialCards.Hunger])],
            Honor: 10, Deck: Filler("Spider", TutorialCards.SpiderStronghold, [TutorialCards.SmallFarm], [TutorialCards.UnorthodoxAttack])),
        new("Unicorn", TutorialCards.UnicornStronghold, [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm], [TutorialCards.Focus],
            [new(TutorialCards.MotoTaha, Attachments: [TutorialCards.HorseArchers]), new(TutorialCards.BayushiKasumi)], Honor: 8,
            Deck: Filler("Unicorn", TutorialCards.UnicornStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void SpiderOpening(TutorialContext c) { foreach (var player in new[] { 0, 1 }) c.KeepFaceDown(player, 0, 1, 2, 3); c.Draw(3); c.Engine.TutorialDraw(1, 1); c.StartTurn(); }

    /// <summary>Spider (you, The Spider's Web) with two Paid Offs in hand and the Gold to play both; Unicorn controls a lone Shiba Jikaro.</summary>
    private static TutorialSetup StalwartTable => new(
        new("Spider", TutorialCards.SpiderStronghold, [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.PaidOff, TutorialCards.PaidOff], [new(TutorialCards.ShinomenMarsh), new(TutorialCards.SmallFarm)], Honor: 10,
            Deck: Filler("Spider", TutorialCards.SpiderStronghold, [TutorialCards.SmallFarm], [TutorialCards.PaidOff])),
        new("Unicorn", TutorialCards.UnicornStronghold, [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm], [],
            [new(TutorialCards.ShibaJikaro)], Honor: 8, Deck: Filler("Unicorn", TutorialCards.UnicornStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void StalwartOpening(TutorialContext c) { foreach (var player in new[] { 0, 1 }) c.KeepFaceDown(player, 0, 1, 2, 3); c.Draw(2); c.StartTurn(); }

    /// <summary>The Unicorn defends the Province you attack; everything else it does is asked for by a note.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], []);

    public static readonly TutorialLesson Lesson = new("keywords", "Keywords", CraneTable, Steps, FoePlays, Opening: CraneOpening);
}
