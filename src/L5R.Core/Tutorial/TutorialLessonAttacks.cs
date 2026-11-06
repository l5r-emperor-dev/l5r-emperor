namespace L5R.Core;

/// <summary>The Attacks lesson (curriculum id <c>special-attacks</c>): Dragon (you, Foothills Keep) against Mantis (Koshin Keep). Mantis opens its turn by attacking your first Province. The lesson reads what a Ranged
/// or Melee Attack is, why a Personality with a Follower cannot be targeted (and a Follower can be destroyed), how Reconnaissance and Strong Defenses change a Ranged Attack's strength, and ends with Mirumoto Michi's Melee Attack.
/// The engine plays every rule: the script only chooses what is offered. TutorialAttacksTests checks the numbers and results the words name.</summary>
internal static class TutorialLessonAttacks
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
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

    private static bool AttackedProvince(GameCommand command, int province) => command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == province) : command.Province == province;

    private static readonly string[] Bar = [TutorialGlow.PhaseBar], Battle = [TutorialGlow.BattleBar];

    public static readonly TutorialStep[] Steps =
    [
        Note("welcome", "Attacks", "There are two kinds of Attack, Ranged and Melee. They work identically and differ only in name, so a card that affects Melee Attacks does not affect Ranged Attacks, and the reverse.\n\nRanged and Melee Attacks are a battle action that can be used to destroy a follower or personality during battle.",
            highlight: All(Foe(TutorialCards.TsuruchiTomaru, TutorialCards.YoritomoHaruhiko), Own(TutorialCards.MirumotoMichi))),
        Note("what-it-is", "Ranged X and Melee X", "An Attack may target a Follower, or a Personality without Followers, in the enemy army. If the strength of the attack is equal to or greater than that of the target, it destroys it.\n\nAttacks can have their strength increased or decreased before their effect is applied. You\'ll see that in a moment.\n\nMantis passes and attacks.",
            showDefinition: TutorialCards.TsuruchiTomaru, then: c => { c.Auto("pass"); c.Foe(); }),
        Note("attacked", "Attack and defend", "Mantis attacked your first Province with Tomaru and Haruhiko. Defend with everyone.",
            highlight: Foe(TutorialCards.TsuruchiTomaru, TutorialCards.YoritomoHaruhiko)),
        Do("defend", "Defend your Province", "Defend Province 1 with Mirumoto Michi and Togashi Torazu.", "Click each of your Personalities and then Province 1, or drag them to it, then click Confirm assignments.",
            (_, command) => command.Type == "assignDefense" && AttackedProvince(command, 0),
            accept: (look, command) => new[] { TutorialCards.MirumotoMichi, TutorialCards.TogashiTorazu }.All(d => look.Own(d, Zone.Home).Any(id => command.Assignments is { } a && a.TryGetValue(id, out var at) && at == 0)),
            hint: "Defend Province 1 with Mirumoto Michi and Togashi Torazu.", highlight: Own(TutorialCards.MirumotoMichi, TutorialCards.TogashiTorazu), onlyProvinces: [0],
            then: c => { c.Foe(s => s.Phase == Phase.Battle); c.FoeUse("wasteland-scout-recon", card: TutorialCards.WastelandScout); c.Foe(); }),
        Note("followers", "Followers protect their Personality", "A Personality with a Follower cannot be targeted, but the Follower can. Michi has Veteran Skirmishers (3 Force) and Haruhiko has a Wasteland Scout. Torazu has no Follower.\n\nPass and let Mantis act first.",
            highlight: Own(TutorialCards.MirumotoMichi, TutorialCards.VeteranSkirmishers, TutorialCards.TogashiTorazu), glow: Battle,
            button: "Pass", then: c => { c.Auto("pass"); c.FoeUse("tomaru-attack-3", performer: TutorialCards.TsuruchiTomaru, target: TutorialCards.VeteranSkirmishers); }),
        Note("follower-destroyed", "A Follower is destroyed", "Tomaru's Ranged 3 Attack targeted Veteran Skirmishers. It has 3 Force, so it was destroyed. Michi can be targeted now.",
            highlight: Foe(TutorialCards.TsuruchiTomaru), glow: Battle),
        Note("reconnaissance", "Reconnaissance", "Haruhiko's Wasteland Scout gave Mantis Reconnaissance. His Attack is Ranged 3, +2 with Reconnaissance, so it is a Ranged 5.\n\nPass again.",
            highlight: Foe(TutorialCards.YoritomoHaruhiko, TutorialCards.WastelandScout), glow: Battle,
            button: "Pass", then: c => { c.Auto("pass"); c.FoeUse("haruhiko-attack", performer: TutorialCards.YoritomoHaruhiko, target: TutorialCards.TogashiTorazu); }),
        Do("strong-defenses", "Strong Defenses", "Haruhiko's Ranged 5 targets Torazu, who has 4 Force and would be destroyed.\n\nStrong Defenses is a Reaction that gives an Attack -3 strength. Use Strong Defenses to reduce the ranged attack.",
            "Click Strong Defenses.", Ability("strong-defenses-reaction"), hint: "Play Strong Defenses.", highlight: All(Hand(TutorialCards.StrongDefenses), Own(TutorialCards.TogashiTorazu)), glow: Battle),
        Note("blocked", "The Attack fails", "The Attack is now Ranged 2. Torazu has 4 Force, so he survives. Strength can go down as well as up.",
            highlight: Own(TutorialCards.TogashiTorazu), glow: Battle),
        Do("melee", "Michi's Melee Attack", "Michi's Unparalleled Sword gives him a Melee 4 Attack. Tomaru has 4 Force and Haruhiko's Wasteland Scout is within reach too. Haruhiko cannot be targeted because he has a Follower.",
            "Click the Unparalleled Sword, then click your chosen target to use your Melee Attack.", Ability("unparalleled-sword-attack"),
            accept: (look, command) => look.Is(command.TargetId, TutorialCards.TsuruchiTomaru) || look.Is(command.TargetId, TutorialCards.WastelandScout), hint: "Use the Melee 4 Attack on Tsuruchi Tomaru or the Wasteland Scout.",
            highlight: All(Own(TutorialCards.MirumotoMichi, TutorialCards.UnparalleledSword), Foe(TutorialCards.TsuruchiTomaru, TutorialCards.WastelandScout)), glow: Battle, targets: Foe(TutorialCards.TsuruchiTomaru, TutorialCards.WastelandScout)),
        Note("melee-result", "Destroyed", "Melee 4 against a target with 4 Force or less: it is destroyed. A Melee Attack works just like a Ranged Attack.",
            highlight: Foe(TutorialCards.YoritomoHaruhiko), glow: Battle),
        Note("honor", "No Honor in battle", "Destroying a card during battle doesn't gain you any Honor. Only destroying cards during resolution does."),
        Note("congratulations", "Congratulations", "This concludes the Tutorial on Attacks. Select the Next Tutorial to continue learning.", button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Dragon (you, Foothills Keep, 8 Honor): a Border Keep, Gold Mine, Large Farm and Small Farm; Mirumoto Michi with the Unparalleled Sword and Veteran Skirmishers; Togashi Torazu with four Fire tokens; Strong Defenses in hand.
    /// Mantis (Koshin Keep, 8 Honor, first player): a Border Keep, Bamboo Harvesters, Kobune Port, Copper Mine and Marketplace, Tsuruchi Tomaru, and Yoritomo Haruhiko with a Wasteland Scout (his Reconnaissance).</summary>
    private static TutorialSetup Table => new(
        new("Dragon", TutorialCards.DragonStronghold, [TutorialCards.SmallFarm], [TutorialCards.StrongDefenses],
            [new(TutorialCards.BorderKeep), new(TutorialCards.GoldMine), new(TutorialCards.LargeFarm), new(TutorialCards.SmallFarm),
             new(TutorialCards.MirumotoMichi, Attachments: [TutorialCards.UnparalleledSword, TutorialCards.VeteranSkirmishers]), new(TutorialCards.TogashiTorazu, FireTokens: 4)], Honor: 8,
            Deck: Filler("Dragon", TutorialCards.DragonStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        new("Mantis", TutorialCards.MantisStronghold, [TutorialCards.SmallFarm], [],
            [new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters), new(TutorialCards.KobunePort), new(TutorialCards.CopperMine), new(TutorialCards.Marketplace),
             new(TutorialCards.TsuruchiTomaru), new(TutorialCards.YoritomoHaruhiko, Attachments: [TutorialCards.WastelandScout])], Honor: 8,
            Deck: Filler("Mantis", TutorialCards.MantisStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        FirstPlayer: 1, TurnsTaken: 3, NoEndDraw: true);

    private static void Opening(TutorialContext c) { c.Draw(1); c.StartTurn(); }

    /// <summary>Mantis sends both Personalities at your first Province; every other move is asked for by a step.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [], AttackProvince: 0);

    public static readonly TutorialLesson Lesson = new("special-attacks", "Special Actions - Attacks", Table, Steps, FoePlays, Opening: Opening);
}
