namespace L5R.Core;

/// <summary>The Duels lesson (curriculum id <c>special-duels</c>): Crane (you, Shinden Asahina) against Scorpion (Law of Darkness Dojo). It plays two duels. In the first, Doji Makoto issues a Sanctioned Duel that Bayushi Nitoshi
/// refuses, then Reckless Duel forces one: the lesson reads the duel flow (focus, Strike, the Duelist's swap, Focus Effects, the winner) and ends it on a clear Crane win while The Loyalty of the Scorpion delays Nitoshi's loss.
/// In the second, Makoto's own Battle ability ends in a tie that Makoto wins because he is a Duelist. The engine plays every rule: the script only chooses what is offered and has the opponent make exactly the plays the notes
/// describe. TutorialDuelsTests checks every number the words name.</summary>
internal static class TutorialLessonDuels
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> Focused(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Focused));
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

    public static readonly TutorialStep[] Steps =
    [
        Note("welcome", "Duels", "Welcome to the training on Duels. A duel is a mini-game inside L5R. When a challenge is issued, the game pauses until the challenge, and any duel it leads to, has been resolved.",
            highlight: Own(TutorialCards.DojiMakoto, TutorialCards.BayushiNitoshi)),
        Note("challenge", "Challenges", "A duel starts when one Personality issues a challenge to a Personality controlled by another player. Some challenges may be refused. If a card doesn't say the challenge may be refused, the duel happens even if the challenged player wishes they could say no (they usually do!).",
            highlight: Own(TutorialCards.DojiMakoto, TutorialCards.BayushiNitoshi)),
        Note("plan", "Your Action phase", "You are the Crane player now, against Scorpion, and it is your Action phase. You would like to attack the Scorpion, but Bayushi Nitoshi is dangerous. It would be better if he couldn't defend.",
            highlight: Foe(TutorialCards.BayushiNitoshi)),
        Note("stakes", "The stakes", "Like a battle, a duel has a consequence for winning or losing. Mouse over Sanctioned Duel: the loser is destroyed. High risk, high reward.",
            highlight: Hand(TutorialCards.SanctionedDuel), showDefinition: TutorialCards.SanctionedDuel),
        Do("sanctioned", "Sanctioned Duel", "Issue a challenge from Doji Makoto to Bayushi Nitoshi with Sanctioned Duel.", "Click Sanctioned Duel, then click Bayushi Nitoshi, then click Play Sanctioned Duel.",
            Ability("sanctioned-duel"), accept: (look, command) => look.Is(command.PerformerId, TutorialCards.DojiMakoto) && look.Is(command.TargetId, TutorialCards.BayushiNitoshi),
            hint: "Use Sanctioned Duel with Doji Makoto against Bayushi Nitoshi.", highlight: All(Hand(TutorialCards.SanctionedDuel), Own(TutorialCards.DojiMakoto), Foe(TutorialCards.BayushiNitoshi)),
            then: c => c.FoeDuel("declineDuel")),
        Note("refused", "Refused", "Sanctioned Duel may be refused. Your opponent knows that Dueling decks don't usually lose duels, so Bayushi Nitoshi refused. The cost of refusing: he is dishonored, and you gain 2 Honor.\n\nPredictable, cowardly behavior from a Scorpion, really.",
            highlight: Foe(TutorialCards.BayushiNitoshi), glow: [TutorialGlow.Honor]),
        Do("attack", "Declare an attack", "Now pass your action and declare an attack. You can deal with Nitoshi in the battle, if it comes to that.", "Click Pass action.", Move("pass"),
            then: c => { c.Foe(s => s.Phase != Phase.Action); c.Auto("declareAttack"); }, glow: Bar),
        Do("assign", "Attack a Province", "Assign Doji Makoto to attack any Province. Bayushi Nitoshi will defend against him.", "Click Doji Makoto, then click any Province, then click Confirm assignments.", Move("attack"),
            hint: "Assign Doji Makoto to attack a Province.", highlight: Own(TutorialCards.DojiMakoto),
            then: c => { c.Foe(s => s.Phase == Phase.BattleSelection); c.AutoSelectBattle(); c.FoeUse("law-of-darkness-weaken", card: TutorialCards.LawOfDarknessDojo, target: TutorialCards.DojiMakoto); }),
        Do("reckless", "Reckless Duel", "The Scorpion used the Law of Darkness Dojo to give Doji Makoto -4 Force. They are now winning on Force, but that's okay: Duels rarely depend on Force.\n\nIt's time to get that slippery Scorpion into a proper duel. Use Reckless Duel to make Nitoshi face Makoto!",
            "Click Reckless Duel, then click Bayushi Nitoshi, then click Play Reckless Duel.", Ability("reckless-duel"),
            accept: (look, command) => look.Is(command.PerformerId, TutorialCards.DojiMakoto) && look.Is(command.TargetId, TutorialCards.BayushiNitoshi), hint: "Use Reckless Duel with Doji Makoto against Bayushi Nitoshi.",
            highlight: All(Hand(TutorialCards.RecklessDuel), Own(TutorialCards.DojiMakoto), Foe(TutorialCards.BayushiNitoshi)), glow: Battle),
        Note("accepted", "The duel begins", "A challenge with no option to refuse must be accepted, so the duel begins. We'll take you through this duel from start to finish.", glow: Battle),
        Note("compare", "A contest of totals", "In a duel, two Personalities each add a duel stat to the Focus Value of the cards they focus. Whoever has the higher total wins, and the other loses.", glow: Battle),
        Note("chi", "The duel stat", "A standard duel compares Chi, the number in the top right of a Personality. Reckless Duel doesn't name another stat, so this duel uses Chi. A duel may compare Force or Personal Honor instead: the card that creates it says so in its text.", glow: Battle),
        Note("focus-strike", "Focus or Strike", "On your turn in a duel you choose to Focus or Strike.\n\nTo Focus, put a card face down in your focusing area, from your hand or the top of your Fate deck. Only you may look at it, and it isn't played, so its cost and effects don't matter.\n\nTo Strike, you stop focusing for both players and every focused card is revealed.", glow: Battle),
        Note("focus-value", "Focus Value", "A card you focus adds its Focus Value to your Personality's total at resolution. A card's Focus Value is the number at the bottom center of the card.", glow: Battle),
        Note("focus-four", "Up to four", "Each player may Focus up to four times before Striking. Focusing is never required: you may Strike straight away.", glow: Battle,
            then: c => c.FoeDuel("focus", TutorialCards.LoyaltyOfTheScorpion)),
        Note("first", "Who goes first", "The challenged Personality gets the first opportunity to Focus or Strike. Bayushi Nitoshi chose to Focus: he put a card from his hand face down. You can't see it.", glow: Battle),
        Do("focus-courtesy", "Focus a card", "Doji Makoto has 6 Chi and Bayushi Nitoshi has 5 Chi. That is close, so Striking isn't safe. Focus The Courtesy of the Crane.", "Click The Courtesy of the Crane, then click PLACE FOCUS.",
            (look, command) => command.Type == "focus" && look.Is(command.CardId, TutorialCards.CourtesyOfTheCrane), hint: "Focus The Courtesy of the Crane.", highlight: Hand(TutorialCards.CourtesyOfTheCrane), glow: Battle),
        Note("focused-four", "A Focus Value of 4", "You have focused a card with a Focus Value of 4, so Doji Makoto's total at resolution will be 6 + 4 = 10. Inspect your focused card.\n\nThe Courtesy of the Crane has a Focus Effect. Read it.",
            highlight: Focused(TutorialCards.CourtesyOfTheCrane), glow: Battle, showDefinition: TutorialCards.CourtesyOfTheCrane),
        Note("focus-effects", "Focus Effects", "Focus Effects resolve once the focused cards are revealed. Some are conditional, on winning or losing the duel. Others apply whoever wins. Most add something to the outcome, like giving your Provinces strength if you win, but a few can change it.\n\nThe Comprehensive Rules resolve every Focus Effect before the totals are counted. This game resolves the ones that change Focus Values or duel stats first, and the ones that depend on the outcome, like this one, once the outcome is known.",
            highlight: Focused(TutorialCards.CourtesyOfTheCrane), glow: Battle, then: c => c.FoeDuel("focus", choice: "deck")),
        Do("focus-deck", "Focus from your deck", "Bayushi Nitoshi has focused again, this time with the top card of his Fate deck instead of a card from his hand. You can do that too. You may inspect the card once it is focused, but you are stuck with whatever you draw.\n\nFocus the top card of your Fate deck.",
            "Click your Fate deck, then click PLACE FOCUS.", (_, command) => command.Type == "focus" && command.Choice == "deck", hint: "Focus from the top of your Fate deck.", glow: Battle),
        Note("strike", "Nitoshi strikes", "You drew A Stain Cleansed, a Focus Value of 1. Not great! Nitoshi has focused twice and feels this is his best shot, so he Strikes.\n\nFocused cards are revealed, the totals are counted, the winner is decided, and then Focus Effects resolve. Are you ready to find out?",
            highlight: Focused(TutorialCards.CourtesyOfTheCrane, TutorialCards.AStainCleansed), glow: Battle, then: c => c.FoeDuel("strike"), button: "Continue"),
        Do("duelist", "Duelist", "The game has paused for you because Doji Makoto is a Duelist. A Duelist's controller may discard one of their focused cards and focus another, from their hand or the top of their Fate deck, before Focus Effects resolve.\n\nRight now Makoto has a 4 and a 1 focused, for a total of 11. Nitoshi has a 2 and a 5, for 12. You are going to lose the duel unless you do something.\n\nReplace A Stain Cleansed with your Retribution.",
            "Click A Stain Cleansed, then click Retribution, then click Replace from selected hand card.", Move("duelistRefocus"),
            accept: (look, command) => look.Is(command.CardId, TutorialCards.AStainCleansed) && look.Is(command.TargetId, TutorialCards.Retribution), hint: "Replace A Stain Cleansed with Retribution.",
            highlight: All(Focused(TutorialCards.AStainCleansed), Hand(TutorialCards.Retribution)), glow: Battle,
            then: c => { c.Auto("orderDuelFocusEffects"); c.FoeDecide("delay"); }),
        Note("outcome", "The outcome", "Doji Makoto has 6 + 4 + 4 = 14. Bayushi Nitoshi has 5 + 2 + 5 = 12. The higher total wins the duel and the other Personality loses: Makoto wins!", glow: Battle),
        Note("consequences", "Consequences", "Then the consequences are applied. Because you won, The Courtesy of the Crane's Focus Effect gives all your Provinces +2 strength. Reckless Duel bows the loser and moves him home, but The Loyalty of the Scorpion's Focus Effect delayed the effects of losing until after the next Dynasty Phase begins. Nitoshi is still in the battle.",
            glow: Battle, then: c => c.Foe(s => s.PriorityPlayerId == TutorialRunner.Seat)),
        Do("makoto-duel", "Makoto's Battle ability", "It is your turn again, and if you don't do something drastic, Bayushi Nitoshi is going to crush you when the battle resolves. Doji Makoto has a Battle ability, and the stakes are much higher: the loser is destroyed.\n\nUse it to duel Nitoshi and rid yourself of this slippery foe once and for all!",
            "Click Doji Makoto, then click Bayushi Nitoshi, then click Use Doji Makoto.", Ability("makoto-duel"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiNitoshi),
            hint: "Use Doji Makoto's Battle ability against Bayushi Nitoshi.", highlight: All(Own(TutorialCards.DojiMakoto), Foe(TutorialCards.BayushiNitoshi)), glow: Battle,
            then: c => c.FoeDuel("focus", TutorialCards.VeteranSkirmishers)),
        Do("reserve", "Reserve Weapon", "Nitoshi focused Veteran Skirmishers from his hand. You have no cards in your hand, so you would have to focus the top card of your Fate deck. Or would you?\n\nMouse over Reserve Weapon. It lets you give the Personality it is attached to +3 to his duel stat instead of focusing. Use it.",
            "Click the +3 button.", (_, command) => command.Type == "focus" && command.Choice == "reserve", hint: "Use Reserve Weapon instead of focusing.", highlight: Own(TutorialCards.ReserveWeapon), glow: Battle,
            then: c => c.FoeDuel("focus", choice: "deck")),
        Do("strike-2", "Strike!", "Nitoshi focused the top card of his Fate deck. You are feeling impetuous, and bet that the Scorpion's deck is full of low Focus Values. Strike!",
            "Click Strike.", Move("strike"), hint: "Strike.", glow: Battle, then: c => c.Auto("duelistRefocus")),
        Note("tie", "A tie", "Makoto has 6 + 3 = 9 and Nitoshi has 5 + 3 + 1 = 9. It's a tie!\n\nWhen the totals are tied, nobody wins and both Personalities lose, unless exactly one of them is a Duelist: then the Duelist wins. Makoto is a Duelist and Nitoshi is not, so Makoto wins. This time nothing delays the loss, so Bayushi Nitoshi is destroyed immediately.",
            highlight: Own(TutorialCards.DojiMakoto), glow: Battle),
        Note("congratulations", "Congratulations", "Congratulations! You now know how challenges are issued, how duels are played, and what the Duelist keyword does. Select the Next Tutorial to continue learning.", button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Crane (you, Shinden Asahina, 11 Honor): Sheltered Port, Marketplace, Small Farm, Large Farm, Border Keep and The Daidoji Marketplace; Doji Makoto with Reserve Weapon; Sanctioned Duel, Retribution, The Courtesy of the Crane
    /// and Reckless Duel in hand, A Stain Cleansed on top of the Fate deck. Scorpion (Law of Darkness Dojo, its starting Honor): two Geisha Houses, Small Farm, Large Farm and Prosperous Village; Bayushi Nitoshi with Veteran Skirmishers;
    /// The Loyalty of the Scorpion and a Veteran Skirmishers in hand, Focus then Crushing Strength on top of the Fate deck.</summary>
    private static TutorialSetup Table => new(
        new("Crane", TutorialCards.FoeStronghold, [TutorialCards.SmallFarm], [TutorialCards.SanctionedDuel, TutorialCards.Retribution, TutorialCards.CourtesyOfTheCrane, TutorialCards.RecklessDuel, TutorialCards.AStainCleansed],
            [new(TutorialCards.ShelteredPort), new(TutorialCards.Marketplace), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.BorderKeep), new(TutorialCards.DaidojiMarketplace),
             new(TutorialCards.DojiMakoto, Attachments: [TutorialCards.ReserveWeapon])], Honor: 11,
            Deck: Filler("Crane", TutorialCards.FoeStronghold, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        new("Scorpion", TutorialCards.LawOfDarknessDojo, [TutorialCards.SmallFarm], [TutorialCards.LoyaltyOfTheScorpion, TutorialCards.VeteranSkirmishers, TutorialCards.Focus, TutorialCards.CrushingStrength],
            [new(TutorialCards.GeishaHouse), new(TutorialCards.GeishaHouse), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.ProsperousVillage),
             new(TutorialCards.BayushiNitoshi, Attachments: [TutorialCards.VeteranSkirmishers])],
            Deck: Filler("Scorpion", TutorialCards.LawOfDarknessDojo, [TutorialCards.SmallFarm], [TutorialCards.Focus])),
        TurnsTaken: 3, NoEndDraw: true);

    private static void Opening(TutorialContext c) { c.Draw(4); c.Engine.TutorialDraw(1, 2); c.StartTurn(); }

    /// <summary>Scorpion only ever plays what a step asks of it.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], []);

    public static readonly TutorialLesson Lesson = new("special-duels", "Special Actions - Duels", Table, Steps, FoePlays, Opening: Opening);
}
