using L5R.AI;
using L5R.Core;

// Issue #642: the policies Mendon specified for the practice bot, each pinned on its own with a small synthetic table. The soak suite (BotSoak.cs) proves the bot can
// answer anything the engine offers; these prove it answers the way the spec says.
internal static class BotPolicyTests
{
    public static readonly (string Name, Action Test)[] Tests =
    [
        ("Intent: abilities are read card-agnostically from their label and target (destroy > bow > penalize > help; negate, draw, favor, honor)", Intent),
        ("Profile: a Stronghold or deck average sets the Honor tier; Proclaim and gain-versus-loss follow it (spec)", Profile),
        ("Attack planner: as many sub-armies as each beat their province plus every unit that could defend it; one army when only one is possible; none when not even one is", Planner),
        ("Nothing a card does scores below passing: an ability the bot has no rule for is played over passing", NeverBelowPass),
        ("Draw abilities gain priority as the hand falls short of its maximum", DrawGrowsWithGap),
        ("Lobby: always with the higher Family Honor and more Personalities; low priority with equal or fewer unless it costs no bow; moderate otherwise", Lobby),
        ("Favor as a cost: spent freely when an Open action takes it back, once when the always-lobby case guarantees it, otherwise never above passing", FavorSpend),
        ("Honor choice: a high deck gains, a low deck takes it from the opponent, an in-between deck gains when ahead or below its starting Honor", HonorChoice),
        ("Arena fixes: a harmful ability is never aimed at the bot's own card; Shinden always bows; a full hand does not spend Gold on a pure draw; the bot never gives the opponent Honor or loses its own by choice", ArenaFixes),
        ("Hoshoku-sha (issue #679): the bot never chooses to destroy himself or any Personality of its own with his Battle/Open action; an enemy target is played", HoshokuNeverSelf),
        ("Read the Essence (issue #699): the bot turns an enemy face-down province card, never its own for nothing, scouts with the optional second flip or declines it, and never stalls", ReadTheEssenceBot),
        ("P25 decision infrastructure (issue #723): the bot answers a multi-pick Either/Or with the fewest picks and Done, a hook-registered pending type with an offered option, and a choice among shown cards, and never stalls", DecisionInfraBot),
        ("P20 search extensions (issue #728): the bot answers a multi-card province search, an attach follow-up and the other player's choice among shown cards, whichever seat decides, and never stalls", SearchExtensionsBot),
        ("Strength in Terror (playtest 2026-10): offered the destruction of an opponent's attachment the bot always destroys it, never declines", StrengthInTerrorBot),
        ("Last stand (playtest 2026-10): when every Province it has left would fall, the bot defends; when the opponent wins on Honor next turn it attacks every Province it can beat, whatever the army in reply", LastStandAndHonorRace),
        ("Memory: copies seen in public zones count (three of a non-Unique card, one of a Unique), the roll is deterministic and fair", Memory),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static AbilityIntent Read(string timing, string label, string target = "none") => AbilityIntent.Of(new AbilityDefinition(timing, label, target));

    private static void Intent()
    {
        var destroy = Read("Battle", "Target an enemy Personality: Destroy him", "enemy-personality");
        Check(destroy.Rank == EffectRank.Destroy && destroy.Side == TargetSide.Enemy, "destroy");
        Check(Read("Battle", "Bow a target enemy Personality", "enemy-personality").Rank == EffectRank.Bow, "bow");
        Check(Read("Battle", "Give a target enemy Personality -3F", "enemy-personality").Rank == EffectRank.Penalize, "penalize");
        Check(Read("Open", "Give your Personality +2F", "own-personality").Rank == EffectRank.Help, "help");
        Check(Read("Battle", "Ranged 4 Attack", "attack").Rank == EffectRank.Destroy, "an Attack destroys");
        Check(Read("Battle", "Bow this card and destroy this card: Draw a card").Rank == EffectRank.Help, "a cost that destroys the source is not a destroy effect");
        Check(Read("Limited", "Draw a card").Draws, "draws");
        Check(Read("Reaction", "After another player's action targets your Merchant, pay 2 Gold: Negate the action's effects").Negates, "negates");
        var take = Read("Open", "Political Open: Take the Imperial Favor");
        Check(take.TakesFavor && !take.TakesFavorBowing, "a free take");
        Check(Read("Limited", "Lobby · bow a Personality to take Favor", "lobby").TakesFavorBowing, "lobby bows");
        Check(Read("Open", "Political Open: Bow Makiko: Take the Imperial Favor").TakesFavorBowing, "a bowing take");
        var spend = Read("Limited", "Discard Favor · gain 2 Honor");
        Check(spend.DiscardsFavor && spend.HonorGain, "spends the Favor");
        var either = Read("Limited", "Political Limited: Bow your performing Courtier: Choose a player. He gains or loses 2 Honor", "player");
        Check(either.HonorGain && either.HonorLoss, "gains or loses");
        Check(Read("Open", "Frobnicate the widget").Rank == EffectRank.Neutral, "an unreadable label is neutral, never an error");
    }

    private static CardDefinition Stronghold(int startingHonor) =>
        CardCatalog.Cards.Values.First(d => d.Type == CardType.Stronghold && d.StartingHonor == startingHonor);

    private static PlayerView WithStronghold(PlayerView p, CardDefinition d) => p with { Stronghold = p.Stronghold with { Definition = d } };

    private static void Profile()
    {
        var game = MatchEngine.CreateStarterMatch(); var seat = game.GetView(0).Players[0];
        var high5 = Stronghold(5); var low2 = Stronghold(2); var mid4 = Stronghold(4);
        Check(BotProfile.Assess(WithStronghold(seat, high5)).Tier == HonorTier.High, "starting Honor 5 is high");
        Check(BotProfile.Assess(WithStronghold(seat, mid4), 2.6).Tier == HonorTier.High, "an average above 2.5 is high");
        Check(BotProfile.Assess(WithStronghold(seat, low2)).Tier == HonorTier.Low, "starting Honor under 3 is low");
        Check(BotProfile.Assess(WithStronghold(seat, mid4), 1.2).Tier == HonorTier.Low, "an average under 1.5 is low");
        Check(BotProfile.Assess(WithStronghold(seat, mid4), 2.0).Tier == HonorTier.Intermediate, "in between");
        Check(BotProfile.Assess(WithStronghold(seat, high5), 1.2).Tier == HonorTier.Intermediate, "conflicting signals are intermediate");
        var high = new BotProfile(HonorTier.High, 5, 3); var low = new BotProfile(HonorTier.Low, 2, 1); var mid = new BotProfile(HonorTier.Intermediate, 4, 2);
        Check(high.StanceFor(0, 30, false) == ProclaimStance.Prioritize, "high always proclaims");
        Check(low.StanceFor(20, 10, false) == ProclaimStance.Deprioritize && low.StanceFor(20, 10, true) == ProclaimStance.Prioritize, "low proclaims only with spare Gold");
        Check(mid.StanceFor(10, 14, false) == ProclaimStance.Prioritize && mid.StanceFor(14, 10, false) == ProclaimStance.Prioritize, "in between: close (within 4) or ahead is a priority");
        Check(mid.StanceFor(10, 15, false) == ProclaimStance.Neutral && mid.StanceFor(10, 16, false) == ProclaimStance.Deprioritize, "behind by 5 is neutral, by more than 5 deprioritized");
        Check(high.PrefersOwnGain(0, 40) && !low.PrefersOwnGain(40, 0), "high gains, low takes it away");
        Check(mid.PrefersOwnGain(12, 10) && mid.PrefersOwnGain(3, 10) && !mid.PrefersOwnGain(8, 10), "in between: gain when ahead or below the starting Honor (4), otherwise take it away");
    }

    private static void Planner()
    {
        AttackPlanner.Unit U(string id, int f) => new(id, f);
        AttackPlanner.Target T(int i, int s) => new(i, s);
        var three = AttackPlanner.Plan([U("a", 6), U("b", 5), U("c", 4), U("d", 3)], [T(0, 2), T(1, 3), T(2, 8)], 0);
        Check(three is not null && three.Values.Distinct().Count() == 3, "three armies when each can beat its province");
        var forces = new Dictionary<int, int>(); var all = new[] { ("a", 6), ("b", 5), ("c", 4), ("d", 3) }.ToDictionary(p => p.Item1, p => p.Item2);
        foreach (var (id, province) in three!) forces[province] = forces.GetValueOrDefault(province) + all[id];
        Check(forces[0] > 2 && forces[1] > 3 && forces[2] > 8, "every army beats its own province");
        var one = AttackPlanner.Plan([U("a", 6), U("b", 5), U("c", 4), U("d", 3)], [T(0, 2), T(1, 3)], 10);
        Check(one is not null && one.Values.Distinct().Count() == 1 && one.Values.First() == 0 && one.Count == 4, "defenders make two armies impossible: one army at the weakest province with everyone");
        Check(AttackPlanner.Plan([U("a", 3), U("b", 2)], [T(0, 6)], 0) is null, "not even one army clears the threshold: no attack");
        Check(AttackPlanner.Plan([], [T(0, 1)], 0) is null && AttackPlanner.Plan([U("a", 5)], [], 0) is null, "nothing to send or nowhere to send it");
    }

    private static (MatchView View, ActionOption Pass, Func<string, ActionOption> Ability) Table()
    {
        var game = MatchEngine.CreateStarterMatch(); var view = game.GetView(0);
        return (view, new("pass", "Pass", true, null, new("pass", 0, view.Version)),
            id => new("useAbility", id, true, null, new("useAbility", 0, view.Version, CardId: "source-" + id, AbilityId: id), "source-" + id));
    }

    private static void NeverBelowPass()
    {
        var (view, pass, ability) = Table();
        // Aya's Cavalry grant has no hand-written rule, no target and no keyword the classifier reads as an effect: it still beats passing.
        var chosen = TrainingOpponent.Choose(view, [pass, ability("aya-cavalry")]);
        Check(chosen.Command?.AbilityId == "aya-cavalry", "an unknown ability must be played over passing, got " + (chosen.Command?.AbilityId ?? chosen.Command?.Type));
        var many = AbilityCatalog.All.Where(p => !p.Key.StartsWith("training-", StringComparison.Ordinal) && p.Value.Timing is "Open" or "Limited" && p.Value.Target == "none" && !p.Value.PlayerAbility).Take(40).Select(p => p.Key).ToArray();
        foreach (var id in many)
        {
            var decision = TrainingOpponent.Choose(view, [pass, ability(id)]);
            var intent = AbilityIntent.Of(id);
            if (intent.TakesFavor || intent.DiscardsFavor || intent.SelfHonorLoss) continue; // spending or taking the Favor, or losing the bot's own Honor, has its own spec; a tie with passing goes to passing (Akuma's Power joined the first 40 once Ring of the Void's recruit left the Limited timing, issue #1157)
            Check(decision.Command?.AbilityId == id, $"{id} scored below passing");
        }
    }

    private static PlayerView WithHand(PlayerView p, int count) => p with { HandCount = count, HandLimit = new HandLimitView(8, 8, []) };

    private static void DrawGrowsWithGap()
    {
        var (view, pass, ability) = Table();
        var draw = ability("korimi-exp2-draw"); var rival = ability("shunya-honor"); // Discard Favor: 100
        Check(AbilityIntent.Of("korimi-exp2-draw").Draws, "korimi-exp2-draw reads as a draw");
        string Pick(int hand)
        {
            var v = view with { Players = [WithHand(view.Players[0], hand), view.Players[1]] };
            return TrainingOpponent.Choose(v, [pass, draw, rival]).Command!.AbilityId!;
        }
        Check(Pick(8) == "shunya-honor", "a full hand: drawing is worth little");
        Check(Pick(1) == "korimi-exp2-draw", "a nearly empty hand: drawing outranks everything else on offer");
    }

    private static void ArenaFixes()
    {
        var (view, pass, ability) = Table();
        var person = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force > 0);
        // 1. Self-harm: Tsujiken's "Dishonor him. His controller loses 1 Honor" aimed at the bot's own Personality scores below passing; aimed at the opponent's it is played.
        var mine = Person("mine-1", 0, person); var theirs = Person("theirs-1", 1, person);
        var harmView = view with { Players = [view.Players[0] with { Home = [mine] }, view.Players[1] with { Home = [theirs] }] };
        ActionOption Aimed(string id, string target) => new("useAbility", id, true, null, new("useAbility", 0, view.Version, CardId: "source-" + id, TargetId: target, AbilityId: id), "source-" + id);
        Check(AbilityIntent.Of("tsujiken-exp-dishonor").Rank >= EffectRank.Penalize, "Tsujiken's ability reads as harmful");
        Check(TrainingOpponent.Choose(harmView, [pass, Aimed("tsujiken-exp-dishonor", "mine-1")]).Command?.Type == "pass", "a harmful ability at its own Personality: it passes");
        Check(TrainingOpponent.Choose(harmView, [pass, Aimed("tsujiken-exp-dishonor", "theirs-1")]).Command?.AbilityId == "tsujiken-exp-dishonor", "...but at the opponent's it is played");
        Check(AbilityIntent.Of("scars-of-war-mark").HonorLoss, "Scars of War reads as an Honor loss");
        Check(TrainingOpponent.Choose(harmView, [pass, Aimed("scars-of-war-mark", "mine-1")]).Command?.Type == "pass", "an Honor-loss ability at its own card: it passes");
        // 2. Shinden: always bow, whatever the Honor totals.
        ActionOption Shinden(string choice) => new("chooseShinden", choice, true, null, new("chooseShinden", 0, view.Version, Choice: choice));
        foreach (var opponentHonor in new[] { 1, 20, 36 })
        {
            // The threatened Personality is an unbowed one with Force, the case where the old rule allowed the Honor gain while the opponent was under 35.
            var v = harmView with { Players = [harmView.Players[0], harmView.Players[1] with { Honor = opponentHonor }], PendingChoice = new PendingChoice("shinden", 0, "mine-1", "Shinden Asahina", 1) };
            Check(TrainingOpponent.Choose(v, [Shinden("honor"), Shinden("bow")], new BotContext { UseSpec818 = false }).Command?.Choice == "bow", $"Shinden bows rather than give the opponent Honor (opponent at {opponentHonor}); issue #818 refines this, see Spec818Tests");
        }
        // 3. A pure draw ability at a full hand is worth no more than passing; with room in the hand it is played.
        var draw = ability("korimi-exp2-draw");
        string Pick(int hand) => TrainingOpponent.Choose(view with { Players = [WithHand(view.Players[0], hand), view.Players[1]] }, [pass, draw]).Command!.Type;
        Check(Pick(8) == "pass" && Pick(9) == "pass", "a full hand: the Gold stays for purchases");
        Check(Pick(6) == "useAbility", "room in the hand: draw");
        // 4. Honor: never the opponent gaining it and never the bot losing it when it chooses who.
        ActionOption Choice(string type, string key) => new(type, key, true, null, new(type, 0, view.Version, Choice: key));
        foreach (var gain in new[] { 2, 30 })
            foreach (var lose in new[] { 2, 30 })
            {
                var v = view with { Players = [view.Players[0] with { Honor = gain }, view.Players[1] with { Honor = lose }] };
                var picked = TrainingOpponent.Choose(v, [Choice("chooseEffect", "opponent-gain"), Choice("chooseEffect", "self-lose"), Choice("chooseEffect", "self-gain"), Choice("chooseEffect", "opponent-lose")]).Command!.Choice;
                Check(picked is "self-gain" or "opponent-lose", $"an effect choice never picks {picked}");
            }
        Check(TrainingOpponent.Choose(view, [Choice("chooseEitherOr", "honor"), Choice("chooseEitherOr", "pay")]).Command?.Choice == "pay", "an Either/Or that costs Honor loses to any other answer");
        Check(TrainingOpponent.Choose(view, [Choice("chooseEitherOr", "honor"), Choice("chooseEitherOr", "other")]).Command?.Choice == "other", "...even an unremarkable one");
        var checkedPlayers = 0;
        foreach (var (id, def) in AbilityCatalog.All.Where(p => p.Value.Target == "player" && !p.Key.StartsWith("training-", StringComparison.Ordinal)))
        {
            var intent = AbilityIntent.Of(id);
            if (intent.HonorGain == intent.HonorLoss) continue; // both (a profile choice) or neither
            var chosen = TrainingOpponent.Choose(view, [ability(id)]).Command;
            if (chosen?.TargetPlayerId is not int player) continue;
            Check(intent.HonorGain ? player == 0 : player == 1, $"{id}: a player who gains Honor is the bot, one who loses it is the opponent (got player {player})");
            checkedPlayers++;
        }
        Check(checkedPlayers > 0, "the catalogue sweep found choose-a-player Honor abilities to check");
    }

