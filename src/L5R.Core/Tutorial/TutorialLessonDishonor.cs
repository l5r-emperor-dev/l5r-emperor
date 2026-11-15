namespace L5R.Core;

/// <summary>The Dishonor lesson (curriculum id <c>special-dishonor</c>): Crab Control (you, Yasuki Palaces - exp, 2 Honor) against Dragon Destruction (Monastery of New Thought, -17 Honor), in your Action phase.
/// Yasuki Jekku dishonors Mirumoto Gobashi (the Dragon fall to -18), the Dragon order Gobashi to commit Seppuku, Kaiu Iemasa's "Gain 3 Honor" rehonors him instead, the Slanderer dishonors Togashi Meiyu (a Monk, so no Seppuku),
/// and The Heir's Wrath destroys her: her base Personal Honor of 2 takes the Dragon to -20, a Dishonor loss at the beginning of their next End Phase. The engine plays every rule: the script only chooses what is offered and has the
/// Dragon order the Seppuku. TutorialDishonorTests checks every number the words name.</summary>
internal static class TutorialLessonDishonor
{
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? showDefinition = null, bool showFoeDead = false) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = showDefinition, ShowFoeDead = showFoeDead };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null, Func<TutorialLookup, IEnumerable<string>>? targets = null) =>
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
            Targets = targets
        };

    private static bool Targets(TutorialLookup look, GameCommand command, string definition) => look.Is(command.TargetId, definition);

    public static readonly TutorialStep[] Steps =
    [
        Note("dishonor-welcome", "Dishonor", "Welcome to the tutorial on Dishonor.\n\nYou may remember that Personalities may be honorable or dishonorable, that a player may gain or lose Honor, and that a player who loses enough Honor loses the game.\n\nThis tutorial shows what dishonor does."),
        Note("honorable", "Honorable and dishonorable", "Your opponent, the Dragon, has two Personalities in play, Togashi Meiyu and Mirumoto Gobashi. Both are honorable: their cards are upright.\n\nIn this game a dishonorable Personality's card is turned upside down. Changing a Personality from honorable to dishonorable is called dishonoring him; changing him back is rehonoring.",
            highlight: Foe(TutorialCards.TogashiMeiyu, TutorialCards.MirumotoGobashi)),
        Do("jekku", "Dishonor Gobashi", "Yasuki Jekku's ability dishonors a Personality for Gold equal to his base Personal Honor minus 1. Gobashi's is 2, so it costs 1 Gold.\n\nUse Jekku's ability and your Small Farm to dishonor Mirumoto Gobashi.",
            "Click Yasuki Jekku, then click Mirumoto Gobashi, then click your Small Farm, then click Use Yasuki Jekku.", Ability("jekku-dishonor"),
            accept: (look, command) => Targets(look, command, TutorialCards.MirumotoGobashi) && command.SourceIds is [var farm] && look.Is(farm, TutorialCards.SmallFarm),
            hint: "Use Yasuki Jekku on Mirumoto Gobashi, paid with your Small Farm.", highlight: All(Own(TutorialCards.YasukiJekku, TutorialCards.SmallFarm), Foe(TutorialCards.MirumotoGobashi)),
            targets: Foe(TutorialCards.MirumotoGobashi), then: c => c.FoeWaits()),
        Note("dishonored", "Dishonored", "Mirumoto Gobashi has been turned upside down: he is dishonorable.\n\nJekku's ability also made the Dragon lose 1 Honor, so they are now at -18. Being dishonored does not cost Honor by itself; Jekku's text did that.\n\nFamily Honor is the one number that can fall below zero. Force, Chi and Personal Honor stop at zero.",
            highlight: Foe(TutorialCards.MirumotoGobashi)),
        Note("personal-honor", "A maximum of zero", "A dishonorable Personality has a maximum Personal Honor of 0. Gobashi's printed Personal Honor is 2, but while he is dishonorable it counts as 0. He can't do anything that needs Personal Honor of 1 or higher: for example, he can't Lobby for the Imperial Favor.\n\nA dishonorable Personality can't be dishonored again: an effect that tries fails, and he can't pay a cost of being dishonored.",
            highlight: Foe(TutorialCards.MirumotoGobashi)),
        Note("minus-twenty", "Losing by Dishonor", "If a player's Family Honor is ever -20 or lower, they lose the game at the beginning of their next End Phase, even if something has raised their Family Honor above -20 by then.\n\nThe Dragon are at -18. If Gobashi were destroyed while dishonorable, the Dragon would lose 2 more Honor and reach -20.",
            highlight: Foe(TutorialCards.MirumotoGobashi)),
        Note("gobashi-seppuku", "Seppuku", "The Dragon player doesn't want to lose to dishonor, and has a defense: they will order Gobashi to commit Seppuku, ritual suicide.\n\nAs an Open action, any number of times per turn, a player may order a dishonorable Samurai, Courtier or Shugenja they control to commit Seppuku. Gobashi is a Samurai.",
            highlight: Foe(TutorialCards.MirumotoGobashi), then: c => c.FoeOrderSeppuku(TutorialCards.MirumotoGobashi)),
        Note("seppuku-honorable", "An honorable death", "Seppuku rehonors the Personality and then destroys him, and neither can be negated. Gobashi died honorable, so the Dragon lost no Honor: they are still at -18.\n\nSamurai, Shugenja and Courtiers may all be ordered to commit Seppuku. It's brutal, but sometimes it's necessary to keep your opponent from eliminating you."),
        Note("rehonor", "Rehonoring", "Seppuku is one way to be rehonored, and some cards rehonor a Personality. The rules also rehonor your dishonorable Personality instead of giving you Honor when you would gain it:\n\n• from an action or trait that targeted, came from, or was performed by him;\n• from attaching a card to him;\n• from destroying enemy cards in battle resolution while he is in your army.",
            highlight: Own(TutorialCards.KaiuIemasa)),
        Do("iemasa", "Rehonor Kaiu Iemasa", "Your own Kaiu Iemasa is dishonorable: his card is upside down. His ability says \"Gain 3 Honor\". Because the ability comes from a dishonorable Personality, he is rehonored instead, and you gain no Honor.\n\nUse Kaiu Iemasa's ability.",
            "Click Kaiu Iemasa, then click Use Kaiu Iemasa.", Ability("iemasa-rebuild"), hint: "Use Kaiu Iemasa's ability.", highlight: Own(TutorialCards.KaiuIemasa)),
        Note("rehonored", "Rehonored", "Kaiu Iemasa is upright again: he is honorable. You are still on 2 Honor.\n\nHis ability also means you may not declare an attack this turn.",
            highlight: Own(TutorialCards.KaiuIemasa), glow: [TutorialGlow.Honor]),
        Do("slanderer", "Slanderer", "Togashi Meiyu is a Monk. She isn't a Samurai, a Courtier or a Shugenja, so the Dragon can't order her to commit Seppuku.\n\nThe Slanderer can dishonor a target Personality if you control a Courtier or Magistrate. Yasuki Jekku and Yasuki Tono are Courtiers. Dishonor Togashi Meiyu with the Slanderer.",
            "Click the Slanderer, then click Togashi Meiyu, then click Use Slanderer.", Ability("slanderer-dishonor"), accept: (look, command) => Targets(look, command, TutorialCards.TogashiMeiyu),
            hint: "Use the Slanderer on Togashi Meiyu.", highlight: All(Own(TutorialCards.Slanderer), Foe(TutorialCards.TogashiMeiyu)), targets: Foe(TutorialCards.TogashiMeiyu)),
        Note("dishonorable-dead", "Dying dishonorable", "Becoming dishonorable doesn't always cost Honor, but it threatens a loss. When a dishonorable Personality is destroyed, his controller loses Honor equal to his base Personal Honor. That is a rule of the game, not a card effect. He stays dishonorable in the Dead pile: he is dishonorably dead.\n\nMeiyu's Personal Honor counts as 0 while she is dishonorable, but her base Personal Honor is 2. If she dies, the Dragon lose 2 Honor and fall to -20.",
            highlight: Foe(TutorialCards.TogashiMeiyu)),
        Do("heirs-wrath", "The Heir's Wrath", "The Heir's Wrath destroys another player's Personality whose unit has a total Gold Cost no higher than the Gold you pay for it. Meiyu's Gold Cost is 5, so you pay 5 Gold. You also discard the Imperial Favor and lose 2 Honor, and none of its effects can be negated.\n\nUse The Heir's Wrath to destroy Togashi Meiyu.",
            "Click The Heir's Wrath, then click Togashi Meiyu, then click Yasuki Palaces to pay 5 Gold, then click Play The Heir's Wrath.", Ability(EmperorMechEAbilities.HeirsWrathFavor), accept: (look, command) => Targets(look, command, TutorialCards.TogashiMeiyu),
            hint: "Play The Heir's Wrath on Togashi Meiyu, discarding the Imperial Favor.", highlight: All(Hand(TutorialCards.TheHeirsWrath), Foe(TutorialCards.TogashiMeiyu)), targets: Foe(TutorialCards.TogashiMeiyu)),
        Note("dead-pile", "Remembered in death", "This is the Dragon's Dead pile. Gobashi died honorably and Meiyu died dishonorably. A Personality keeps his honorable or dishonorable status when he leaves play: if a card brings either of them back into play, he returns as he died.\n\nClick Back to table, then Continue.",
            showFoeDead: true),
        Note("dishonor-loss", "Dishonor", "Look at the Dragon's Family Honor: -20. At the beginning of their next End Phase they lose the game, even if they gain Honor before then. You are the only other player, so you win by Dishonor!\n\nThe Heir's Wrath cost you 2 Honor, so you are on 0."),
        Note("dishonor-congratulations", "Congratulations", "Congratulations on completing the tutorial on Dishonor! Select the Next Tutorial to continue learning.", button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    // Province cards from each side's real deck list (Crab Control, Dragon Destruction); every Province stays face down.
    private static readonly string[] CrabDynasty = ["oracle-l5r-9449", "oracle-l5r-7494", "oracle-l5r-9464", "oracle-l5r-2878", "oracle-l5r-10044", "oracle-l5r-10219"];
    private static readonly string[] DragonDynasty = ["oracle-l5r-5148", "oracle-l5r-10033", "oracle-l5r-10057", "oracle-l5r-6258", "oracle-l5r-5181", "oracle-l5r-10035"];

    /// <summary>Crab (you, Yasuki Palaces - exp, 2 Honor, the Imperial Favor): Slanderer, Iron Mine, Prosperous Village, Border Keep, Bamboo Harvesters and a Small Farm; Yasuki Jekku, Yasuki Tono and a dishonorable Kaiu Iemasa;
    /// Remember Your Ancestors, Dismissing the Cur and The Heir's Wrath in hand. Dragon (Monastery of New Thought, -17 Honor): Border Keep, Gold Mine, Large Farm, Small Farm and Prosperous Village; Togashi Meiyu and Mirumoto Gobashi.</summary>
    private static TutorialSetup Table => new(
        new("Crab", TutorialCards.YasukiPalaces, CrabDynasty, [TutorialCards.RememberYourAncestors, TutorialCards.DismissingTheCur, TutorialCards.TheHeirsWrath],
            [new(TutorialCards.Slanderer), new(TutorialCards.IronMine), new(TutorialCards.ProsperousVillage), new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters), new(TutorialCards.SmallFarm),
             new(TutorialCards.YasukiJekku), new(TutorialCards.YasukiTono), new(TutorialCards.KaiuIemasa)], Honor: 2,
            Deck: Filler("Crab", TutorialCards.YasukiPalaces, CrabDynasty, [TutorialCards.Focus])),
        new("Dragon", TutorialCards.MonasteryOfNewThought, DragonDynasty, [],
            [new(TutorialCards.BorderKeep), new(TutorialCards.GoldMine), new(TutorialCards.LargeFarm), new(TutorialCards.SmallFarm), new(TutorialCards.ProsperousVillage),
             new(TutorialCards.TogashiMeiyu), new(TutorialCards.MirumotoGobashi)], Honor: -17,
            Deck: Filler("Dragon", TutorialCards.MonasteryOfNewThought, DragonDynasty, [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void Opening(TutorialContext c) { c.Draw(3); c.GiveFavor(); c.Dishonor(TutorialCards.KaiuIemasa); c.KeepFaceDown(TutorialRunner.Seat, 0, 1, 2, 3); c.StartTurn(); }

    /// <summary>The Dragon only ever plays what a step asks of it (the Seppuku).</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], []);

    public static readonly TutorialLesson Lesson = new("special-dishonor", "Special Actions - Dishonor", Table, Steps, FoePlays, Opening: Opening);
}
