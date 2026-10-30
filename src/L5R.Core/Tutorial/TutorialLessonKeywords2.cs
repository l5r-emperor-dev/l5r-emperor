namespace L5R.Core;

/// <summary>The Keywords 2 lesson (issue #1150, curriculum id <c>keywords-2</c>), the second of two. Three tables, each set up with one <see cref="TutorialContext.Reset"/>: Phoenix (you) against Dragon, at your Dynasty phase, for the
/// keywords that limit what may enter play (Unique, Singular, Loyal, and Sensei's discard); Scorpion (you) against Dragon, who attacks you, for Destined, Brash, Expendable, Reserve, Terrain and Overconfident; and Scorpion against Crane
/// for Kharmic and Experienced/Inexperienced, ending on the Duelist keyword. The engine plays every rule: the script only chooses what is offered and has the opponent make exactly the plays the notes describe.
/// Every number in the words is checked by TutorialKeywords2Tests.</summary>
internal static class TutorialLessonKeywords2
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? card = null) =>
        (look, command) => command.Type == type && (card is null || look.Is(command.CardId, card));
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? showDefinition = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, ShowDefinitionId = showDefinition };

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

    private static bool AttackedProvince(GameCommand command, int province) => command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == province) : command.Province == province;

    private static readonly string[] Bar = [TutorialGlow.PhaseBar], Battle = [TutorialGlow.BattleBar], HandGlow = [TutorialGlow.Hand];

    private static readonly string[] UniqueInPlay = [TutorialCards.AsakoRikateExp, TutorialCards.TemplesOfGiseiToshi, TutorialCards.ShindenShorai, TutorialCards.ImperialGift];
    private static readonly string[] UniqueInHand = [TutorialCards.Renewal, TutorialCards.BladesOfTheBlackDragon, TutorialCards.EliteSentry, TutorialCards.RingOfWater, TutorialCards.Hanabi];
    private static readonly string[] Nitoshis = [TutorialCards.BayushiNitoshi, TutorialCards.BayushiNitoshiExp, TutorialCards.BayushiNitoshiInexperienced];

    public static readonly TutorialStep[] Steps =
    [
        // ---- Phoenix (you) against Dragon, at your Dynasty phase: Unique, Singular, Loyal, Sensei ----------------------------------------------------------------------------------------------------------
        Note("welcome", "Keywords 2", "In this tutorial, you will learn about the remaining functional keywords. We begin with the keywords that play a role in deck building and in what may enter play: Unique, Singular, and Loyal."),
        Note("unique", "Unique", "A Unique card is limited to one copy in your deck. Any card type can be Unique: Personalities like Asako Rikate, Holdings like the Temples of Gisei Toshi, Regions like Shinden Shorai, and Events like Imperial Gift.",
            highlight: All(Own(TutorialCards.AsakoRikateExp, TutorialCards.TemplesOfGiseiToshi, TutorialCards.ShindenShorai, TutorialCards.ImperialGift))),
        Note("unique-hand", "Unique cards in hand", "Your hand holds more Unique cards: a Strategy like Renewal, an Item like Blades of the Black Dragon, a Follower like Elite Sentry, a Ring like the Ring of Water, and a Spell like Hanabi.",
            highlight: Hand(UniqueInHand), glow: HandGlow),
        Note("unique-play", "Unique in play", "Unique limits play, too. A Unique card will not enter play for a player who already controls a Unique card with the same title. Only you are held to it: the Dragon player has the same cards and may bring out their own copies.",
            highlight: Own(TutorialCards.AsakoRikateExp)),
        Note("unique-control", "Unique and control", "It limits changes of control as well. If a unit would change control to a player who already controls a Unique card with a title that unit holds, the entire unit fails to change control."),
        Note("singular", "Singular", "Singular cards, on the other hand, are held to the ordinary limit of 3 copies in a deck. However, a Singular card can not enter play or become controlled by a player who already controls a Singular card with the same title. One in play at a time, including copies you gain control of.",
            highlight: Own(TutorialCards.TravelingPeddler)),
        Do("peddler-1", "Bring out a Traveling Peddler", "Traveling Peddler is Singular. Bring out a Traveling Peddler.", "Click a Traveling Peddler in your Provinces, choose a payment of 2 Gold, then click Recruit.",
            Move("purchase", TutorialCards.TravelingPeddler), hint: "Recruit a Traveling Peddler.", highlight: Own(TutorialCards.TravelingPeddler)),
        Note("peddler-2", "Only one", "Now try to recruit the other Traveling Peddler. You cannot: its option is greyed out, and the game says why when you point at it. If the Traveling Peddler in play is destroyed, you could bring this second copy into play.",
            highlight: Own(TutorialCards.TravelingPeddler)),
        Note("loyal", "Loyal", "The last of the three is Loyal. Isawa Sanetomo has it. She appears in your Provinces and in the Dragon player's. A Loyal Personality will only join a player who shares her Clan alignment: she is Phoenix Clan, so you may recruit her and the Dragon player never can.",
            highlight: All(Own(TutorialCards.IsawaSanetomo), Foe(TutorialCards.IsawaSanetomo))),
        Note("loyal-control", "Loyal and control", "Loyal covers a change of control as well as bringing her into play. If an effect tried to hand Sanetomo to the Dragon player, she would not join them.",
            highlight: Own(TutorialCards.IsawaSanetomo)),
        Note("sensei", "Sensei", "A note on Sensei. Sanetomo is a Sensei Personality. Sensei is not a functional keyword, but every Sensei Personality's own text says: \"After [he or she] enters play: Discard all your other Sensei Personalities.\"\n\nSo bringing in a Sensei Personality usually discards any other Sensei Personality you control, other copies of the same card included. It works much like a new Celestial replacing your old one.",
            then: c => c.Reset(DestinedTable, DestinedOpening), highlight: Own(TutorialCards.IsawaSanetomo)),

        // ---- Scorpion (you) against Dragon, who attacks you: Destined, Brash, Expendable, Reserve, Terrain, Overconfident --------------------------------------------------------------------------------
        Note("destined-intro", "More keywords", "The table has changed: you are the Scorpion Clan, and the Dragon are Kitsuki Minori and Togashi Noboru. Now for Destined, Brash, Expendable, Reserve, Terrain and Overconfident.",
            highlight: Own(TutorialCards.YogoTakashi, TutorialCards.BayushiWateru)),
        Do("destined", "Destined", "Yogo Takashi has Destined. After a Destined card enters play, its controller draws a card. This is not optional. Recruit Yogo Takashi and draw a card.",
            "Click Yogo Takashi in your Provinces, choose the Clan discount, pay, then click Recruit.", Move("purchase", TutorialCards.YogoTakashi), accept: (_, command) => command.ClanDiscount && !command.ClanAllegiance,
            hint: "Recruit Yogo Takashi with the Clan discount.", highlight: OwnProvinces(1)),
        Do("end-turn", "End your turn", "Now, end your turn and see what the Dragon player does.", "Click End Dynasty Phase.", Move("endDynasty"),
            then: c => { c.Foe(s => s.ActivePlayerId == 1 && s.Phase == Phase.Action && s.PriorityPlayerId == TutorialRunner.Seat); c.Auto("pass"); }, glow: Bar),
        Note("dragon-turn", "The Dragon's turn", "The Dragon player has Togashi Noboru, who is Brash, and Kitsuki Minori, who is Overconfident. Let's find out what they do.",
            then: c => c.Foe(), highlight: Foe(TutorialCards.TogashiNoboru, TutorialCards.KitsukiMinori)),
        Do("brash", "Brash", "The Dragon player assigned both Personalities to attack the Province with Bayushi Wateru. When a Brash card is assigned to attack, the Defender may draw a card. This is optional. Choose to draw a card.",
            "Click Draw 1 card.", Move("brashDraw"), accept: (_, command) => command.Choice == "draw", hint: "Choose to draw a card.", highlight: Foe(TutorialCards.TogashiNoboru), glow: HandGlow),
        Do("defend", "Defend", "Assign Yogo Takashi to defend.", "Click Yogo Takashi, then Province 1, then click Confirm.", (_, command) => command.Type == "assignDefense" && AttackedProvince(command, 0), hint: "Defend Province 1 with Yogo Takashi.", then: c => c.Foe(s => s.Phase == Phase.Battle && s.PriorityPlayerId == TutorialRunner.Seat), highlight: Own(TutorialCards.YogoTakashi)),
        Do("block-supply-lines", "Block Supply Lines", "You get the first action, and Togashi Noboru is dangerous. Use Block Supply Lines to send him home.", "Click Block Supply Lines, then choose Togashi Noboru.",
            Ability("block-supply"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.TogashiNoboru), hint: "Play Block Supply Lines on Togashi Noboru.",
            then: c => c.FoeUse("a-stain-cleansed-destroy", performer: TutorialCards.KitsukiMinori, card: TutorialCards.AStainCleansed, target: TutorialCards.YogoTakashi),
            highlight: All(Hand(TutorialCards.BlockSupplyLines), Foe(TutorialCards.TogashiNoboru)), glow: Battle),
        Note("expendable", "Expendable", "The Dragon player played A Stain Cleansed to destroy Yogo Takashi. Takashi is Expendable: after your Expendable card is destroyed, you draw a card. It is not optional, and you drew the Celestial Sword of the Dragon.",
            highlight: Hand(TutorialCards.CelestialSwordOfTheDragonExp), glow: HandGlow),
        Note("reserve-intro", "Reserve", "You have no one left to defend, but you can still win this battle, because Bayushi Wateru has the Reserve keyword.", highlight: OwnProvinces(0)),
        Do("reserve", "Reserve", "A Reserve Personality may be recruited as a Battle action, from the Province under attack, if he would be opposed. Kitsuki Minori is here, so Bayushi Wateru would be opposed.",
            "Click Bayushi Wateru in your Province, choose a payment, then click Recruit.", Move("recruitReserve", TutorialCards.BayushiWateru), hint: "Recruit Bayushi Wateru from Reserve.", highlight: OwnProvinces(0), glow: Battle),
        Do("interrupt", "Wateru's Interrupt", "Wateru was just recruited during battle, so you may use her Interrupt to take an additional Battle action. An Interrupt is used before an action's effects resolve, and this one may be used for the action that brought her in.",
            "Click Use Interrupt.", Ability("wlpa-wateru-additional"), hint: "Use Bayushi Wateru's Interrupt.", highlight: Own(TutorialCards.BayushiWateru), glow: Battle),
        Do("terrain", "Terrain", "Terrain is a Battle Strategy keyword: it establishes the ground the battle is fought on, and enters play at the battlefield. It is in no army, so it is never an enemy or opposing card. While one is in play at a battlefield, another Terrain may not be played there. Play Thick Marsh.",
            "Click Thick Marsh.", Move("playStrategy", TutorialCards.ThickMarsh), hint: "Play Thick Marsh.", highlight: Hand(TutorialCards.ThickMarsh), glow: Battle),
        Do("marsh-bows", "Thick Marsh", "After Thick Marsh enters play, bow an enemy card at the battlefield. Choose Kitsuki Minori.", "Choose Kitsuki Minori.", (_, command) => command.TargetId is not null,
            accept: (look, command) => look.Is(command.TargetId, TutorialCards.KitsukiMinori), hint: "Bow Kitsuki Minori.", then: c => c.Foe(s => s.PriorityPlayerId == TutorialRunner.Seat && s.PendingChoice is null), highlight: Foe(TutorialCards.KitsukiMinori), glow: Battle),
        Do("pass-battle", "Winning the battle", "The Dragon player has passed, so you may pass and win the battle. Kitsuki Minori is bowed and adds nothing, so her army is destroyed.", "Click Pass battle action.", Move("pass"), then: c => c.Foe(), glow: Battle),
        Do("overconfident-1", "Overconfident", "Kitsuki Minori is Overconfident: after a card with Overconfident leaves play, each player other than its owner may draw a card. Choose to draw a card.",
            "Click Draw 1 card.", Move("overconfidentDraw"), accept: (_, command) => command.Choice == "draw", hint: "Choose to draw a card.",
            then: c => { c.Foe(s => s.Phase == Phase.Battle && s.PriorityPlayerId == TutorialRunner.Seat); c.Auto("passAllBattles"); }, glow: HandGlow),
        Note("dragon-dynasty", "The Dragon's Dynasty phase", "On to the Dragon's Dynasty phase. They recruit the other Kitsuki Minori.", then: c => c.Foe(), highlight: FoeProvinces(0)),
        Do("overconfident-2", "Overconfident burns both ways", "Kitsuki Minori is cheap, but when an Overconfident card enters play, each player other than its controller may draw a card. Choose to draw a card.",
            "Click Draw 1 card.", Move("overconfidentDraw"), accept: (_, command) => command.Choice == "draw", hint: "Choose to draw a card.", then: c => c.Reset(ExperienceTable, ExperienceOpening), glow: HandGlow),

        // ---- Scorpion (you) against Crane: Kharmic, Experienced and Inexperienced, Duelist -------------------------------------------------------------------------------------------------------------
        Note("experience-intro", "Experience", "Next, we'll learn about Experience. L5R is a living game: Personalities grow and gain new skills and abilities. It isn't always limited to Unique Personalities, but most Personalities with the Experienced keyword are Unique.",
            highlight: Own(TutorialCards.BayushiNitoshiInexperienced)),
        Note("inexperienced", "Inexperienced", "Point at Bayushi Nitoshi in play. He has the Inexperienced keyword, the lowest level of Experience a Personality may have.", highlight: Own(TutorialCards.BayushiNitoshiInexperienced)),
        Note("no-experience", "No Experience keyword", "Point at the Bayushi Nitoshi in your Provinces. He has neither Inexperienced nor Experienced. He is more Experienced than Bayushi Nitoshi Inexperienced.",
            highlight: OwnProvinces(1)),
        Do("kharmic", "Kharmic", "That Traveling Market isn't helping, and it has the Kharmic keyword. Every player may, as a Repeatable Limited action, pay 2 Gold to discard a Kharmic card from their hand and draw a card, or discard a face-up Kharmic card from their Province and refill that Province face-up. Use the Province one now.",
            "Click Traveling Market, then click Kharmic.", Ability("kharmic-discard"), hint: "Discard Traveling Market with Kharmic.", highlight: OwnProvinces(0)),
        Do("to-dynasty", "Experienced", "A new card appeared face-up in the Province: Bayushi Nitoshi with the Experienced keyword. Of the three, he is the most Experienced version of Bayushi Nitoshi. Pass to end the Action phase and skip to the Dynasty phase.",
            "Click Pass action.", Move("pass"), then: c => { c.Foe(s => s.Phase != Phase.Action); c.Auto("pass"); }, highlight: OwnProvinces(0), glow: Bar),
        Note("unique-titles", "Unique and Experience", "If you noticed that each Bayushi Nitoshi is Unique and has the same title, and thought, \"Isn't this illegal?\", you're on to something. But Unique cards with the same title may be in one deck if each copy has a different Experience level.",
            highlight: All(Own(Nitoshis))),
        Note("one-in-play", "Only one in play", "You might also be thinking, \"Surely I can't have more than one of these in play at a time.\" You would be correct.", highlight: All(Own(Nitoshis))),
        Do("overlay", "Overlay", "However, if one dies, you may bring another into play. You may also upgrade to a more Experienced version by Overlaying it onto a less Experienced version. You may skip levels. Overlay one of your more Experienced Bayushi Nitoshis on the Inexperienced one.",
            "Click a Bayushi Nitoshi in your Provinces, then click Overlay.", Move("overlay"), hint: "Overlay a Bayushi Nitoshi onto the Inexperienced one.", then: c => c.HoldReveal(1, 0, after: true), highlight: All(Own(Nitoshis))),
        Do("end-dynasty", "Overlay costs nothing", "When you Overlay, you pay nothing. The old Personality is removed from the game and completely replaced by the new one. End your Dynasty phase.", "Click End Dynasty Phase.", Move("endDynasty"), glow: Bar),
        Note("hakuseki", "Experienced 2", "In the Crane player's Events phase, Doji Hakuseki is revealed. Some Personalities with Experienced have a number next to it. Doji Hakuseki is Experienced 2.",
            highlight: FoeProvinces(0)),
        Note("experience-order", "The order of Experience", "The order of increasing Experience is Inexperienced, then no Experience keyword, then Experienced, then Experienced 2 and up. You may Overlay onto a Personality with the same title as long as the Experience level of the Overlaying Personality is higher.",
            then: c => c.Resume(), highlight: FoeProvinces(0)),
        Note("duelist", "Duelist", "We almost forgot the Duelist keyword. Duelists win ties in duels and have a second special ability. We'll cover that in the Dueling tutorial. Doji Makoto and Doji Hakuseki are Duelists, and so is Kitsuki Minori. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu", highlight: Foe(TutorialCards.DojiMakoto)),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    private static readonly TutorialHomeCard[] PhoenixHome =
        [new(TutorialCards.ProsperousVillage), new(TutorialCards.SmallFarm), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.SilverMine), new(TutorialCards.TemplesOfGiseiToshi), new(TutorialCards.SilkWorks)];
    private static readonly string[] PhoenixProvinces = [TutorialCards.IsawaSanetomo, TutorialCards.TravelingPeddler, TutorialCards.TravelingPeddler, TutorialCards.AsakoRikateExp];

    /// <summary>Phoenix (you, Library of Rebirth) and Dragon (Foothills Keep) in a mirror: Isawa Sanetomo, two Traveling Peddlers (Shinden Shorai on the second's Province) and Asako Rikate (Experienced) in the Provinces;
    /// Prosperous Village, two Small Farms, Large Farm, Silver Mine, Temples of Gisei Toshi and Silk Works in play; Imperial Gift in the Dynasty discard pile; Renewal, Blades of the Black Dragon, Elite Sentry, Ring of Water and Hanabi in hand.</summary>
    private static TutorialSetup UniqueTable => new(
        new("Phoenix", TutorialCards.LibraryOfRebirth, PhoenixProvinces, UniqueInHand, PhoenixHome, Honor: 8, Deck: Filler("Phoenix", TutorialCards.LibraryOfRebirth, [TutorialCards.SmallFarm], [TutorialCards.Renewal]),
            DynastyDiscard: [TutorialCards.ImperialGift], Regions: [(2, TutorialCards.ShindenShorai)]),
        new("Dragon", TutorialCards.DragonStronghold, PhoenixProvinces, UniqueInHand, PhoenixHome, Honor: 8, Deck: Filler("Dragon", TutorialCards.DragonStronghold, [TutorialCards.SmallFarm], [TutorialCards.Renewal]),
            DynastyDiscard: [TutorialCards.ImperialGift], Regions: [(2, TutorialCards.ShindenShorai)]),
        TurnsTaken: 3, NoEndDraw: true);

    private static void UniqueOpening(TutorialContext c) { c.Draw(5); c.Engine.TutorialDraw(1, 5); c.StartTurn(); c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); c.Auto("pass"); }

    /// <summary>Scorpion (you, The Towers of the Yogo): Bayushi Wateru and Yogo Takashi in the Provinces, Floating Reserve and Thick Marsh in hand, the top of the Fate deck Block Supply Lines, a Focus and the Celestial Sword of the
    /// Dragon (Experienced). Dragon (Foothills Keep): Togashi Noboru and Kitsuki Minori in play, another Kitsuki Minori in a Province, A Stain Cleansed in hand.</summary>
    private static TutorialSetup DestinedTable => new(
        new("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.BayushiWateru, TutorialCards.YogoTakashi, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.FloatingReserve, TutorialCards.ThickMarsh, TutorialCards.BlockSupplyLines, TutorialCards.Focus, TutorialCards.CelestialSwordOfTheDragonExp],
            [new(TutorialCards.GeishaHouse), new(TutorialCards.RedCraneDojo), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.ProsperousVillage), new(TutorialCards.SilkWorks)],
            Honor: 8, Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        new("Dragon", TutorialCards.DragonStronghold, [TutorialCards.KitsukiMinori, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm], [TutorialCards.AStainCleansed],
            [new(TutorialCards.KitsukiMinori), new(TutorialCards.TogashiNoboru), new(TutorialCards.GoldMine), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.ProsperousVillage)],
            Honor: 8, Deck: Filler("Dragon", TutorialCards.DragonStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void DestinedOpening(TutorialContext c) { c.Draw(2); c.Engine.TutorialDraw(1, 1); c.StartTurn(); c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); c.Auto("pass"); }

    /// <summary>Scorpion (you, The Towers of the Yogo) with Bayushi Nitoshi (Inexperienced) in play, a Traveling Market and Bayushi Nitoshi in the Provinces and Bayushi Nitoshi (Experienced) next in the Dynasty deck.
    /// Crane (Shinden Asahina): Doji Makoto in play, Doji Hakuseki (Experienced 2) in its first Province.</summary>
    private static TutorialSetup ExperienceTable => new(
        new("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.TravelingMarket, TutorialCards.BayushiNitoshi, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.BayushiNitoshiExp], [],
            [new(TutorialCards.BayushiNitoshiInexperienced), new(TutorialCards.GeishaHouse), new(TutorialCards.Slanderer), new(TutorialCards.SilkWorks), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm),
                new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters)],
            Honor: 10, Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        new("Crane", TutorialCards.FoeStronghold, [TutorialCards.DojiHakusekiExp2, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm], [],
            [new(TutorialCards.Marketplace), new(TutorialCards.DamagedPort), new(TutorialCards.ShelteredPort), new(TutorialCards.SecondCityPort), new(TutorialCards.DojiMakoto), new(TutorialCards.BorderKeep)],
            Honor: 8, Deck: Filler("Crane", TutorialCards.FoeStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void ExperienceOpening(TutorialContext c) { c.StartTurn(); }

    /// <summary>The Dragon attacks the Province with Bayushi Wateru, and recruits the second Kitsuki Minori with the Clan Discount (3 Gold: Large Farm and Small Farm).</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [],
        Purchases: [new(TutorialCards.KitsukiMinori, true, false, [TutorialCards.LargeFarm, TutorialCards.SmallFarm])], AttackProvince: 0);

    public static readonly TutorialLesson Lesson = new("keywords-2", "Keywords 2", UniqueTable, Steps, FoePlays, Opening: UniqueOpening);
}
