namespace L5R.Core;

/// <summary>The Attachments lesson (issue #1077, curriculum id <c>attachments</c>): Scorpion Lethargy (you, The Towers of the Yogo, 3 Honor) against Phoenix (Library of Rebirth, 10 Honor), from your Action phase into your
/// attack on Phoenix's Province 1. You attach a Follower, two Items (a Weapon and an Armor) and a General Item with Equip (Limited), then watch Phoenix attach Spells with Equip (Battle) and use their abilities, and use
/// the abilities on your own unbowed attachments. The engine plays every rule: the script only chooses what is offered and has Phoenix make the plays the notes describe.
/// Every number in the words is checked by TutorialAttachmentsTests.</summary>
internal static class TutorialLessonAttachments
{
    private static Func<TutorialLookup, GameCommand, bool> Move(string type, string? card = null) =>
        (look, command) => command.Type == type && (card is null || look.Is(command.CardId, card));
    private static Func<TutorialLookup, GameCommand, bool> Ability(string ability) => (_, command) => command.AbilityId == ability;

    private static Func<TutorialLookup, IEnumerable<string>> Own(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Foe(params string[] definitions) => look => definitions.SelectMany(d => look.Foe(d, Zone.Home, Zone.Battlefield, Zone.Stronghold, Zone.Attached));
    private static Func<TutorialLookup, IEnumerable<string>> Hand(params string[] definitions) => look => definitions.SelectMany(d => look.Own(d, Zone.Hand));
    private static Func<TutorialLookup, IEnumerable<string>> FoeProvinces(params int[] indices) => look => indices.Select(i => look.Province(1 - TutorialRunner.Seat, i)).OfType<string>();
    private static Func<TutorialLookup, IEnumerable<string>> All(params Func<TutorialLookup, IEnumerable<string>>[] parts) => look => parts.SelectMany(p => p(look));

    /// <summary>True when the command pays with exactly one of these Gold sources.</summary>
    private static bool PaidByOneOf(TutorialLookup look, GameCommand command, params string[] definitions) =>
        command.SourceIds is [var source] && definitions.Any(d => look.Is(source, d));

    private static TutorialStep Note(string id, string title, string body, Action<TutorialContext>? then = null, string button = "Continue", Func<TutorialLookup, IEnumerable<string>>? highlight = null,
        string[]? glow = null, string? emphasis = null) =>
        new() { Id = id, Title = title, Body = body, Button = button, Then = then, Highlight = highlight, Glow = glow, Emphasis = emphasis };

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

    private static readonly string[] YourPersonalities = [TutorialCards.BayushiHurunayi, TutorialCards.BayushiUtoro, TutorialCards.BayushiWaru];

    private static bool AttackedProvince(GameCommand command, int province) => command.Assignments is { Count: > 0 } assignments ? assignments.Values.All(p => p == province) : command.Province == province;

    public static readonly TutorialStep[] Steps =
    [
        Note("attachments", "Attachments", "In this lesson you'll learn everything you need to use attachments. Attachments are Fate cards, and there are three kinds: Items, Followers and Spells.\n\nYour hand holds Items and Followers. Your opponent, the Phoenix, is holding Spells, and you'll see them used in a battle later.",
            glow: [TutorialGlow.Hand]),
        Note("units", "Joining an army", "An attachment enters play by attaching to one of your Personalities, and it can't be in play without one. If a Personality leaves play, his attachments leave play with him. A Personality and his attachments together make up his unit.",
            highlight: Own(TutorialCards.BayushiHurunayi, TutorialCards.BayushiUtoro, TutorialCards.BayushiWaru)),
        Note("equip-limited", "Equip (Limited)", "During your own Action phase, every player has the Equip (Limited) ability, which attaches an attachment from your hand to one of your Personalities. It is repeatable: you may use it as many times as you have attachments to play and Gold to pay for them.\n\nTo use it you announce the card. Select the card, choose the Personality it will attach to, pay its Gold cost and confirm. Choosing the Personality is targeting; the Gold is the cost. The card is then placed beneath the Personality with its title showing.",
            glow: Bar),
        Do("equip-elite", "Your first attachment", "Attach Seppun Heavy Elite, a Follower, to Bayushi Waru.",
            "Click Seppun Heavy Elite, then click Bayushi Waru. It costs 3 Gold: click your Geisha House, which produces 3 Gold for a Scorpion player. Click Attach to confirm.",
            Move("equip", TutorialCards.SeppunHeavyElite), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiWaru) && PaidByOneOf(look, command, TutorialCards.GeishaHouse),
            hint: "Attach Seppun Heavy Elite to Bayushi Waru, paid by your Geisha House.",
            highlight: All(Hand(TutorialCards.SeppunHeavyElite), Own(TutorialCards.BayushiWaru, TutorialCards.GeishaHouse))),
        Note("elite", "Elite", "Mouse over Seppun Heavy Elite. It has the Elite keyword, so it contributes its Force to an army even when it is bowed; an ordinary bowed card adds nothing. Its text also gives its Personality Elite, so Bayushi Waru contributes his Force to an army even when he is bowed.",
            highlight: Own(TutorialCards.SeppunHeavyElite, TutorialCards.BayushiWaru)),
        Note("honor-requirement", "Honor Requirement", "Followers have an Honor Requirement, too. Unlike a Personality's, it is not compared to your Family Honor: it is compared to the Personal Honor of the Personality the Follower attaches to. Seppun Heavy Elite needs 0, and Bayushi Waru has 2, so it attached without trouble.\n\nThis is a restriction on attaching, not a price. You can't pay extra to attach a Follower to a Personality whose Personal Honor is too low.",
            highlight: All(Own(TutorialCards.SeppunHeavyElite), Hand(TutorialCards.SoshiHouseGuard)), emphasis: TutorialEmphasis.HonorRequirement),
        Do("equip-blade", "Items", "Now attach Nightingale Blade, an Item, to Bayushi Hurunayi.",
            "Click Nightingale Blade, then click Bayushi Hurunayi. It costs 4 Gold: click The Towers of the Yogo, your Stronghold. Click Attach to confirm.",
            Move("equip", TutorialCards.NightingaleBlade), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiHurunayi) && PaidByOneOf(look, command, TutorialCards.TowersOfTheYogo),
            hint: "Attach Nightingale Blade to Bayushi Hurunayi, paid by your Stronghold.",
            highlight: All(Hand(TutorialCards.NightingaleBlade), Own(TutorialCards.BayushiHurunayi, TutorialCards.TowersOfTheYogo))),
        Note("weapon", "Weapons", "Items attach the same way Followers do, but they have a few more rules. Many Items have the keyword Weapon or Armor, and the rest are General Items.\n\nMouse over Nightingale Blade: it is a Weapon. A Personality will not attach a Weapon if he already has one, unless he is a Kensai, who may attach two.",
            highlight: Own(TutorialCards.NightingaleBlade)),
        Note("armor", "Armor", "Mouse over Light Armor. It has the keyword Armor. A Personality will not attach an Armor if he already has one, and not even a Kensai may have two. A Personality may have both a Weapon and an Armor, though.",
            highlight: Hand(TutorialCards.LightArmor)),
        Do("equip-light-armor", "A Weapon and an Armor", "Attach Light Armor to Bayushi Hurunayi, who already carries Nightingale Blade.",
            "Click Light Armor, then click Bayushi Hurunayi. It costs 2 Gold: click your Large Farm. Click Attach to confirm.",
            Move("equip", TutorialCards.LightArmor), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiHurunayi) && PaidByOneOf(look, command, TutorialCards.LargeFarm),
            hint: "Attach Light Armor to Bayushi Hurunayi, paid by your Large Farm.",
            highlight: All(Hand(TutorialCards.LightArmor), Own(TutorialCards.BayushiHurunayi, TutorialCards.LargeFarm))),
        Do("equip-standard", "General Items", "An Item with neither Weapon nor Armor is a General Item, and a Personality may have any number of them. Clan Standard is one. Attach it to Bayushi Utoro.",
            "Click Clan Standard, then click Bayushi Utoro. It costs 3 Gold: click your Remote Village (a Holding that produces 3 Gold). Click Attach to confirm.",
            Move("equip", TutorialCards.ClanStandard), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiUtoro) && PaidByOneOf(look, command, TutorialCards.RemoteVillage, TutorialCards.RedCraneDojo),
            hint: "Attach Clan Standard to Bayushi Utoro, paid by your Remote Village.",
            highlight: All(Hand(TutorialCards.ClanStandard), Own(TutorialCards.BayushiUtoro, TutorialCards.RemoteVillage))),
        Do("equip-soshi", "A restricted Follower", "Soshi House Guard is a Follower that will only attach to a Scorpion Clan Personality, and Bayushi Utoro is one. Attach it to him.",
            "Click Soshi House Guard, then click Bayushi Utoro. It costs 4 Gold: click your Red Crane Dojo, which produces 4 Gold when paying for a Ninja card like this one. Click Attach to confirm.",
            Move("equip", TutorialCards.SoshiHouseGuard), accept: (look, command) => look.Is(command.TargetId, TutorialCards.BayushiUtoro),
            hint: "Attach Soshi House Guard to Bayushi Utoro.",
            highlight: All(Hand(TutorialCards.SoshiHouseGuard), Own(TutorialCards.BayushiUtoro, TutorialCards.RedCraneDojo))),
        Note("abilities", "Abilities on attachments", "Mouse over Soshi House Guard and Nightingale Blade. Each has a Battle ability. You are out of cards, so those abilities are what you have to cause the Phoenix trouble.\n\nPass now, and then declare an attack on the Phoenix.",
            highlight: Own(TutorialCards.SoshiHouseGuard, TutorialCards.NightingaleBlade), glow: Bar),
        Do("pass-to-attack", "Both players pass", "The Phoenix player has passed as well.", "Click Pass action.", Move("pass"), glow: Bar),
        Do("declare-attack", "Declare an attack", "Now it's time to attack.", "Click the red Declare an attack button.", Move("declareAttack")),
        Do("assign-attackers", "Assign your attackers", "Assign all three of your Personalities to attack the Phoenix player's Province 1.",
            "Click each of your Personalities and then Province 1, or drag them to it, then click Confirm assignments.",
            (_, command) => command.Type == "attack" && AttackedProvince(command, 0), accept: (look, command) => YourPersonalities.All(d => look.Own(d, Zone.Home).Any(id => command.Assignments is { } a && a.TryGetValue(id, out var at) && at == 0)),
            hint: "Attack the Phoenix player's Province 1 with all three Personalities.", highlight: All(Own(YourPersonalities), FoeProvinces(0)), glow: Battle, onlyProvinces: [0]),
        Do("engage", "Engagement", "Both Phoenix Personalities have assigned to defend. Select that battle to resolve it.",
            "Click Fight at province 1.", (_, command) => command is { Type: "selectBattle", Province: 0 }, hint: "Resolve the battle at Province 1.", highlight: FoeProvinces(0), glow: Battle),
        Note("phoenix-first", "The Phoenix acts first", "The defender takes the first action in a battle. The Phoenix player is holding Spells, so now we'll learn about them.",
            then: c => c.FoeUse("equip-battle", TutorialCards.Natsumi, TutorialCards.StrengthOfTheTsunami, pay: [TutorialContext.LibraryReaction, TutorialCards.BorderKeep, TutorialCards.LargeFarm]), highlight: Foe(TutorialCards.IsawaTomohiro, TutorialCards.Natsumi), glow: Battle),
        Note("spells", "Spells", "The Phoenix player attached Strength of the Tsunami to Natsumi. Spells are attachments that only a Shugenja can take, and Natsumi and Isawa Tomohiro are both Shugenja.\n\nA Spell can be attached with Equip (Limited) in the Action phase, like your Items and Followers, or with Equip (Battle) during a battle.",
            highlight: Foe(TutorialCards.StrengthOfTheTsunami, TutorialCards.Natsumi), glow: Battle),
        Note("modifiers", "Modifiers", "Strength of the Tsunami says \"This Shugenja has +6F\", so Natsumi's Force is now 9. Any attachment with a +X modifier in a stat position, or in its text, changes that statistic of its Personality. It does so whether or not the attachment is bowed.",
            highlight: Foe(TutorialCards.Natsumi, TutorialCards.StrengthOfTheTsunami), glow: Battle),
        Note("contributions", "Who adds what", "The three kinds of attachment add Force in different ways. An Item's Force and Chi are modifiers to its Personality, as Nightingale Blade's +3F/+1C is to Bayushi Hurunayi. A Follower has a Force of its own, added to the army separately. A Spell has no Force or Chi bonus on its card: Strength of the Tsunami's +6F comes from its text.",
            highlight: Own(TutorialCards.NightingaleBlade, TutorialCards.SeppunHeavyElite), glow: Battle),
        Note("additional", "The additional action", "Equip (Battle) needs an unbowed, opposed Shugenja, and it lets the player take an additional action to use a Battle ability on the Spell. The Phoenix is going to use Strength of the Tsunami to send the very dangerous Bayushi Waru home.",
            then: c => c.FoeUse("tsunami-move-home", card: TutorialCards.StrengthOfTheTsunami, target: TutorialCards.BayushiWaru), highlight: All(Foe(TutorialCards.StrengthOfTheTsunami), Own(TutorialCards.BayushiWaru)), glow: Battle),
        Do("soshi-bow", "Your first action", "Bayushi Waru is home, and his Seppun Heavy Elite went with him. Remember that a bowed Personality doesn't add Force to an army in the battle's resolution. Use Soshi House Guard to bow Natsumi.",
            "Click Soshi House Guard's ability in the sidebar, choose Natsumi, then click Use ability.",
            Ability("soshi-conditional-bow"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.Natsumi),
            hint: "Use Soshi House Guard on Natsumi.", then: c => c.FoeUse("equip-battle", TutorialCards.IsawaTomohiro, TutorialCards.TheSwellOfStrength, pay: [TutorialCards.SilverMine]),
            highlight: All(Own(TutorialCards.SoshiHouseGuard), Foe(TutorialCards.Natsumi)), glow: Battle, targets: look => look.Foe(TutorialCards.Natsumi, Zone.Battlefield)),
        Note("swell", "The Swell of Strength", "The Phoenix player attached The Swell of Strength to Isawa Tomohiro. That's bad news for Bayushi Hurunayi, whose Force of 9 is large (his own 2, Nightingale Blade's 3 and Light Armor's 3, and 1 from your Clan Standard), because The Swell of Strength can reduce an enemy's Force to 0.",
            then: c => c.FoeUse("swell-of-strength-attack", card: TutorialCards.TheSwellOfStrength, target: TutorialCards.BayushiHurunayi), highlight: All(Foe(TutorialCards.TheSwellOfStrength), Own(TutorialCards.BayushiHurunayi)), glow: Battle),
        Do("towers-bow", "Hurunayi is still useful", "Hurunayi's Force is 0, but he can still act. The Towers of the Yogo gives every Scorpion Clan Personality a Battle ability that bows an enemy Follower or an enemy Personality without Followers. Use it, on Bayushi Hurunayi, to bow Isawa Tomohiro.",
            "Click Bayushi Hurunayi's Bow this card ability in the sidebar, choose Isawa Tomohiro, then click Use ability.",
            Ability("towers-of-yogo-bow"), accept: (look, command) => look.Is(command.PerformerId ?? command.CardId, TutorialCards.BayushiHurunayi) && look.Is(command.TargetId, TutorialCards.IsawaTomohiro),
            hint: "Use The Towers of the Yogo's ability with Bayushi Hurunayi on Isawa Tomohiro.", highlight: All(Own(TutorialCards.BayushiHurunayi), Foe(TutorialCards.IsawaTomohiro)), glow: Battle,
            targets: look => look.Foe(TutorialCards.IsawaTomohiro, Zone.Battlefield)),
        Note("bowed-host", "Oops", "Oops: Hurunayi bowed himself to pay for that ability, before he used Nightingale Blade's bow. That's all right. An ability on a bowed card can't be used, but that doesn't stop abilities on unbowed cards attached to a bowed Personality. Nightingale Blade is unbowed, so Hurunayi can still use it.",
            then: c => { c.FoeUse("library-rebirth-straighten", TutorialCards.IsawaTomohiro); c.FoeDeclineAdditionalAction(); }, highlight: Own(TutorialCards.BayushiHurunayi, TutorialCards.NightingaleBlade), glow: Battle),
        Do("blade-bow", "Nightingale Blade", "The Phoenix player used an ability of its Stronghold to straighten Isawa Tomohiro. Use Nightingale Blade to bow her again.",
            "Click Nightingale Blade's ability in the sidebar, choose Isawa Tomohiro, then click Use ability.",
            Ability("nightingale-blade-bow"), accept: (look, command) => look.Is(command.TargetId, TutorialCards.IsawaTomohiro),
            hint: "Use Nightingale Blade on Isawa Tomohiro.", highlight: All(Own(TutorialCards.NightingaleBlade), Foe(TutorialCards.IsawaTomohiro)), glow: Battle,
            targets: look => look.Foe(TutorialCards.IsawaTomohiro, Zone.Battlefield)),
        Note("bowed-bonus", "Bowed, but still bonused", "Natsumi is bowed, but Strength of the Tsunami still gives Natsumi +6F: a modifier applies whether the attachment or the Personality is bowed. Natsumi's Force of 9 just doesn't count toward the Phoenix army while Natsumi is bowed.",
            highlight: Foe(TutorialCards.Natsumi, TutorialCards.StrengthOfTheTsunami), glow: Battle),
        Note("even-if", "Even if this card is bowed", "The Phoenix player is going to cut their losses. Mouse over Strength of the Tsunami and read its second ability: \"Even if this card is bowed, if this Shugenja is Water: Straighten him. You may move him home.\"\n\nNatsumi has the Water keyword, so Natsumi can use it even though the Spell is bowed. Rather than stay and lose, the Phoenix player straightens Natsumi and moves home.",
            then: c => { c.FoeUse("tsunami-straighten", TutorialCards.Natsumi, TutorialCards.StrengthOfTheTsunami); c.FoeDecide("move"); }, highlight: Foe(TutorialCards.StrengthOfTheTsunami, TutorialCards.Natsumi), glow: Battle),
        Do("pass-battle", "Winning the battle", "Natsumi is home and Isawa Tomohiro is bowed, so no Phoenix Personality is left to fight, and you have more than enough Force to destroy the Province.",
            "Click Pass battle action to proceed to the battle's resolution.", Move("pass"), then: c => c.Foe(), glow: Battle),
        Note("resolved", "Resolution", "The Province is destroyed, and your attackers return home, bowed.", glow: Battle),
        Note("congratulations", "Congratulations", "You now know how to attach each kind of attachment, how Spells are attached in the Action phase and in a battle, and how to use the abilities on an attachment. Select the Next Tutorial to continue learning.",
            button: "Tutorial Menu"),
    ];

    private static PublishedDeck Filler(string clan, string stronghold, string[] dynasty, string[] fate) =>
        new(clan, stronghold, [.. Enumerable.Range(0, 40).Select(i => dynasty[i % dynasty.Length])], [.. Enumerable.Range(0, 40).Select(i => fate[i % fate.Length])]);

    /// <summary>Scorpion Lethargy (you), at The Towers of the Yogo with 3 Honor: a Geisha House, Small Farm, Large Farm, Red Crane Dojo and Remote Village, Bayushi Hurunayi, Bayushi Utoro and Bayushi Waru in play, and five
    /// attachments in hand. Phoenix, at Library of Rebirth with 10 Honor: a Silver Mine, Border Keep, Small Farm and Large Farm, Isawa Tomohiro and Natsumi in play, and six Spells in hand. Every Province is face down.
    /// Your attachments cost 16 Gold, which is exactly what the table makes: each is paid by one source. Nobody draws at the End phase.</summary>
    private static readonly TutorialSetup Table = new(
        new("Scorpion", TutorialCards.TowersOfTheYogo,
            [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.SoshiHouseGuard, TutorialCards.NightingaleBlade, TutorialCards.SeppunHeavyElite, TutorialCards.LightArmor, TutorialCards.ClanStandard],
            [new(TutorialCards.GeishaHouse), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm), new(TutorialCards.RedCraneDojo), new(TutorialCards.RemoteVillage),
             new(TutorialCards.BayushiHurunayi), new(TutorialCards.BayushiUtoro), new(TutorialCards.BayushiWaru)],
            Honor: 3, Deck: Filler("Scorpion", TutorialCards.TowersOfTheYogo, [TutorialCards.SmallFarm, TutorialCards.GeishaHouse], [TutorialCards.SneakAttack])),
        new("Phoenix", TutorialCards.LibraryOfRebirth,
            [TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm, TutorialCards.SmallFarm],
            [TutorialCards.ObscuredPathways, TutorialCards.PeaceOfTheKami, TutorialCards.ScouringFlood, TutorialCards.TheEarthsHunger, TutorialCards.StrengthOfTheTsunami, TutorialCards.TheSwellOfStrength],
            [new(TutorialCards.SilverMine), new(TutorialCards.BorderKeep), new(TutorialCards.SmallFarm), new(TutorialCards.LargeFarm),
             new(TutorialCards.IsawaTomohiro), new(TutorialCards.Natsumi)],
            Honor: 10, Deck: Filler("Phoenix", TutorialCards.LibraryOfRebirth, [TutorialCards.SmallFarm, TutorialCards.LargeFarm], [TutorialCards.PeaceOfTheKami])),
        TurnsTaken: 3, NoEndDraw: true);

    /// <summary>The Phoenix defends the Province you attack; everything else it does is asked for by a note (<see cref="TutorialContext.FoeUse"/>).</summary>
    private static readonly TutorialFoeScript FoePlays = new([], [], []);

    /// <summary>The lesson opens in your Action phase: your five attachments and the Phoenix's six Spells are in hand, and every Province stays face down.</summary>
    public static readonly TutorialLesson Lesson = new("attachments", "Attachments", Table, Steps, FoePlays,
        Opening: c => { foreach (var player in new[] { 0, 1 }) c.KeepFaceDown(player, 0, 1, 2, 3); c.Draw(5); c.Engine.TutorialDraw(1, 6); c.StartTurn(); });
}
