namespace L5R.Core;

/// <summary>The Imperial Favor lesson (curriculum id <c>imperial-favor</c>), from Mendon's script: Favors (you, Kyuden Otomo - exp with two Favor tokens, 13 Honor) against Dragon Destruction (Monastery of New Thought, 8 Honor),
/// from your Action phase into the Dragon's attack on your Province 1. Kakita Nara Lobbies; the Favor's Limited ability trades The Empress' Address for Moksha Patam; Dazzling Attire takes the Favor back. On the Dragon's turn
/// one Daidoji Sosuke discards the Favor to bow Mirumoto Yumaru's Unparalleled Sword, Doji Dainagon Lobbies as an Open action and Kyuden Otomo turns the Favor into a token, the second Sosuke pays with a token to bow Togashi
/// Hiroto, and in the battle the Favor's Battle ability, paid with a token, sends Mirumoto Gobashi home. Corrections to the script (R-097: holding the Favor bars Lobby, so Sosuke spends it first; R-098: Dainagon's Open Lobby
/// shares Lobby's once-per-turn use) are listed in docs/TUTORIAL.md. TutorialFavorTests checks every number the words name.</summary>
internal static class TutorialLessonFavor
{
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;
    private static Func<TutorialLookup, GameCommand, bool> Move(string type) => (_, command) => command.Type == type;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces(params int[] indexes) => look => indexes.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow };

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

    private static readonly string[] FavorGlow = [TutorialGlow.Favor], Battle = [TutorialGlow.BattleBar];
    private const int Foe0 = 1 - TutorialRunner.Seat;

    /// <summary>The move names this card as its performer, its source or its target (abilities differ in which field carries the Personality who bows).</summary>
    private static bool Names(TutorialLookup look, GameCommand command, string definition) =>
        look.Is(command.PerformerId, definition) || look.Is(command.CardId, definition) || look.Is(command.TargetId, definition);
    private static bool PaysWithToken(GameCommand command) => command.CostOption == MatchEngine.FavorTokenCostOption;

    public static readonly TutorialStep[] Steps =
    [
        Note("favor-welcome", "The Imperial Favor", "Welcome to the tutorial on the Imperial Favor.\n\nIn this tutorial you'll learn how to Lobby for the Imperial Favor and how to use it to your advantage."),
        Note("player-abilities", "Abilities every player has", "By now you'll know that every player has abilities of their own, such as Tactical Advantage and Order Seppuku, that are always available whenever their conditions are met. Lobbying for the Imperial Favor and using it work the same way: they are abilities of the player, not of any card."),
        Note("about-favor", "The Favor", "A few things about the Imperial Favor:\n\n• There's only one, and only one player may hold it at a time.\n• It starts the game held by no one.\n• A player may take it even while another player holds it.\n• Each ability that takes or uses it may be used once per turn, like most abilities in L5R. The Favor itself has no limit: whenever you hold it, you may discard it to pay any cost that asks for it.",
            glow: FavorGlow),
        Note("lobby-rules", "Lobbying", "To Lobby for the Imperial Favor, your Family Honor must be higher than every other player's; a tie isn't enough. Lobby is a Political Limited action, so you may only take it in your own Action phase: bow one of your Personalities with 1 or more Personal Honor to take the Imperial Favor.\n\nOne more condition: you may not Lobby while you already hold the Favor.",
            glow: [TutorialGlow.Honor]),
        Do("lobby-nara", "Lobby", "You have 13 Honor and the Dragon have 8, so you may Lobby for the Imperial Favor.\n\nUse Kakita Nara to Lobby. She has 3 Personal Honor.",
            "Click Kakita Nara, then click \"Lobby · bow a Personality to take Favor\". Choose Kakita Nara, then click Use ability.", Ability("lobby"), accept: (look, command) => Names(look, command, TutorialCards.KakitaNara),
            hint: "Lobby with Kakita Nara.", highlight: Own(TutorialCards.KakitaNara), targets: Own(TutorialCards.KakitaNara), then: c => c.DeclineYourReaction(), glow: [TutorialGlow.Honor]),
        Note("favor-abilities", "Now what?", "You've taken the Imperial Favor: it sits on your side of the table. The Favor has no actions of its own; holding it lets you use two abilities every player has:\n\nFavor Political Limited: Discard the Imperial Favor and a card: Draw a card.\n\nFavor Political Battle: Discard the Imperial Favor and target an attacking enemy Personality: Move him home.",
            glow: FavorGlow),
        Note("favor-battle-rules", "Sending an attacker home", "The Battle ability sends an attacking enemy Personality home; it can't send home your own attackers. Like any Battle action, you may only take it while you have a unit at the battle, and the Personality you target must be attacking there.",
            glow: FavorGlow),
        Do("favor-cycle", "Discard and draw", "The Favor's Limited ability discards a card from your hand and draws a new one.\n\nDiscard the Imperial Favor and The Empress' Address to draw a card.",
            "Click The Empress' Address in your hand, then click \"Discard Favor and a card · draw a card\". Choose The Empress' Address, then Discard the Imperial Favor (not a Favor token), and click Confirm choice.",
            Ability("favor-cycle"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.TheEmpressAddress) && !PaysWithToken(command),
            hint: "Discard the Imperial Favor and The Empress' Address to draw a card.", highlight: Hand(TutorialCards.TheEmpressAddress), targets: Hand(TutorialCards.TheEmpressAddress), glow: FavorGlow),
        Do("dazzling-attire", "Taking it back", "You drew Moksha Patam. Looking at that line of Dragon Personalities, you may want the Favor again to send one of them home. You've already Lobbied this turn, and Lobby is once per turn, but other cards take the Favor too.\n\nDoji Dainagon wears Dazzling Attire: \"Political Open, Bow this card: Take the Imperial Favor.\" Use it.",
            "Click Dazzling Attire on Doji Dainagon, then click Use Dazzling Attire.", Ability("dazzling-attire-favor"),
            hint: "Use Dazzling Attire to take the Imperial Favor.", highlight: Own(TutorialCards.DazzlingAttire), then: c => c.DeclineYourReaction(), glow: FavorGlow),
        Note("end-turn", "Ending your turn", "That's better. Now finish your turn: you'll pass your Action phase, make no attack, and end your Dynasty phase. You'll draw a card in your End phase.",
            button: "End Turn", glow: [TutorialGlow.PhaseBar],
            then: c =>
            {
                c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); c.Auto("pass"); c.Auto("endDynasty");
                c.Foe(s => s.ActivePlayerId == Foe0 && s.Phase == Phase.Action && s.PriorityPlayerId == Foe0);
                c.FoeUse("deadly-discipline-tokens", performer: TutorialCards.TogashiHiroto, card: TutorialCards.DeadlyDiscipline);
            }),
        Note("dragon-turn", "The Dragon's turn", "It's the Dragon's Action phase. They played Deadly Discipline to give Togashi Hiroto two more +1F Fire tokens, and now it's your opportunity to act.\n\nLimited actions are only for your own Action phase, but Open actions may be taken in any player's Action phase.",
            highlight: Foe(TutorialCards.TogashiHiroto), glow: [TutorialGlow.PhaseBar]),
        Note("dainagon-open", "Doji Dainagon", "Doji Dainagon reads: \"Dainagon may Lobby as an Open action.\" She may Lobby now, on the Dragon's turn. It's a new turn, so Lobby's once-per-turn limit has reset.\n\nBut you still hold the Imperial Favor, and you may not Lobby while you hold it. Spend it first.",
            highlight: Own(TutorialCards.DojiDainagon), glow: FavorGlow),
        Do("sosuke-sword", "Raising tariffs", "The Dragon look menacing; let's take away their bite. Mirumoto Yumaru's Unparalleled Sword gives her a Melee 4 Attack.\n\nDaidoji Sosuke: \"Political Open: Discard the Imperial Favor and target a Personality: Bow a card without attachments in the target's unit.\" Yumaru carries the Sword, so she can't be chosen, but the Sword has no attachments. A bowed card's abilities can't be used.",
            "Click a Daidoji Sosuke, then choose Mirumoto Yumaru. Choose Discard the Imperial Favor (not a Favor token), then click Confirm choice. The Sword is the only card in her unit without attachments, so it bows.",
            Ability("sosuke-tariffs"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.MirumotoYumaru) && !PaysWithToken(command),
            hint: "Use a Daidoji Sosuke on Mirumoto Yumaru, discarding the Imperial Favor.", highlight: All(Own(TutorialCards.DaidojiSosuke), Foe(TutorialCards.MirumotoYumaru, TutorialCards.UnparalleledSword)),
            targets: Foe(TutorialCards.MirumotoYumaru), glow: FavorGlow),
        Do("dainagon-lobby", "Lobby as an Open action", "The Sword is bowed, and you no longer hold the Imperial Favor. Your 13 Honor is still higher than the Dragon's 8, so Doji Dainagon may Lobby.",
            "Click Doji Dainagon, then click Use Doji Dainagon.", Ability("dainagon-lobby"),
            hint: "Lobby with Doji Dainagon.", highlight: Own(TutorialCards.DojiDainagon), glow: [TutorialGlow.Honor]),
        Do("otomo-token", "Kyuden Otomo", "You took the Imperial Favor, and your Stronghold, Kyuden Otomo, reacts: \"After you take the Imperial Favor: You may discard it; if you did, add a Favor token to this card.\"\n\nTake the Reaction: discard the Favor and add a third Favor token.",
            "Click Use Kyuden Otomo, then Play Reaction.", Ability("otomo-favor-token"),
            hint: "Use Kyuden Otomo's Reaction.", highlight: Own(TutorialCards.KyudenOtomoExp)),
        Note("otomo-tokens", "Favor tokens", "Kyuden Otomo doesn't take the Favor instead of you: you took it, then traded it for a token. Its trait reads: \"You may substitute discarding a Favor token from this card for discarding the Imperial Favor as a cost.\" Each token pays any cost that discards the Favor. It also has a Political Open ability that bows one of your honorable Courtiers to add a token.\n\nFavor tokens are not the Favor: holding them doesn't stop you from Lobbying.",
            highlight: Own(TutorialCards.KyudenOtomoExp)),
        Do("sosuke-hiroto", "Paying with a token", "Togashi Hiroto can do dangerous things too: his Battle ability bows an enemy card without attachments. Use your other Daidoji Sosuke on him, paying with a Favor token. Hiroto's Fire tokens aren't attachments, so he is a card without attachments in his own unit.\n\nUsing Sosuke's ability twice this turn is fine: each Sosuke is a separate card with its own ability, and each is used once. One Sosuke couldn't use his twice.",
            "Click your other Daidoji Sosuke, then choose Togashi Hiroto. Choose Discard a Favor token instead, then click Confirm choice.",
            Ability("sosuke-tariffs"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.TogashiHiroto) && PaysWithToken(command),
            hint: "Use your other Daidoji Sosuke on Togashi Hiroto, paying with a Favor token.", highlight: All(Own(TutorialCards.DaidojiSosuke, TutorialCards.KyudenOtomoExp), Foe(TutorialCards.TogashiHiroto)),
            targets: Foe(TutorialCards.TogashiHiroto)),
        Do("pass-dragon", "The Dragon attack", "There isn't much more to do, so let's see what the Dragon player does next.",
            "Click Pass action.", Move("pass"), then: c => c.Foe(s => s.Phase == Phase.DefenseAssignment && s.PriorityPlayerId == TutorialRunner.Seat), glow: [TutorialGlow.PhaseBar]),
        Do("favor-defend", "Defend", "The Dragon attacked your Province 1 with Mirumoto Gobashi. Kakita Nara and Doji Dainagon bowed to Lobby, so your two Daidoji Sosuke are the ones who can defend.\n\nDefend with one or both of them. This will be quick.",
            "Drag a Daidoji Sosuke to your Province 1 (or click him, then the Province), then click Confirm assignments.",
            (_, command) => command.Type == "assignDefense" && (command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == 0) : command.Province is null or 0),
            accept: (look, command) => command.Assignments is { Count: > 0 } assignments && assignments.Keys.All(id => look.Is(id, TutorialCards.DaidojiSosuke)),
            hint: "Defend your Province 1 with one or both Daidoji Sosuke.", then: c => c.Foe(s => s.Phase == Phase.Battle && s.PriorityPlayerId == TutorialRunner.Seat),
            highlight: All(Own(TutorialCards.DaidojiSosuke), OwnProvinces(0), Foe(TutorialCards.MirumotoGobashi)), glow: Battle, onlyProvinces: [0]),
        Do("favor-home", "Send him home", "Defenders act first. You have a unit here and Gobashi is attacking: send him home with the Favor's Battle ability. You don't hold the Favor, but a Favor token pays for it.",
            "Click \"Discard Favor · move an enemy attacker home\", choose Mirumoto Gobashi, choose Discard a Favor token instead, then click Confirm choice.",
            Ability("favor-battle"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.MirumotoGobashi) && PaysWithToken(command),
            hint: "Use the Favor's Battle ability on Mirumoto Gobashi, paying with a Favor token.", highlight: All(Foe(TutorialCards.MirumotoGobashi), Own(TutorialCards.KyudenOtomoExp)),
            targets: Foe(TutorialCards.MirumotoGobashi), glow: Battle, then: c => c.Foe(s => s.PriorityPlayerId == TutorialRunner.Seat)),
        Do("favor-resolve", "An empty battlefield", "Gobashi has gone home and the Dragon have no one left at the battle. The battle resolves once both players pass.",
            "Click Pass battle action.", Move("pass"), glow: Battle, then: c => c.Foe(s => s.Phase != Phase.Battle)),
        Note("favor-done", "Congratulations", "With no Personality at the battle, the Dragon couldn't destroy your Province.\n\nYou've learned how to Lobby for the Imperial Favor, how to use it and its two abilities, other ways to take it, and how Kyuden Otomo's Favor tokens stand in for it. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu", highlight: OwnProvinces(0)),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    // Province cards: the Dragon's are the script's (Mirumoto Gobashi, Togashi Meiyu, Prosperous Village, Large Farm); yours are Holdings from the Favors deck, so no second Sosuke or Nara hides in them.
    // Every Province stays face down.
    private static readonly string[] CraneDynasty = [TutorialCards.Marketplace, TutorialCards.LuxuriousSilk, TutorialCards.RemoteVillage, TutorialCards.Marketplace];
    private static readonly string[] DragonDynasty = [TutorialCards.MirumotoGobashi, TutorialCards.TogashiMeiyu, TutorialCards.ProsperousVillage, TutorialCards.LargeFarm];

    /// <summary>You (Favors, Kyuden Otomo - exp, 13 Honor): Border Keep, Marketplace, Luxurious Silk and Remote Village; Doji Dainagon wearing Dazzling Attire, two Daidoji Sosuke and Kakita Nara;
    /// The Host's Advantage, Request Authorization, Dismissing the Cur and The Empress' Address in hand, Moksha Patam then Home Guard on top of the Fate deck. The Dragon (Monastery of New Thought, 8 Honor):
    /// Gold Mine, Farmer's Market, Incense Mill, Border Keep and Bamboo Harvesters; Mirumoto Yumaru with an Unparalleled Sword, Togashi Hiroto with 5 Fire tokens, Mirumoto Gobashi (a Kensai) with two Blades of Perfection;
    /// Deadly Discipline, Hundred-Fold Cut and Revenge in hand.</summary>
    private static TutorialSetup Table => new(
        new("Crane", TutorialCards.KyudenOtomoExp, CraneDynasty,
            [TutorialCards.TheHostsAdvantage, TutorialCards.RequestAuthorization, TutorialCards.DismissingTheCur, TutorialCards.TheEmpressAddress, TutorialCards.MokshaPatam, TutorialCards.HomeGuard],
            [new(TutorialCards.BorderKeep), new(TutorialCards.Marketplace), new(TutorialCards.LuxuriousSilk), new(TutorialCards.RemoteVillage),
             new(TutorialCards.DojiDainagon, Attachments: [TutorialCards.DazzlingAttire]), new(TutorialCards.DaidojiSosuke), new(TutorialCards.DaidojiSosuke), new(TutorialCards.KakitaNara)], Honor: 13,
            Deck: Filler("Crane", TutorialCards.KyudenOtomoExp, CraneDynasty, [TutorialCards.HomeGuard])),
        new("Dragon", TutorialCards.MonasteryOfNewThought, DragonDynasty, [TutorialCards.DeadlyDiscipline, TutorialCards.HundredFoldCut, TutorialCards.Revenge],
            [new(TutorialCards.GoldMine), new(TutorialCards.FarmersMarket), new(TutorialCards.IncenseMill), new(TutorialCards.BorderKeep), new(TutorialCards.BambooHarvesters),
             new(TutorialCards.MirumotoYumaru, Attachments: [TutorialCards.UnparalleledSword]), new(TutorialCards.TogashiHiroto, FireTokens: 5),
             new(TutorialCards.MirumotoGobashi, Attachments: [TutorialCards.BladeOfPerfection, TutorialCards.BladeOfPerfection])], Honor: 8,
            Deck: Filler("Dragon", TutorialCards.MonasteryOfNewThought, DragonDynasty, [TutorialCards.HundredFoldCut])),
        TurnsTaken: 3);

    /// <summary>Your hand of four and the Dragon's three are drawn, Kyuden Otomo starts with two Favor tokens, every Province stays face down through both Events phases, and your turn starts.</summary>
    private static void Opening(TutorialContext c)
    {
        c.Draw(4); c.FoeDraw(3); c.AddFavorTokens(2);
        c.KeepFaceDown(TutorialRunner.Seat, 0, 1, 2, 3); c.KeepFaceDown(Foe0, 0, 1, 2, 3);
        c.StartTurn();
    }

    /// <summary>The Dragon attack your Province 1 with Mirumoto Gobashi alone; everything else they do is a step's (Deadly Discipline) or a pass.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [], AttackProvince: 0, Attackers: [TutorialCards.MirumotoGobashi]);

    public static readonly TutorialLesson Lesson = new("imperial-favor", "Imperial Favor", Table, Steps, FoePlays, Opening: Opening);
}
