namespace L5R.Core;

/// <summary>The Spells and Shugenja lesson (issue #1076, curriculum id <c>spells</c>): Phoenix (you, Library of Rebirth) against Scorpion Lethargy (The Towers of the Yogo), from your Action phase into the
/// Scorpion player's attack. You attach Spells with Equip (Limited), see that only a Shugenja may take one, and meet element keywords; then you defend and use Equip (Battle) and the additional action it gives
/// on two Spells, with Shiba Sansesuke catching the Scorpion's bow. The engine plays every rule: the script only chooses what is offered and has the Scorpion make the plays the notes describe.
/// Every number in the words is checked by TutorialSpellsTests.</summary>
internal static class TutorialLessonSpells
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? card = null) =>
        (look, command) => command.Type == type && (card is null || look.Is(command.CardId, card));
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> OwnProvinces(params int[] indices) => look => indices.Select(i => look.Province(TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    /// <summary>True when the command pays with exactly one of these Gold sources.</summary>
    private static bool PaidByOneOf(TutorialLookup look, GameCommand command, params string[] definitions) =>
        command.SourceIds is [var source] && definitions.Any(d => look.Is(source, d));

    private static bool Assigned(TutorialLookup look, GameCommand command, string definition, int province) =>
        command.Assignments is { } assignments && look.Own(definition, Zone.Home).Any(id => assignments.TryGetValue(id, out var at) && at == province);

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

    private static readonly string[] Bar = [TutorialGlow.PhaseBar], Battle = [TutorialGlow.BattleBar];

    /// <summary>This ability is open to you: the Scorpion's play has reached the point the next step talks about (Sansesuke's Reaction to its bow, your next Equip (Battle)).</summary>
    private static bool YourAction(TutorialContext c, string ability) =>
        c.Engine.QueryActions(TutorialRunner.Seat).Any(a => a.Enabled && a.Command?.AbilityId == ability);

    public static readonly TutorialStep[] Steps =
    [
        Note("shugenja", "Shugenja", "In L5R, wizards are called Shugenja, and they're the only kind of Personality that may cast Spells. You can tell a Personality is a Shugenja because the word is printed among their bold keywords.\n\nMouse over Isawa Tomohiro and Natsumi: they both have it. Shiba Sansesuke does not, and neither do any of the Scorpion Personalities.",
            highlight: Own(TutorialCards.IsawaTomohiro, TutorialCards.Natsumi)),
        Note("spells", "Spells", "Your hand is full of Spells. They are Fate cards with a blue background. Only Shugenja may attach Spells, and only Shugenja may use the actions on them. If a Personality loses the Shugenja keyword, they may not use the actions on the Spells attached to them until they regain it.",
            glow: [TutorialGlow.Hand]),
        Note("two-equips", "Two ways to equip", "Every player has two Equip abilities. Equip (Limited) attaches an attachment from your hand (an Item, a Follower or a Spell) to one of your Personalities during your own Action phase. Equip (Battle) attaches only Spells, during a battle; we'll come to it later.\n\nSpells can use both.",
            glow: Bar),
        Do("equip-flood", "Equip (Limited)", "Do that now: attach Scouring Flood to Isawa Tomohiro.",
            "Click Scouring Flood, then click Isawa Tomohiro. Scouring Flood costs 2 Gold: click your Border Keep or your Large Farm to pay for it. Click Attach to confirm.",
            Move("equip", TutorialCards.ScouringFlood), accept: (look, command) => look.Is(command.TargetId, TutorialCards.IsawaTomohiro) && PaidByOneOf(look, command, TutorialCards.BorderKeep, TutorialCards.LargeFarm),
            hint: "Attach Scouring Flood to Isawa Tomohiro, paid by your Border Keep or your Large Farm.",
            highlight: All(Hand(TutorialCards.ScouringFlood), Own(TutorialCards.IsawaTomohiro, TutorialCards.BorderKeep, TutorialCards.LargeFarm))),
        Note("elements", "Elements", "Mouse over Scouring Flood. It has the Water keyword. There are five elements in L5R (Air, Earth, Fire, Water and Void), and many cards carry one as a keyword. Isawa Tomohiro has Air and Void.\n\nOn their own these keywords do nothing, but some cards check for them, so read your cards carefully. A Shugenja may attach a Spell of any element: Tomohiro isn't Water, and Scouring Flood attached to him just fine.",
            highlight: Own(TutorialCards.ScouringFlood, TutorialCards.IsawaTomohiro)),
        Note("flood-trait", "A trait", "Scouring Flood doesn't do anything special with its Water keyword, so let's move on. It has the trait \"This Personality has +2F.\" Like a Weapon's bonus, it applies whether the Spell is bowed or unbowed: as long as Scouring Flood is attached, Isawa Tomohiro has +2F.",
            highlight: Own(TutorialCards.ScouringFlood)),
        Note("cost-effect", "Before and after the colon", "It also has a Battle ability: \"Bow this card and target a defending Personality: Move him home.\" Everything before the colon must happen for the effect after it to happen. Bowing Scouring Flood is its cost. Choosing a defending Personality is its target (strictly, targeting isn't a cost, but it works the same way here). If you can't bow Scouring Flood, or there is no defending Personality to target, you can't get the effect, \"Move him home.\"\n\nScouring Flood moves a defender, so it's a Spell for when you attack.",
            highlight: Own(TutorialCards.ScouringFlood)),
        Note("performers", "Who performs it", "This is how all actions work. Spells generally don't name a performer because, like an Item's, a Spell's actions are always performed by the Personality it is attached to.",
            highlight: Own(TutorialCards.IsawaTomohiro)),
        Note("not-a-shugenja", "Not a Shugenja", "Shiba Sansesuke isn't a Shugenja, so he can't attach a Spell. When you choose a Personality for a Spell, he isn't even offered. You'll see that on the next attachment.",
            highlight: Own(TutorialCards.ShibaSansesuke)),
        Do("equip-swell", "Natsumi", "Now let's boost Natsumi's strength too. We want to make sure you can withstand the attack the Scorpion player is likely to make.",
            "Click The Swell of Strength, then click Natsumi (Shiba Sansesuke isn't offered). It costs 3 Gold: click your Silver Mine, which produces 3 Gold for a Phoenix player. Click Attach to confirm.",
            Move("equip", TutorialCards.TheSwellOfStrength), accept: (look, command) => look.Is(command.TargetId, TutorialCards.Natsumi) && PaidByOneOf(look, command, TutorialCards.SilverMine),
            hint: "Attach The Swell of Strength to Natsumi, paid by your Silver Mine.",
            highlight: All(Hand(TutorialCards.TheSwellOfStrength), Own(TutorialCards.Natsumi, TutorialCards.SilverMine))),
        Note("swell-traits", "Two traits", "The Swell of Strength has two traits. The first, \"As a Focus Effect...\", only does something when the card is focused from your hand during a duel; it does nothing while attached. The second is the familiar +2F. It has a Battle ability too, which reduces an enemy's Force to 0.",
            highlight: Own(TutorialCards.TheSwellOfStrength)),
        Note("to-dynasty", "Your Dynasty phase", "Now that your forces are a little stronger, you can probably defend against the Scorpion. You'll skip attacking this turn and move on to your Dynasty phase.\n\nBe sure to keep your Stronghold straightened, because otherwise you won't be able to use all of its powerful abilities.",
            then: c => { c.Auto("pass"); c.Foe(s => s.Phase != Phase.Action); c.Auto("pass"); }, highlight: Own(TutorialCards.LibraryOfRebirth), glow: Bar),
        Do("buy-archive", "Whispering Archive", "Whispering Archive costs 0 Gold, and your Spells enter play for 1 less Gold while you have it.",
            "Click Whispering Archive in your Provinces, then click Recruit.", Move("purchase", TutorialCards.WhisperingArchive),
            hint: "Buy the Whispering Archive.", highlight: OwnProvinces(0), glow: Bar),
        Do("buy-silver-mine", "Silver Mine", "A second Silver Mine costs 2 Gold. Pay with a Holding, not your Stronghold.",
            "Click Silver Mine in your Provinces, then click your Large Farm (or your Border Keep, whichever is still straightened) and click Recruit.", Move("purchase", TutorialCards.SilverMine),
            accept: (look, command) => PaidByOneOf(look, command, TutorialCards.BorderKeep, TutorialCards.LargeFarm),
            hint: "Buy the Silver Mine with your Large Farm or your Border Keep, keeping your Stronghold straightened.",
            highlight: All(OwnProvinces(1), Own(TutorialCards.BorderKeep, TutorialCards.LargeFarm)), glow: Bar),
        Do("end-dynasty", "Ending your turn", "That's all for this turn. Your Stronghold is still straightened.", "Click End Dynasty Phase.", Move("endDynasty"),
            then: c => c.Foe(s => s.ActivePlayerId == 1 - TutorialRunner.Seat && s.Phase == Phase.Action && s.PriorityPlayerId == TutorialRunner.Seat), highlight: Own(TutorialCards.LibraryOfRebirth), glow: Bar),
        Do("limited", "The Scorpion's turn", "It's the Scorpion player's turn. They revealed their Provinces and passed their first action.\n\nEquip (Limited) is a Limited action, and Limited actions may only be taken in your own Action phase, so you may not attach any Spells from your hand right now.",
            "Click Pass action.", Move("pass"), then: c => c.Foe(s => s.Phase == Phase.DefenseAssignment && s.PriorityPlayerId == TutorialRunner.Seat), glow: [TutorialGlow.Hand, TutorialGlow.PhaseBar]),
        Do("defend", "Defend", "The Scorpion player has attacked your Province 1 with all three of their Personalities. You're prepared: assign all of your Personalities to defend it.",
            "Drag Isawa Tomohiro, Natsumi and Shiba Sansesuke to your Province 1 (or click each, then the Province), then click Confirm assignments.",
            (_, command) => command.Type == "assignDefense" && (command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == 0) : command.Province is null or 0), accept: (look, command) => new[] { TutorialCards.IsawaTomohiro, TutorialCards.Natsumi, TutorialCards.ShibaSansesuke }.All(d => Assigned(look, command, d, 0)),
            hint: "Assign Isawa Tomohiro, Natsumi and Shiba Sansesuke to defend your Province 1.",
            then: c => c.Foe(s => s.Phase == Phase.Battle && s.PriorityPlayerId == TutorialRunner.Seat),
            highlight: All(Own(TutorialCards.IsawaTomohiro, TutorialCards.Natsumi, TutorialCards.ShibaSansesuke), OwnProvinces(0)), glow: Battle, onlyProvinces: [0]),
        Note("equip-battle", "Equip (Battle)", "Remember that there are two ways to equip a Spell. Equip (Battle) is the second, and you still have Spells in your hand. Defenders take the first action in a battle, so you can use your first action to attach a Spell.",
            glow: [TutorialGlow.Hand, TutorialGlow.BattleBar]),
        Note("additional-action", "The additional action", "Equip (Battle) comes with a bonus. Choose your unbowed, opposed Shugenja and a Spell in your hand, and attach it, paying all costs. Then you may take an additional action to use a Battle ability on that Spell.",
            glow: Battle),
        Note("waru", "Two bows", "Let's get ahead of the Scorpion player's bow abilities. Bayushi Waru effectively has two: one printed on him, and one that The Towers of the Yogo, the Scorpion Stronghold, gives every Scorpion Clan Personality. Let's get rid of him.",
            highlight: Foe(TutorialCards.BayushiWaru, TutorialCards.TowersOfTheYogo), glow: Battle),
        Do("equip-hunger", "The Earth's Hunger", "The Earth's Hunger costs 1 Gold, but your Whispering Archive makes Spells enter play for 1 less, so it's free.",
            "Click The Earth's Hunger, then \"Isawa Tomohiro · Equip (Battle)\" in the sidebar. Choose The Earth's Hunger and click Use ability.",
            Ability("equip-battle"), accept: (look, command) => look.Is(command.PerformerId, TutorialCards.IsawaTomohiro) && (look.Is(command.TargetId, TutorialCards.TheEarthsHunger) || look.Is(command.CardId, TutorialCards.TheEarthsHunger)),
            hint: "Use Equip (Battle) to attach The Earth's Hunger to Isawa Tomohiro.", highlight: All(Hand(TutorialCards.TheEarthsHunger), Own(TutorialCards.IsawaTomohiro)), glow: Battle),
        Do("destroy-waru", "An element at work", "The Earth's Hunger's cost is \"Bow this Shugenja, or bow this card if he is Earth\". Isawa Tomohiro is Air and Void, not Earth, so he has to bow himself to pay: an element keyword doing something special. A bowed Personality adds no Force, but Waru is worth it.",
            "Click Isawa Tomohiro's The Earth's Hunger ability in the sidebar, choose Bayushi Waru, then click Use The Earth's Hunger.",
            Ability("earths-hunger-destroy"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiWaru),
            hint: "Use The Earth's Hunger on Bayushi Waru.", then: c => c.Foe(s => YourAction(c, "sansesuke-redirect")), highlight: All(Own(TutorialCards.TheEarthsHunger), Foe(TutorialCards.BayushiWaru)), glow: Battle,
            targets: look => look.Foe(TutorialCards.BayushiWaru, Zone.Battlefield)),
        Do("redirect", "Protect Natsumi", "The Scorpion player is using the ability The Towers of the Yogo gives Bayushi Hurunayi to bow Natsumi, wisely trying to bow your most powerful remaining Shugenja. If Natsumi is bowed, you'll be in trouble. Shiba Sansesuke's Reaction can make the action target him instead.",
            "Click Use Shiba Sansesuke in the sidebar, then Play Reaction.",
            Ability("sansesuke-redirect"), hint: "Use Shiba Sansesuke's Reaction.", then: c => c.Foe(s => YourAction(c, "equip-battle")), highlight: Own(TutorialCards.ShibaSansesuke, TutorialCards.Natsumi), glow: Battle),
        Do("equip-tsunami", "Strength of the Tsunami", "It's your action again: time to hit the Scorpion hard. Strength of the Tsunami costs 7 with your Whispering Archive. Your Stronghold's Reaction produces 4 Gold for a Spell in the Combat Segment, even while bowed, and bowing it gives 4 more.",
            "Click Strength of the Tsunami, then \"Natsumi · Equip (Battle)\" in the sidebar, and choose it. Tick both Library of Rebirth payment boxes, then click Use ability.",
            Ability("equip-battle"), accept: (look, command) => look.Is(command.PerformerId, TutorialCards.Natsumi) && (look.Is(command.TargetId, TutorialCards.StrengthOfTheTsunami) || look.Is(command.CardId, TutorialCards.StrengthOfTheTsunami)),
            hint: "Use Equip (Battle) to attach Strength of the Tsunami to Natsumi, paid by Library of Rebirth.", highlight: All(Hand(TutorialCards.StrengthOfTheTsunami), Own(TutorialCards.Natsumi, TutorialCards.LibraryOfRebirth)), glow: Battle),
        Do("utoro-home", "Sending Utoro home", "You used Equip (Battle), so you may now use a Battle ability on Strength of the Tsunami. Its second ability reads: \"Bow this card and target an enemy Personality: Move him home. If he moved, negate his (future) movement.\"",
            "Click Strength of the Tsunami's second ability in the sidebar, choose Bayushi Utoro, then click Use Strength of the Tsunami.",
            Ability("tsunami-move-home"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiUtoro),
            hint: "Use Strength of the Tsunami to send Bayushi Utoro home.", highlight: All(Own(TutorialCards.StrengthOfTheTsunami), Foe(TutorialCards.BayushiUtoro)), glow: Battle,
            targets: look => look.Foe(TutorialCards.BayushiUtoro, Zone.Battlefield)),
        Note("done", "Congratulations", "You're now decisively winning this battle thanks to Equip (Battle): Natsumi has 11 Force, and the only Scorpion Personality left at the battlefield is bowed.\n\nYou've learned what Shugenja are, how Spells work, how an element can matter, and how to use the Equip (Limited) and Equip (Battle) abilities Spells enjoy. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu", highlight: Own(TutorialCards.Natsumi), glow: Battle),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Phoenix (you), at Library of Rebirth with 10 Honor: a Silver Mine, Border Keep, Small Farm and Large Farm, Shiba Sansesuke, Isawa Tomohiro and Natsumi in play, six Spells in hand, and Provinces holding
    /// a Whispering Archive, a Silver Mine and (face down) Agasha Kodo and Asako Megu. Scorpion Lethargy, at The Towers of the Yogo with 3 Honor: a Geisha House, Small Farm and Remote Village, Bayushi Hurunayi,
    /// Bayushi Utoro with a Seppun Heavy Elite, and Bayushi Waru. Nobody draws at the End phase.</summary>
    private static readonly TutorialSetup Table = new(
        new("Phoenix", TutorialCards.LibraryOfRebirth,
            [TutorialCards.WhisperingArchive, TutorialCards.SilverMine, TutorialCards.AgashaKodo, TutorialCards.AsakoMegu, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.ObscuredPathways, TutorialCards.PeaceOfTheKami, TutorialCards.ScouringFlood, TutorialCards.TheEarthsHunger, TutorialCards.StrengthOfTheTsunami, TutorialCards.TheSwellOfStrength],
            [new(TutorialCards.SilverMine), new(TutorialCards.BorderKeep), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm),
             new(TutorialCards.ShibaSansesuke), new(TutorialCards.IsawaTomohiro), new(TutorialCards.Natsumi)],
            Honor: 10, Deck: Filler("Phoenix", TutorialCards.LibraryOfRebirth, [TutorialCards.SmallFarm, TutorialCards.LargeFarm], [TutorialCards.PeaceOfTheKami])),
        new("Scorpion", TutorialCards.TowersOfTheYogo,
            [TutorialCards.GeishaHouse, TutorialCards.SmallFarm, TutorialCards.RemoteVillage, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.SneakAttack],
            [new(TutorialCards.GeishaHouse), new(TutorialCards.SmallFarm), new(TutorialCards.RemoteVillage),
             new(TutorialCards.BayushiHurunayi), new(TutorialCards.BayushiUtoro, Attachments: [TutorialCards.SeppunHeavyElite]), new(TutorialCards.BayushiWaru)],
            Honor: 3, Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.SmallFarm, TutorialCards.GeishaHouse], [TutorialCards.SneakAttack])),
        TurnsTaken: 3, NoEndDraw: true);

    /// <summary>The Scorpion attacks your Province 1 with everyone, then bows Natsumi with the ability The Towers of the Yogo gives Bayushi Hurunayi.</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], [("towers-of-yogo-bow", TutorialCards.Natsumi)], AttackProvince: 0, Performers: [TutorialCards.BayushiHurunayi]);

    /// <summary>The lesson opens in your Action phase: your six Spells are in hand, and the Events phase revealed only your first two Provinces.</summary>
    public static readonly TutorialLesson Lesson = new("spells", "Spells and Shugenja", Table, Steps, FoePlays,
        Opening: c => { c.KeepFaceDown(TutorialRunner.Seat, 2, 3); c.Draw(6); c.StartTurn(); });
}
