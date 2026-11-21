namespace L5R.Core;

/// <summary>The Traits and Actions lesson (curriculum id <c>traits-actions</c>): Mantis (you, Koshin Keep) against Crane (Shinden Asahina, 10 Honor). It reads the Border Keep, Yoritomo Minori, Doji Dainagon and Chiisai
/// for traits, walks the steps of an action, then plays one attack: Naval Invasion, Unorthodox Attack on Chiisai, the Crane sending Yoritomo Hama home with Asahina Munefusa, and Back to the Front bringing him back.
/// The engine plays every rule: the script only chooses what is offered. TutorialTraitsActionsTests checks the numbers and results the words name.</summary>
internal static class TutorialLessonTraitsActions
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces => look => Enumerable.Range(0, 4).Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
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

    private static bool AttackedProvince(GameCommand command, int province) => command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == province) : command.Province == province;

    public static readonly TutorialStep[] Steps =
    [
        Note("welcome", "Traits and Actions", "Welcome to the Traits and Actions tutorial. You just learned about Keywords, the bold words at the top of the text box. Now, we'll discuss the text that comes below those Keywords: Traits and Actions."),
        Note("border-keep", "The Border Keep", "Every player starts with a Border Keep in every game, so let's start there. The Border Keep has a Keyword, a Trait, and two Actions.",
            highlight: Own(TutorialCards.BorderKeep), showDefinition: TutorialCards.BorderKeep),
        Note("traits", "Traits", "Traits are card effects that are \"always on\". That means, if their trigger happens, then their effect occurs. Traits are preceded by no text and are immediately below Keywords on a card.",
            highlight: Own(TutorialCards.BorderKeep), showDefinition: TutorialCards.BorderKeep),
        Note("border-trait", "The Border Keep's Trait", "Border Keep's Trait is: Bow this card: Produce 2 Gold.\n\nThat means, if this card is straightened, you may bow it to produce 2 Gold. If the Border Keep is straightened multiple times in a turn, not just your Straighten phase, it can bow to produce Gold again.",
            highlight: Own(TutorialCards.BorderKeep), showDefinition: TutorialCards.BorderKeep),
        Note("minori", "Yoritomo Minori", "Yoritomo Minori has a Trait that gives her +1F for each other Magistrate in her army. This is automatically applied and updated as the army she is in changes. Later, you'll see this in action.",
            highlight: Own(TutorialCards.YoritomoMinori), showDefinition: TutorialCards.YoritomoMinori),
        Note("dainagon", "Doji Dainagon", "Traits are only optional if they contain the word \"may\".\n\nDoji Dainagon may Lobby as an Open action.",
            highlight: Foe(TutorialCards.DojiDainagon), showDefinition: TutorialCards.DojiDainagon),
        Note("chiisai", "Chiisai", "Some traits are restrictive. They may restrict you and they may restrict other players.\n\nChiisai's traits restrict you from attaching items or performing Iaijutsu or Political actions.\n\nChiisai also restricts other players from targeting his followers with Melee Attacks.",
            highlight: Foe(TutorialCards.Chiisai), showDefinition: TutorialCards.Chiisai),
        Note("limited", "Limited actions", "It is your Action Phase and Border Keep has a Limited action. As a reminder, you may only perform Limited actions during your Action Phase.\n\nYour Provinces are all face down, so let's use the Border Keep's once per game ability to replace them with face up cards.",
            highlight: All(Own(TutorialCards.BorderKeep), OwnProvinces)),
        Note("six-steps", "Six steps", "When performing an action, you must take 6 steps to resolve it. In many cases, the computer automates these steps because there is only one option. So, playing an action may feel fluid, even though a number of things are happening in short order."),
        Note("announce", "1. Announce", "To perform an action, first, you must Announce the action. Effectively declaring your intent to use the action. This gives your opponent the opportunity to understand what you are doing and then, if they have one, play a Reaction to negate or modify its effects.\n\nThe engine does this differently from the rulebook: when playing against the computer, you pay costs before announcing the action. You choose the performers, the targets and the payment first, and the action is announced and played when you confirm."),
        Note("performers", "2. Performers", "Second, you choose performers if performers are required.\n\nOnly Personalities and Followers can perform actions. A Personality or Follower is its own performer when the ability is on it, and a Spell or Item is performed by the Personality it is attached to. A card may also name a performer.\n\nAbilities on a bowed card cannot be used, but that does not stop a Spell or Item from using its printed actions while the Personality it is attached to is bowed, unless bowing that Personality is a cost of the action.\n\nWhen playing against the computer, you select performers before announcing the action."),
        Note("targets", "3. Targets", "Third, you choose targets, if any.\n\nActions will often instruct you to target a card. This is usually as simple as clicking on a card (or cards) of the appropriate type before clicking Confirm.\n\nWhen playing against the computer, you select targets before announcing the action."),
        Note("costs", "4. Costs", "Finally, the last thing you do is pay costs.\n\nSome cards have optional costs and mandatory costs. You must pay mandatory costs. You can tell if a cost is optional if the word \"may\" is used. Usually, then there is an \"if\" statement that applies an additional effect if you paid the optional cost."),
        Note("resolution", "5. Resolution", "From here, the engine takes over and applies the effects of your card (and allows Reactions where they may happen). This is Action Resolution."),
        Note("action-end", "6. Action End", "After Resolution is the Action End, where the ability to act moves to the next action. This is usually alternating back and forth between two players but occasionally a player will be granted additional actions that they may take in sequence."),
        Note("use-border-keep", "Use the Border Keep", "Now, let's use that Border Keep.\n\nTo use the Border Keep, click on the Border Keep.\n\nNotice in the sidebar that its first turn ability isn't available because it isn't the first turn. You will have to select the once per game option.",
            highlight: Own(TutorialCards.BorderKeep)),
        Do("choose-provinces", "Replace your Provinces", "Now that you have selected this action, click on each Province to select it for replacement.\n\nWhile Border Keep doesn't call for targets, this is effectively selecting targets of its effects.",
            "Click Border Keep's once per game option, click each of your four Provinces, then click Confirm.", Ability("border-game"),
            accept: (look, command) => Enumerable.Range(0, 4).All(i => look.Province(TutorialRunner.Seat, i) is { } id && command.CardIds?.Contains(id) == true),
            hint: "Use the once per game option and choose all four Provinces.", highlight: All(Own(TutorialCards.BorderKeep), OwnProvinces), targets: OwnProvinces,
            then: c => c.FoeUse("dainagon-lobby", performer: TutorialCards.DojiDainagon, card: TutorialCards.DojiDainagon)),
        Note("opponent-lobbies", "Your opponent's Open action", "Now, play passes to your opponent, who may use an Open action. Doji Dainagon bowed to Lobby for the Imperial Favor.",
            highlight: Foe(TutorialCards.DojiDainagon)),
        Note("favor", "The Imperial Favor", "Your opponent used Doji Dainagon's optional Trait allowing him to Lobby as an Open action. You'll learn more about the Imperial Favor later but for now, understand that it can be used to send an attacking Personality home.",
            highlight: Foe(TutorialCards.DojiDainagon)),
        Do("pass-to-attack", "Go to the Attack phase", "You don't have anything more to do, so let's move on to the Attack phase and find out more about Traits and Actions.", "Click Pass action.", Move("pass"), glow: Bar),
        Do("declare-attack", "Declare an attack", "Now declare the attack.", "Click the red Declare an attack button.", Move("declareAttack")),
        Do("assign-attackers", "Assign your attackers", "Assign Yoritomo Minori and Yoritomo Hama to attack Province 1.",
            "Click each of your Personalities and then Province 1, or drag them to it, then click Confirm assignments.",
            (_, command) => command.Type == "attack" && AttackedProvince(command, 0),
            accept: (look, command) => new[] { TutorialCards.YoritomoMinori, TutorialCards.YoritomoHama }.All(d => look.Own(d, Zone.Home).Any(id => command.Assignments is { } a && a.TryGetValue(id, out var at) && at == 0)),
            hint: "Attack the Crane player's Province 1 with Yoritomo Minori and Yoritomo Hama.", highlight: All(Own(TutorialCards.YoritomoMinori, TutorialCards.YoritomoHama), FoeProvinces(0)), glow: Battle, onlyProvinces: [0]),
        Note("force", "Minori's Force", "Notice that Yoritomo Minori has 5 Force. The engine immediately applied her Trait now that she is in an army with another Magistrate: Yoritomo Hama is a Magistrate, so Minori gains +1F as long as he is in her army.",
            highlight: Own(TutorialCards.YoritomoMinori, TutorialCards.YoritomoHama), glow: Battle),
        Do("engage", "Engagement", "Select the battle at Province 1 to resolve it.", "Click Fight at province 1.", (_, command) => command is { Type: "selectBattle", Province: 0 },
            hint: "Resolve the battle at Province 1.", highlight: FoeProvinces(0), glow: Battle),
        Do("naval", "Naval Invasion", "Both of your Personalities are Naval, so you get to take the first action. Use Yoritomo Hama as the performer.",
            "Click Naval Invasion in the sidebar, choose Yoritomo Hama, then confirm.", Ability("naval-invasion"), accept: (look, command) => look.Is(command.PerformerId ?? command.CardId, TutorialCards.YoritomoHama),
            hint: "Use Naval Invasion with Yoritomo Hama.", then: c => { c.Foe(); c.Auto("declineReaction"); c.Foe(); }, highlight: Own(TutorialCards.YoritomoHama), glow: Battle),
        Do("bow-chiisai", "Bow Chiisai", "Use Yoritomo Hama to bow Chiisai or else there will be trouble.\n\nThis requires you to click on Unorthodox Attack, choose Yoritomo Hama (selecting him as a performer), select Chiisai (choosing targets), then the effects are applied when you click Confirm.",
            "Click Unorthodox Attack, choose Yoritomo Hama, then choose Chiisai.", Ability("unorthodox-attack"),
            accept: (look, command) => look.Is(command.PerformerId, TutorialCards.YoritomoHama) && look.Is(command.TargetId, TutorialCards.Chiisai), hint: "Play Unorthodox Attack with Yoritomo Hama on Chiisai.",
            then: c => c.FoeUse("munefusa-move", card: TutorialCards.AsahinaMunefusa, target: TutorialCards.YoritomoHama),
            highlight: All(Hand(TutorialCards.UnorthodoxAttack), Own(TutorialCards.YoritomoHama), Foe(TutorialCards.Chiisai)), glow: Battle, targets: Foe(TutorialCards.Chiisai)),
        Note("hama-home", "Sent home", "Yoritomo Hama has been sent home. If you had an ability to counter this, an option to do so would have been presented to you. Now that he is no longer in the army, note that Yoritomo Minori's Force has returned to 4.",
            highlight: Own(TutorialCards.YoritomoHama, TutorialCards.YoritomoMinori), glow: Battle),
        Do("back-to-the-front", "Back to the Front", "Play Back to the Front to return Yoritomo Hama to the battle. Watch Yoritomo Minori's Force change back to 5.",
            "Click Back to the Front, then choose Yoritomo Hama.", Ability("back-to-the-front-move"), accept: (look, command) => look.Is(command.PerformerId ?? command.TargetId, TutorialCards.YoritomoHama),
            hint: "Play Back to the Front on Yoritomo Hama.", highlight: All(Hand(TutorialCards.BackToTheFront), Own(TutorialCards.YoritomoHama)), glow: Battle),
        Note("presence", "Presence", "As an aside, normally, you may not play an action if you do not have a Personality present at a battle. However, if an action would move a Personality to a battlefield, it may be taken without presence at that battlefield. Presence means you have at least one Personality under your control at a battlefield. This is satisfied even if the Personality is bowed.\n\nYou could have played Back to the Front to bring Hama back even if Yoritomo Minori wasn't there!",
            highlight: Own(TutorialCards.YoritomoMinori, TutorialCards.YoritomoHama), glow: Battle),
        Note("congratulations", "Congratulations", "This concludes the Tutorial on Traits and Actions. Select the Next Tutorial to continue learning.", button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Mantis (you, Koshin Keep, 5 Honor): a Border Keep, Yoritomo Minori and Yoritomo Hama in play, Back to the Front and Unorthodox Attack in hand, and four cards waiting to refill the Provinces.
    /// Crane (Shinden Asahina, 10 Honor): a Border Keep, Doji Dainagon, Asahina Munefusa and Chiisai in play.</summary>
    private static TutorialSetup Table => new(
        new("Mantis", TutorialCards.MantisStronghold,
            [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.YoritomoSachina, TutorialCards.KobunePort, TutorialCards.ShelteredPort, TutorialCards.ProsperousVillage],
            [TutorialCards.BackToTheFront, TutorialCards.UnorthodoxAttack],
            [new(TutorialCards.BorderKeep), new(TutorialCards.YoritomoMinori), new(TutorialCards.YoritomoHama)], Honor: 5,
            Deck: Filler("Mantis", TutorialCards.MantisStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        new("Crane", TutorialCards.FoeStronghold, [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm], [],
            [new(TutorialCards.BorderKeep), new(TutorialCards.DojiDainagon), new(TutorialCards.AsahinaMunefusa), new(TutorialCards.Chiisai)], Honor: 10,
            Deck: Filler("Crane", TutorialCards.FoeStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void Opening(TutorialContext c) { foreach (var player in new[] { 0, 1 }) c.KeepFaceDown(player, 0, 1, 2, 3); c.Draw(2); c.StartTurn(); }

    /// <summary>The Crane defends the Province you attack; every other move is asked for by a step.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], []);

    public static readonly TutorialLesson Lesson = new("traits-actions", "Traits and Actions", Table, Steps, FoePlays, Opening: Opening);
}