    private static void HoshokuNeverSelf()
    {
        var (view, pass, _) = Table();
        var person = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force > 0);
        var hoshoku = Person("hoshoku-1", 0, person); var ally = Person("ally-1", 0, person); var theirs = Person("theirs-1", 1, person);
        var v = view with { Players = [view.Players[0] with { Home = [hoshoku, ally] }, view.Players[1] with { Home = [theirs] }] };
        const string id = "wl1-hoshoku-destroy";
        Check(AbilityIntent.Of(id).Rank == EffectRank.Destroy, "Hoshoku-sha's action reads as a destroy");
        ActionOption Aimed(string target) => new("useAbility", id, true, null, new("useAbility", 0, v.Version, CardId: "hoshoku-1", TargetId: target, AbilityId: id), "hoshoku-1");
        Check(TrainingOpponent.Choose(v, [pass, Aimed("hoshoku-1")]).Command?.Type == "pass", "aimed at himself: the bot passes");
        Check(TrainingOpponent.Choose(v, [pass, Aimed("ally-1")]).Command?.Type == "pass", "aimed at its own Personality: the bot passes");
        Check(TrainingOpponent.Choose(v, [pass, Aimed("hoshoku-1"), Aimed("ally-1")]).Command?.Type == "pass", "with only its own cards legal it still passes");
    }

    // Read the Essence (issue #699): the bot scouts an enemy face-down province card, never turns its own for nothing, answers the optional second flip, and never stalls.
    private static void ReadTheEssenceBot()
    {
        var (view, pass, _) = Table();
        const string id = WlEssenceAbilities.Flip;
        ActionOption Flip(int owner) => new("useAbility", id, true, null, new("useAbility", 0, view.Version, CardId: "spell-1", AbilityId: id, TargetPlayerId: owner, Province: 1), "spell-1");
        Check(TrainingOpponent.Choose(view, [pass, Flip(1)]).Command?.TargetPlayerId == 1, "an enemy face-down card is turned");
        Check(TrainingOpponent.Choose(view, [pass, Flip(0)]).Command?.Type == "pass", "its own face-down card is not turned for nothing");
        ActionOption Answer(string choice) => new("chooseEffect", choice, true, null, new("chooseEffect", 0, view.Version, Choice: choice));
        var second = TrainingOpponent.Choose(view, [Answer(WlEssenceAbilities.Decline), Answer("wl-essence-flip:0:1"), Answer("wl-essence-flip:1:2")]).Command!.Choice;
        Check(second == "wl-essence-flip:1:2", "the second flip scouts the enemy's card (got " + second + ")");
        Check(TrainingOpponent.Choose(view, [Answer("wl-essence-flip:0:1"), Answer(WlEssenceAbilities.Decline)]).Command!.Choice == WlEssenceAbilities.Decline, "with only its own card left it declines the second flip");
    }

    private static void StrengthInTerrorBot()
    {
        var (view, pass, _) = Table();
        var person = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force > 0);
        var attachment = Person("theirs-att", 1, person) with { Zone = Zone.Attached, AttachedTo = "theirs-1" };
        var own = Person("own-att", 0, person) with { Zone = Zone.Attached, AttachedTo = "own-1" };
        var v = view with { Players = [view.Players[0] with { Home = [own] }, view.Players[1] with { Home = [Person("theirs-1", 1, person), attachment] }] };
        ActionOption Answer(string choice) => new("chooseEffect", choice, true, null, new("chooseEffect", 0, v.Version, Choice: choice));
        Check(TrainingOpponent.Choose(v, [Answer("sit-decline"), Answer("sit-destroy:theirs-att")]).Command!.Choice == "sit-destroy:theirs-att", "an opponent's attachment is destroyed");
        Check(TrainingOpponent.Choose(v, [Answer("sit-destroy:theirs-att"), Answer("sit-decline")]).Command!.Choice == "sit-destroy:theirs-att", "whatever the order offered");
        Check(TrainingOpponent.Choose(v, [Answer("sit-destroy:own-att"), Answer("sit-decline")]).Command!.Choice == "sit-decline", "never its own attachment");
    }

    private static void LastStandAndHonorRace()
    {
        var (view, pass, _) = Table();
        var person = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force >= 4);
        CardView Unit(string id, int owner, int? province) => Person(id, owner, person) with { Zone = province is null ? Zone.Home : Zone.Battlefield, ProvinceIndex = province, Force = 6 };
        ProvinceView[] Provinces(int count) => Enumerable.Range(0, count).Select(i => new ProvinceView(i, 3, false, null, null)).ToArray();
        // Two Provinces left, both attacked by 12 Force (3 + defense can not save them with 6 Force at home against 12 apiece): the bot defends anyway.
        var attacked = view with
        {
            Players = [view.Players[0] with { Home = [Unit("own-1", 0, null)], Army = [], Provinces = Provinces(2) },
            view.Players[1] with { Home = [], Army = [Unit("a-0", 1, 0), Unit("a-0b", 1, 0), Unit("a-1", 1, 1), Unit("a-1b", 1, 1)] }]
        };
        var defend = new ActionOption("assignDefense", "Defend", true, null, new("assignDefense", 0, attacked.Version, Assignments: []), TargetIds: ["own-1"], Provinces: [0, 1]);
        Check(TrainingOpponent.Choose(attacked, [pass, defend]).Command?.Type == "assignDefense", "every Province would fall: the bot defends");
        // Honor race: the opponent holds 40 Honor and a bigger home army. Without the race the bot stays home; with it, it attacks.
        var wall = Enumerable.Range(0, 3).Select(i => Unit("wall-" + i, 1, null)).ToArray();
        var race = view with { Players = [view.Players[0] with { Home = [Unit("own-1", 0, null), Unit("own-2", 0, null)] }, view.Players[1] with { Honor = 40, Home = wall, Provinces = Provinces(4) }] };
        var calm = race with { Players = [race.Players[0], race.Players[1] with { Honor = 20 }] };
        var declare = (MatchView v) => new ActionOption("declareAttack", "Declare", true, null, new("declareAttack", 0, v.Version));
        Check(TrainingOpponent.Choose(calm, [pass, declare(calm)]).Command?.Type == "pass", "no Honor race and a larger army at home: no attack");
        Check(TrainingOpponent.Choose(race, [pass, declare(race)]).Command?.Type == "declareAttack", "the opponent wins on Honor next turn: the bot attacks regardless of the army at home");
    }

    private static CardView Person(string id, int owner, CardDefinition d) => new(id, d, owner, Zone.Home, true, false, null, null, d.Force);

    private static MatchView Races(MatchView view, int ownStart, int oppStart, int ownPeople, int oppPeople, int? favorHolder = null)
    {
        var person = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Force > 0);
        PlayerView Make(PlayerView p, int start, int people, int owner) => WithStronghold(p, Stronghold(start)) with { Home = Enumerable.Range(0, people).Select(i => Person($"p{owner}-{i}", owner, person)).ToArray() };
        return view with { FavorPlayerId = favorHolder, Players = [Make(view.Players[0], ownStart, ownPeople, 0), Make(view.Players[1], oppStart, oppPeople, 1)] };
    }

    private static void Lobby()
    {
        var (view, pass, ability) = Table();
        var lobby = ability("lobby"); var free = ability("makoto-favor"); var rival = ability("shunya-honor");
        string Pick(MatchView v, params ActionOption[] options) => TrainingOpponent.Choose(v, options).Command!.AbilityId ?? "pass";
        var ahead = Races(view, 5, 3, 3, 2);
        Check(Pick(ahead, pass, lobby, rival) == "lobby", "higher Family Honor and more Personalities: always lobby");
        var fewer = Races(view, 5, 3, 2, 3);
        Check(Pick(fewer, pass, lobby, rival) == "shunya-honor", "fewer Personalities: lobbying (which bows) yields to anything else on offer");
        Check(Pick(fewer, pass, lobby) == "lobby", "...but still beats passing");
        Check(Pick(fewer, pass, free, rival) == "makoto-favor", "...unless it costs no bow: an Open action that simply takes the Favor is a priority");
        var middling = Races(view, 3, 5, 3, 2);
        Check(Pick(middling, pass, lobby, rival) == "shunya-honor" && Pick(middling, pass, lobby) == "lobby", "in between: moderate");
    }

    private static void FavorSpend()
    {
        var (view, pass, ability) = Table();
        var spend = ability("p5-nara-provinces"); var rival = ability("shunya-honor"); // "Discard the Imperial Favor: give each of your provinces +2 strength" vs 100
        Check(AbilityIntent.Of("p5-nara-provinces").DiscardsFavor, "reads as a Favor cost");
        string Pick(MatchView v) => TrainingOpponent.Choose(v, [pass, spend, rival]).Command!.AbilityId ?? "pass";
        var mine = Races(view, 5, 3, 3, 2, favorHolder: 0);
        Check(Pick(mine) == "p5-nara-provinces", "ahead on both: it can lobby the Favor back, so it spends it");
        var cannotRetake = Races(view, 3, 5, 3, 2, favorHolder: 0);
        Check(Pick(cannotRetake) == "shunya-honor", "no way to be sure of getting it back: spending the Favor is worth no more than passing");
        var taker = CardCatalog.Cards.Values.First(d => d.Abilities?.Contains("makoto-favor") == true);
        var withFreeTaker = cannotRetake with { Players = [cannotRetake.Players[0] with { Home = cannotRetake.Players[0].Home.Append(Person("taker", 0, taker)).ToArray() }, cannotRetake.Players[1]] };
        Check(Pick(withFreeTaker) == "p5-nara-provinces", "an Open action that takes the Favor back: spend it freely");
    }

    private static void HonorChoice()
    {
        var (view, _, _) = Table();
        ActionOption Option(string key) => new("chooseEffect", key, true, null, new("chooseEffect", 0, view.Version, Choice: key));
        string Pick(MatchView v) => TrainingOpponent.Choose(v, [Option("self-gain"), Option("opponent-gain"), Option("self-lose"), Option("opponent-lose")]).Command!.Choice!;
        MatchView At(int start, int own, int opp, double average)
        {
            var v = view with { Players = [WithStronghold(view.Players[0], Stronghold(start)) with { Honor = own }, view.Players[1] with { Honor = opp }] };
            return v;
        }
        // The bot reads its own average from the Personalities it has seen; give it three of known Personal Honor through its hand.
        CardView Seen(string id, int honor) => Person(id, 0, CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.PersonalHonor == honor));
        MatchView WithAverage(MatchView v, int honor) => v with { Players = [v.Players[0] with { Home = [Seen("a", honor), Seen("b", honor), Seen("c", honor)] }, v.Players[1]] };
        Check(Pick(WithAverage(At(5, 10, 30, 0), 3)) == "self-gain", "a high deck gains Honor");
        Check(Pick(WithAverage(At(2, 30, 10, 0), 1)) == "opponent-lose", "a low deck makes the opponent lose Honor");
        Check(Pick(WithAverage(At(4, 12, 10, 0), 2)) == "self-gain", "in between and ahead: gain");
        Check(Pick(WithAverage(At(4, 8, 10, 0), 2)) == "opponent-lose", "in between, behind and above the starting Honor: take it away");
        Check(Pick(WithAverage(At(4, 3, 10, 0), 2)) == "self-gain", "in between and below the starting Honor: gain");
    }

    private static void DecisionInfraBot()
    {
        foreach (var (kind, spec) in new[] { ("p25-multi", "multi-pick Either/Or"), ("p25-unknown", "hook-registered pending type"), ("p25-shown", "shown choice") })
        {
            var engine = MatchEngine.CreateDecisionShowcase(kind);
            Check(engine.State.PendingChoice is not null && engine.State.PendingChoice.PlayerId == 0, $"{spec}: the showcase opens a decision for seat 0");
            var steps = 0; var picks = new List<string>();
            while (engine.State.PendingChoice is not null && steps++ < 8)
            {
                var actions = engine.QueryActions(0);
                Check(actions.Any(a => a.Enabled && a.Command is not null && a.Type != "concede"), $"{spec}: the engine offers an answer");
                var decision = TrainingOpponent.Choose(engine.GetView(0), actions);
                Check(decision.Command is not null && decision.Command.Type != "concede", $"{spec}: the bot answers ({decision.Reason})");
                picks.Add(decision.Command!.Choice ?? "");
                var result = engine.Execute(decision.Command);
                Check(result.Accepted, $"{spec}: the engine accepts the bot's answer: {result.Error}");
            }
            Check(engine.State.PendingChoice is null, $"{spec}: the decision is answered within {steps} steps (stalled)");
            if (kind == "p25-multi") Check(picks.Count == 2 && picks[1] == "done", "multi-pick: the bot takes the one pick it must and then Done: " + string.Join(",", picks));
        }
    }

    private static void SearchExtensionsBot()
    {
        foreach (var kind in new[] { "p20-provinces", "p20-other", "p20-attach" })
        {
            var engine = MatchEngine.CreateSearchShowcase(kind);
            Check(engine.State.PendingChoice is not null, $"{kind}: the showcase opens a decision");
            var steps = 0; var answers = new List<string>();
            while (engine.State.PendingChoice is not null && steps++ < 8)
            {
                var seat = engine.State.PendingChoice.PlayerId;
                var actions = engine.QueryActions(seat);
                Check(actions.Any(a => a.Enabled && a.Command is not null && a.Type != "concede"), $"{kind}: the engine offers seat {seat} an answer at {engine.State.PendingChoice.Type}");
                var decision = TrainingOpponent.Choose(engine.GetView(seat), actions);
                Check(decision.Command is not null && decision.Command.Type != "concede", $"{kind}: the bot answers ({decision.Reason})");
                answers.Add(decision.Command!.Type);
                var result = engine.Execute(decision.Command);
                Check(result.Accepted, $"{kind}: the engine accepts the bot's answer {decision.Command.Type}: {result.Error}");
            }
            Check(engine.State.PendingChoice is null && engine.State.PendingSearch is null, $"{kind}: the search is answered within {steps} steps (stalled): {string.Join(",", answers)}");
            if (kind == "p20-provinces") Check(answers is ["selectSearchCards"], "a province search takes the engine's default selection: " + string.Join(",", answers));
            if (kind == "p20-other") Check(answers is ["selectSearchCards", "chooseShown"], "the searcher takes two Items and the other seat chooses one: " + string.Join(",", answers));
            if (kind == "p20-attach") Check(answers is ["chooseSearchAttachTarget"], "the bot picks an attach target: " + string.Join(",", answers));
        }
    }

    private static void Memory()
    {
        var game = MatchEngine.CreateStarterMatch(); var view = game.GetView(0);
        var plain = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Keywords?.Contains("Unique") != true);
        var unique = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Keywords?.Contains("Unique") == true);
        var seen = view with { Players = [view.Players[0], view.Players[1] with { Discard = [new("d1", plain, 1, Zone.FateDiscard, true, false, null, null, 1), new("d2", plain, 1, Zone.FateDiscard, true, false, null, null, 1), new("u1", unique, 1, Zone.FateDiscard, true, false, null, null, 1)] }] };
        var memory = new BotContext(); memory.Observe(seen);
        Check(memory.Seen(plain.Name) == 2 && memory.ExpectedHiddenCopies(plain.Name) == 1, "two seen copies leave one expected");
        Check(memory.ExpectedHiddenCopies(unique.Name) == 0, "a Unique card seen once has no more copies");
        memory.Observe(view with { Players = [view.Players[0], view.Players[1] with { Discard = [] }] });
        Check(memory.Seen(plain.Name) == 2, "a card seen once stays seen after it leaves the public zone");
        Check(BotContext.Roll(3, "a", "b") == BotContext.Roll(3, "a", "b"), "the roll is deterministic");
        var mean = Enumerable.Range(0, 2000).Average(v => BotContext.Roll(v, "ability", "card"));
        Check(mean is > .45 and < .55 && Enumerable.Range(0, 2000).All(v => BotContext.Roll(v, "x", null) is >= 0 and < 1), $"the roll is fair and in range (mean {mean:F3})");
    }
}
