namespace L5R.Core;

/// <summary>The Rings and Enlightenment lesson (curriculum id <c>rings</c>, issue #1084), from Mendon's script: Dragon Enlightenment (you, Pillars of Virtue, 9 Honor, one Province left) against Scorpion
/// (The Towers of the Yogo, 2 Honor), late in the game in your Action phase. Four Rings are in play; the notes show each of the five and what puts it into play, and what an Enlightenment Victory needs.
/// Togashi Hizumi attacks alone, lowers the Province by 4 and destroys it, which lets Ring of Fire enter play. The last step plays the game out against the Hard practice opponent: survive the Scorpion's
/// turn and you win by Enlightenment as your next turn starts. Corrections to the script are listed in docs/TUTORIAL.md. TutorialRingsTests checks the table and the scripted part.</summary>
internal static class TutorialLessonRings
{
    internal const string HizumiLower = "hizumi-province-lower", FireRecruit = "ring-of-fire-recruit";

    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.Type == "useAbility" && command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indexes) => look => indexes.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? showDefinition = null) =>
        new() { Id = id, Title = title, Body = body, Button = "Continue", Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = showDefinition };

    private static TutorialStep Do(string id, string title, string body, string instruction, Func<TutorialLookup, GameCommand, bool> allow, Func<TutorialLookup, GameCommand, bool>? accept = null,
        string? hint = null, Action<TutorialContext>? then = null, Func<TutorialLookup, IEnumerable<string>>? highlight = null, string[]? glow = null, string? showDefinition = null) =>
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
            ShowDefinitionId = showDefinition
        };

    private static readonly string[] Battle = [TutorialGlow.BattleBar];
    private static readonly string[] Rings = [TutorialCards.RingOfTheVoid, TutorialCards.RingOfEarth, TutorialCards.RingOfWater, TutorialCards.RingOfAir];

    /// <summary>The Scorpion take their turn at a Battle action (they pass), handing the next one back to you.</summary>
    private static void FoePasses(TutorialContext c) => c.Foe(s => s.PriorityPlayerId == TutorialRunner.Seat || s.Phase != Phase.Battle);

    public static readonly TutorialStep[] Steps =
    [
        Note("rg-welcome", "Rings and Enlightenment", "Welcome to the tutorial on Rings and Enlightenment.\n\nIn this tutorial you'll learn about each of the five Rings and how to win an Enlightenment Victory."),
        Note("rg-scene", "Late in the game", "First, let's set the scene. It's late in the game and the Scorpion made an attempt to destroy you: you have one Province left. Their Personalities are all at home, bowed, and it's your Action phase.",
            highlight: Foe(TutorialCards.BayushiWaru, TutorialCards.ShosuroKoshiba, TutorialCards.ShosuroTsuji)),
        Note("rg-four", "Four Rings", "You have four Rings in play: the Ring of the Void, the Ring of Earth, the Ring of Water and the Ring of Air. You're dangerously close to playing your last one.", highlight: Own(Rings)),
        Note("rg-enlightenment", "Enlightenment Victory", "You win an Enlightenment Victory when one of your turns starts and you have five Rings in play, each with a different element (Air, Earth, Fire, Water and Void).\n\nEach of them must have last entered play by its own text: the condition printed on the Ring. A card that puts a Ring into play for you doesn't count, so a Ring brought in that way makes an Enlightenment Victory impossible (or even more difficult) until it leaves play and enters again by its own text.",
            highlight: Own(Rings)),
        Note("rg-void", "The Ring of the Void", "\"After you resolve your fourth or later non-Kiho action from Strategies in one phase: You may put this card into play from your hand.\"\n\nFour actions from Strategy cards that aren't Kiho, resolved in the same phase. A phase is one of the named sections of the turn on the phase bar. In practice that means the Action phase or the Battle phase.",
            showDefinition: TutorialCards.RingOfTheVoid, highlight: Own(TutorialCards.RingOfTheVoid), glow: [TutorialGlow.PhaseBar]),
        Note("rg-earth", "The Ring of Earth", "\"After the resolution of a battle that you won, if there were ever any enemy units at that battlefield and none of your Personalities were destroyed during the battle: You may put this card into play from your hand.\"\n\nWin a battle that the enemy fought in, without losing a Personality.",
            showDefinition: TutorialCards.RingOfEarth, highlight: Own(TutorialCards.RingOfEarth)),
        Note("rg-air", "The Ring of Air", "\"After you resolve your third or later Spell or Kiho action in one turn from cards with different titles: You may put this card into play from your hand.\"\n\nThree Spell or Kiho actions in one turn, each from a card with a different title: two copies of the same Kiho count once.",
            showDefinition: TutorialCards.RingOfAir, highlight: Own(TutorialCards.RingOfAir)),
        Note("rg-water", "The Ring of Water", "\"After you resolve your fourth or later Battle action in one battle from cards with different titles: You may put this card into play from your hand.\"\n\nFour Battle actions in one battle, each from a card with a different title.",
            showDefinition: TutorialCards.RingOfWater, highlight: Own(TutorialCards.RingOfWater)),
        Note("rg-fire", "The Ring of Fire", "\"After the resolution of a battle at a province, if you destroyed one or more provinces or enemy cards in that resolution: You may put this card into play from your hand.\"\n\nDestroy a province, or enemy cards, as a battle at a province resolves. Winning destroys the losing army, and a tie destroys both, so at a minimum you must tie an opposed battle, or win an unopposed one by destroying the province.",
            showDefinition: TutorialCards.RingOfFire, highlight: Hand(TutorialCards.RingOfFire), glow: [TutorialGlow.Hand]),
        Note("rg-together", "One effort, several Rings", "Each Ring's condition is checked on its own, but nothing stops one effort from meeting several. A player who resolves four non-Kiho Strategy actions and three Kiho actions in one battle, all with different titles, has met the conditions of the Ring of Water, the Ring of Air and the Ring of the Void. If they then win the battle without losing a Personality, they meet the Ring of Fire's and the Ring of Earth's as well.\n\nMore likely, each Ring comes into play piecemeal, one or two at a time, through great effort and some luck."),
        Note("rg-abilities", "Rings in play", "A Ring is Unique, and once in play it stays there. Each also has an ability you can use by bowing it, or by discarding it from your hand: the Ring of Earth gives a province +3 or -3 strength in a battle, the Ring of Air straightens a Personality's unit and the Ring of Water moves one. Bowing a Ring in play doesn't take it out of play, so it still counts toward Enlightenment.",
            highlight: Own(Rings)),
        Note("rg-path", "Your path to victory", "You have a clear path to victory: destroy a province to bring in the Ring of Fire, then survive until your next turn starts.\n\nYou have nothing else to do in your Action phase, so you pass to the Attack phase.", highlight: Hand(TutorialCards.RingOfFire),
            then: c => { c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); }),
        Do("rg-declare", "Your attack", "Declare an attack.", "Click the red Declare an attack button.", Move("declareAttack"), glow: Battle),
        Do("rg-assign", "Togashi Hizumi attacks", "Send Togashi Hizumi, alone, to attack any of the Scorpion's provinces. Their Personalities are bowed, so nobody can defend it.",
            "Drag Togashi Hizumi to one of the Scorpion's provinces (or click him, then the province), then click Confirm assignments.", Move("attack"),
            accept: (look, command) => command.Assignments is { Count: 1 } a && a.Keys.All(id => look.Is(id, TutorialCards.TogashiHizumi)),
            hint: "Attack one of the Scorpion's provinces with Togashi Hizumi alone.", highlight: All(Own(TutorialCards.TogashiHizumi), FoeProvinces(0, 1, 2, 3)), glow: Battle),
        Do("rg-engage", "Choose the battle", "Choose the battle at the province Hizumi attacked.", "Click Fight at the province Hizumi attacked.",
            (look, command) => command.Type == "selectBattle" && command.Province == look.BattlefieldOf(TutorialCards.TogashiHizumi), hint: "Fight at the province Togashi Hizumi attacked.", glow: Battle, then: c => { c.PassNestedWindows(); FoePasses(c); }),
        Do("rg-hizumi", "Togashi Hizumi", "The Scorpion have passed. Hizumi has 6 Force and the province has a strength of 7, so on his own he can't destroy it.\n\nHizumi's ability: \"Battle: Give the current battlefield's province +4 or -4 strength.\" Use it to give the province -4 strength.",
            "Click Togashi Hizumi, then choose his -4 strength ability.", Ability(HizumiLower), hint: "Use Togashi Hizumi's ability to give the province -4 strength.", highlight: Own(TutorialCards.TogashiHizumi), glow: Battle,
            then: c => { c.PassNestedWindows(); FoePasses(c); }),
        Do("rg-pass", "Resolve the battle", "The province's strength is now 3, and Hizumi's 6 Force is enough. Pass: the battle resolves, Hizumi destroys the province, and you'll have the chance to play the Ring of Fire.",
            "Click Pass battle action.", Move("pass"), glow: Battle, then: c => c.Foe(s => s.PriorityPlayerId == TutorialRunner.Seat || s.Phase != Phase.Battle)),
        Do("rg-fire-play", "The Ring of Fire", "You destroyed a province as the battle resolved, so the Ring of Fire's condition is met. Put it into play.", "Click the Ring of Fire in your hand and put it into play.", Ability(FireRecruit),
            hint: "Put the Ring of Fire into play.", highlight: Hand(TutorialCards.RingOfFire), showDefinition: TutorialCards.RingOfFire, glow: [TutorialGlow.Hand]),
        new()
        {
            Id = "rg-survive", Title = "Survive", FreePlay = true, Allow = (_, _) => true,
            Body = "Tremendous. You have achieved Enlightenment: five Rings in play, each brought in by its own text. Now all you have to do is survive until your next turn starts to win.\n\nThe Scorpion now play as the Hard practice opponent. Play the rest of your turn, then defend your last province: your Rings and your Kiho can help.",
            Instruction = "Finish your turn and defend your last province until your next turn starts.",
            Highlight = Own([TutorialCards.RingOfTheVoid, TutorialCards.RingOfEarth, TutorialCards.RingOfWater, TutorialCards.RingOfAir, TutorialCards.RingOfFire]),
            Won = ("Congratulations!", "CONGRATULATIONS! YOU HAVE COMPLETED ALL OF THE TUTORIALS!"),
            Lost = ("You lose", "YOU LOSE! TRY AGAIN!")
        },
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    // Your decks are cards from the Dragon Enlightenment deck (no Rings: each is Unique, and all five are already in play or in hand). Your last province holds Togashi Osawa; the Scorpion's four hold
    // the script's Bayushi Kasumi, Bayushi Waru, Geisha House and Small Farm, face down.
    private static readonly string[] DragonDynasty = [TutorialCards.TogashiOsawaExp, TutorialCards.TogashiKasuru, TutorialCards.TempleOfHarmony, TutorialCards.TogashiTsukagi];
    private static readonly string[] DragonFate = [TutorialCards.PillarTattoo, TutorialCards.SpinningHeelKick, TutorialCards.WeaknessOfMan, TutorialCards.NerveStrike];
    private static readonly string[] ScorpionDynasty = [TutorialCards.BayushiKasumi, TutorialCards.BayushiWaru, TutorialCards.GeishaHouse, TutorialCards.SmallFarm];
    private static readonly string[] ScorpionFate = [TutorialCards.Brawl, TutorialCards.CrushingStrength];

    /// <summary>You (Dragon, Pillars of Virtue, 9 Honor, Provinces 1-3 destroyed): Border Keep, Gold Mine, Prosperous Village, Temple of Harmony, Tattoos &amp; Trinkets and Small Farm; the Rings of the Void, Earth,
    /// Water and Air; Togashi Kasuru, Togashi Hizumi and Togashi Tsukagi; the Ring of Fire, Nerve Strike, Grappling the Snake and Deadly Discipline in hand, and seven cards in the Fate discard pile. The Scorpion
    /// (The Towers of the Yogo, 2 Honor): Bamboo Harvesters, Prosperous Village, Overlooked Palace, Red Crane Dojo, Remote Village and Border Keep; Bayushi Waru, Shosuro Koshiba with a Nightingale Blade and
    /// Shosuro Tsuji, all bowed; Hiromi's Vassals, Brawl, At Any Cost, Crushing Strength and Elite Training in hand.</summary>
    private static TutorialSetup Table => new(
        new("Dragon", TutorialCards.PillarsOfVirtue, DragonDynasty, [TutorialCards.RingOfFire, TutorialCards.NerveStrike, TutorialCards.GrapplingTheSnake, TutorialCards.DeadlyDiscipline],
            [new(TutorialCards.BorderKeep), new(TutorialCards.GoldMine), new(TutorialCards.ProsperousVillage), new(TutorialCards.TempleOfHarmony), new(TutorialCards.TattoosAndTrinkets), new(TutorialCards.SmallFarm),
             new(TutorialCards.RingOfTheVoid), new(TutorialCards.RingOfEarth), new(TutorialCards.RingOfWater), new(TutorialCards.RingOfAir),
             new(TutorialCards.TogashiKasuru), new(TutorialCards.TogashiHizumi), new(TutorialCards.TogashiTsukagi)], Honor: 9,
            Deck: Filler("Dragon", TutorialCards.PillarsOfVirtue, DragonDynasty, DragonFate),
            FateDiscard: [TutorialCards.DeadlyDiscipline, TutorialCards.GrapplingTheSnake, TutorialCards.NerveStrike, TutorialCards.Ambush, TutorialCards.PillarTattoo, TutorialCards.VoidTattoo, TutorialCards.StringOfVictories],
            DestroyedProvinces: [0, 1, 2]),
        new("Scorpion", TutorialCards.TowersOfTheYogo, ScorpionDynasty, [TutorialCards.HiromisVassals, TutorialCards.Brawl, TutorialCards.AtAnyCost, TutorialCards.CrushingStrength, TutorialCards.EliteTraining],
            [new(TutorialCards.BambooHarvesters), new(TutorialCards.ProsperousVillage), new(TutorialCards.OverlookedPalace), new(TutorialCards.RedCraneDojo), new(TutorialCards.RemoteVillage), new(TutorialCards.BorderKeep),
             new(TutorialCards.BayushiWaru, Bowed: true), new(TutorialCards.ShosuroKoshiba, Bowed: true, Attachments: [TutorialCards.NightingaleBlade]), new(TutorialCards.ShosuroTsuji, Bowed: true)], Honor: 2,
            Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, ScorpionDynasty, ScorpionFate)),
        TurnsTaken: 8);

    /// <summary>Both hands are drawn, the Scorpion's provinces stay face down (yours shows Togashi Osawa) and your turn starts.</summary>
    private static void Opening(TutorialContext c)
    {
        c.Draw(4); c.FoeDraw(5);
        c.KeepFaceDown(1 - TutorialRunner.Seat, 0, 1, 2, 3);
        c.StartTurn();
    }

    public static readonly TutorialLesson Lesson = new("rings", "Rings and Enlightenment", Table, Steps, TutorialFoeScript.Passive, Opening: Opening);
}
