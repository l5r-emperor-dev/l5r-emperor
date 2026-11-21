using L5R.Core;
using static RegressionAssertions;

// Issue #833, slice 3: single-action cards whose own preconditions the generic kitchen-sink probe of MultiWindowAuditTests could not meet (or whose effect no test checks). Each
// ability gets a hand-built fixture that satisfies its real preconditions, then is proved offered AND accepted in every window its printed timing allows, refused in every other
// window (a direct command is rejected with the state unchanged), and checked against its printed text with concrete state assertions. A scenario is a (phase, who acts) pair: the
// attacker/defender of a Battle phase (player 1 attacks) and the active/non-active player of an Action phase (player 0 is active).
internal static partial class SingleAudit3Tests
{
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static string Rejection(MatchEngine e, GameCommand c) => Batch336Support.Rejection(e, c);
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    private sealed record W(int Me, int Foe, bool Battle, string Scenario)
    {
        public Zone Zone => Battle ? Zone.Battlefield : Zone.Home;
        public int? Province => Battle ? 0 : null;
    }
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null, bool SlotTarget = false, string? SlotReason = null);

    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);

    // The card (definition id) that prints an ability: looked up so a fixture never hard-codes an oracle id.
    private static string CardOf(string ability) => CardCatalog.Cards.Values.First(c => !c.CreatedOnly && (c.Abilities ?? []).Contains(ability)).Id;
    // A copy of the instance with a printed Force, so an Attack's strength can be matched to it exactly.
    private static CardInstance WithForce(CardInstance c, int force) { c.Printed = CardCatalog.Get(c.DefinitionId) with { Force = force }; return c; }
    private static int ForceOf(MatchEngine e, string id) => e.CardForceForTest(id);
    private static int ChiOf(MatchEngine e, string id) => e.PersonalityChiForTest(id);
    private static void Ninja(CardInstance c, params string[] keywords) { foreach (var k in keywords) c.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); }

    // The target of the command: the fixture's own, or (SlotTarget) the first card of the first slot, which some slot actions require to be their target.
    private static string? TargetOf(Fx f) => f.Target ?? (f.SlotTarget ? f.Sel?.FirstOrDefault()?.CardIds?.FirstOrDefault() : null);

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
        if (option is null)
        {
            // A slot action whose Selections the engine cannot guess is listed disabled with the reason that its slots are unfilled; that listing is the offer, and the fixture's selections complete it.
            if (f.SlotReason is not null && f.Sel is not null && Options(e, f.Me, f.Ability, f.Card, f.Performer).Any(o => !o.Enabled && o.Reason == f.SlotReason)) return Use(e, f.Me, f.Ability, f.Card, TargetOf(f), f.Performer, f.Sel);
            return null;
        }
        var command = option.Command! with { TargetId = TargetOf(f) ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
        if (command.SourceIds is null)
        {
            var quote = f.Target is not null && option.TargetGold is not null && option.TargetGold.TryGetValue(f.Target, out var tq) ? tq : option.Gold;
            if (quote is { Payments.Length: > 0 }) command = command with { SourceIds = quote.Payments[0].SourceIds };
        }
        return command;
    }

    // Declines every reaction window the action opened (nobody holds a Reaction in these fixtures), so the action has fully resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 20 && e.State.PendingChoice is not null; guard++)
        {
            var step = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "passReaction" or "pass");
            if (step is null) break;
            Accept(e, step.Command!);
        }
    }

    private static readonly string[] Scenarios = ["battle-attacker", "battle-defender", "action-active", "action-other"];
    private static (MatchState State, W Window) Scene(string scenario)
    {
        var battle = scenario.StartsWith("battle");
        var me = scenario is "battle-defender" or "action-active" ? 0 : 1;
        var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0);
        s.PriorityPlayerId = me;
        return (s, new W(me, 1 - me, battle, scenario));
    }

    // Runs one ability through all four windows. `build` lays out the table for the acting player; `effect` sees the state before and the engine after
    // an accepted use. Allowed windows must offer and accept it; every other window must not offer it enabled and must refuse a direct command (with
    // the state unchanged), for the reason `why` gives (default: its timing is unavailable).
    private static void Windows(string ability, Func<MatchState, W, Fx> build, Action<MatchState, MatchEngine, Fx, W>? effect = null, string[]? only = null, Func<string, string>? why = null, bool settle = true)
    {
        var timing = AbilityCatalog.All[ability].Timing;
        var allowed = only ?? MultiWindowAuditTests.Expected(timing);
        foreach (var scenario in Scenarios)
        {
            var (s, w) = Scene(scenario);
            var fx = build(s, w);
            var e = MatchEngine.FromTestState(s);
            var before = e.State;
            var command = Offered(e, fx);
            if (allowed.Contains(scenario))
            {
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
                var result = e.Execute(command!);
                Check(result.Accepted, $"{ability} offered but refused in {scenario}: {result.Error}");
                if (settle) Settle(e);
                effect?.Invoke(before, e, fx, w);
            }
            else
            {
                Check(command is null, $"{ability} [{timing}] must not be offered in {scenario}");
                var direct = Use(e, fx.Me, ability, fx.Card, TargetOf(fx), fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds };
                var json = System.Text.Json.JsonSerializer.Serialize(e.State);
                var error = Rejection(e, direct);
                Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, $"{ability} refused in {scenario} but mutated the state");
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
            }
        }
    }

    // One accepted use in one window, returning the position before and the engine after.
    private static (MatchState Before, MatchEngine Engine, Fx Fx, W Window) Once(string scenario, Func<MatchState, W, Fx> build, bool settle = true)
    {
        var (s, w) = Scene(scenario);
        var fx = build(s, w);
        var e = MatchEngine.FromTestState(s);
        var before = e.State;
        var command = Offered(e, fx);
        Check(command is not null, $"{fx.Ability} was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, fx.Ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        Accept(e, command!);
        if (settle) Settle(e);
        return (before, e, fx, w);
    }

    public static void Run(TestRunner runner) => runner.Run("single-audit-3", [
        ("A Welcome Reprieve: the Open action bars other players' actions from targeting your honorable Personality, in either player's Action phase only", ReprieveBar),
        ("A Welcome Reprieve: it names only an honorable Personality of yours, and your own actions may still target him", ReprieveTargets),
        ("Bayushi Jin-e: the Ninja Open action bows her and gives the target and Jin-e a -1F/-1C Poison token each, in either player's Action phase only", JinePoison),
        ("Cavalry Flankers: the Attacker bows it for a Melee 2 Attack that kills a Force-2 enemy; the Defender and the Action phase may not", CavalryFlankersAttack),
        ("Cavalry Flankers: the Melee 2 Attack does not destroy a Force-3 enemy", CavalryFlankersStrength),
        ("Blighted Region: the Defender bows it to move his Personality to the battlefield; opposed he straightens and gains 2 Honor; the Attacker and the Action phase may not", BlightedRegionMove),
        ("Blighted Region: a moved Personality that is not opposed neither straightens nor gains Honor", BlightedRegionUnopposed),
        ("Mirumoto Katagi: the Battle action equips a non-Unique Weapon from the discard pile to him, paying its cost, as either side of a battle only", KatagiEquip),
        ("Mirumoto Katagi: only a non-Unique Weapon in his own discard pile is a target", KatagiTargets),
        ("Yogo Nobukai: on your own turn the Open action gives a Personality -2F, in either player's Action phase with a Courtier and only on your turn without", NobukaiOwnTurn),
        ("Yogo Nobukai: out of turn the Open action needs a Courtier you control", NobukaiCourtier),
        ("Soshi Kodanshi: the Limited action bows a Ninja of yours and a Fudo or Fallen Personality that will not straighten this turn, for the active player's Action phase only", KodanshiFudo),
        ("Soshi Kodanshi: a Personality that is neither Fudo nor Fallen is bowed and straightens as usual", KodanshiPlain),
        ("Kage's Teachings: out of turn the Open action bows the item and the chosen player bows one of his unbowed Personalities, in the non-active player's Action phase only", KageBow),
        ("Ikoma Satoru: an accepted gift goes to the other player's hand and the giver draws two, for the active player's Action phase only", SatoruAccepted),
        ("Ikoma Satoru: a refused gift is discarded and a Melee Attack of its Focus Value kills a Force-3 Personality in his home, for the active player's Action phase only", SatoruRefused),
        ("Zansho: the Battle action bows one of his own Followers for a Ranged Attack of its Force, as either side of a battle only", ZanshoRanged),
        ("Purveyor of Questionable Goods: a Spider Clan player with four or fewer cards in hand bows it to draw a card, for the active player's Action phase only", MarketSpider),
        ("Tattoos & Trinkets: a Dragon Clan player with four or fewer cards in hand bows it to draw a card, for the active player's Action phase only", MarketDragon),
        ("The Ide Caravan: a Unicorn Clan player with four or fewer cards in hand bows it to draw a card, for the active player's Action phase only", MarketUnicorn),
        ("Kitsuki Fujimura: the Limited action bows him and a dishonorable Personality's controller loses 2 Honor, for the active player's Action phase only", FujimuraExpose),
        ("Kitsuki Fujimura: an honorable Personality is no target, and a dishonorable Personality of his own controller costs that controller the 2 Honor", FujimuraTargets),
        ("Hida O-Ushi: the Limited action bows a Personality with lower Force, for the active player's Action phase only", OUshiBow),
        ("Hida O-Ushi: a Personality with equal or higher Force is no target", OUshiTargets),
        ("Rumormonger: the Political Open action bows it and gives a Personality a Force penalty equal to his base Personal Honor, in either player's Action phase only", RumormongerPenalty),
        ("Rumormonger: with no dishonorable Personality under the target's controller the action is not offered", RumormongerCondition),
        ("Legulus: the Limited action searches the Fate deck for a Yodotai Legionnaire Follower and puts it in hand, for the active player's Action phase only", LegulusSearch),
        ("Asahina Kitiaru: the Limited action bows her, finds a Ring in the discard pile, puts it in hand and discards a card, for the active player's Action phase only", KitiaruSearch),
        ("Asahina Kitiaru: with no Ring in the discard pile the search moves on to the Fate deck", KitiaruFateDeck),
        ("Sudden Blockade: the Open action stops cards entering play from the named province this turn after the active player's first turn, in either player's Action phase", BlockadeLock),
        ("Sudden Blockade: it is refused during the active player's first turn", BlockadeFirstTurn),
        ("Training move-to-battle Strategy: the Battle action moves your Personality from home to the current battlefield, as either side of a battle only", TrainingMove),
        ("Vengeful Dismissal: the performing unbowed Personality moves a lower-Chi enemy Personality home, as either side of a battle only", VengefulDismissal),
        ("Control the Field: a Scout performer destroys a Terrain and its player may take an additional Battle action to play a Terrain, as either side of a battle only", ControlFieldDestroy),
        ("Control the Field: a Scout, a Tactician or a 3-Chi Personality may perform it, any other may not", ControlFieldPerformers),
        ("Daigotsu Ishibashi: showing a Dark Virtue card of Focus 3 makes a Ranged Attack of strength 4, as either side of a battle only", IshibashiAttack),
        ("Den of Iniquity: bowing it makes the dishonorable Personality's controller lose Honor equal to his base Personal Honor or 3, and bars attacking this turn, for the active player's Action phase only", DenOfIniquityShame),
        ("Den of Iniquity: a base Personal Honor under 3 is the whole loss", DenOfIniquityLow),
        ("Doji's Guidance: a Crane player discards it to put a non-Unique Strategy with a Political ability from the discard pile in hand, in either player's Action phase only", DojisGuidanceReturn),
        ("Fading Shadows: the Ninja Battle action equips it from the hand to the performing Ninja and the player loses 1 Honor, as either side of a battle only", FadingShadowsEquip),
        ("Fading Shadows: only a Ninja may perform it", FadingShadowsNinja),
        ("Fury of a Mob: with more units than the enemy army two Nonhumans bow and destroy an enemy unit, as either side of a battle only", FuryOfAMobDestroy),
        ("Fury of a Mob: with no more units than the enemy army it is not offered", FuryOfAMobCount),
        ("Isawa Ryuzo: the Fire Open action pairs two Madness Personalities who then cannot be assigned to the same army, in either player's Action phase only", RyuzoPairArmy),
        ("My Ancestor's Steed: bowing it moves your Spirit Personality from home to the current battlefield, as either side of a battle only", SteedMove),
        ("My Ancestor's Steed: Home lets a Spell on a Personality at home use it, and only your own Spirit Personality is a target", SteedHome),
        ("One Action, Two Strikes: after the enemy leader took a Battle action it grants two additional Battle actions, as either side of a battle only", OneActionTwoStrikes),
        ("Requesting Reinforcements: bowing a Scout brings a face-up Scout from your province into play at the battlefield, as either side of a battle only", RequestingReinforcementsRecruit),
        ("Requesting Reinforcements: only a face-up Scout is a target, and with Reconnaissance it grants an additional Battle action", RequestingReinforcementsRecon),
        ("Shameful Death: the Magistrate reduces an enemy dishonorable Personality's Force to 0 and gains it, until the turn ends, as either side of a battle only", ShamefulDeathReduce),
        ("The Price of Honor: if the target's controller shows his hand the acting player discards a card from it, for the active player's Action phase only", PriceOfHonorShow),
        ("The Price of Honor: if the target's controller refuses, his Personality may not be ordered to commit seppuku this turn", PriceOfHonorRefuse),
        ("The Trap is Sprung!: with Reconnaissance a Scout bows an enemy card and its unit for a Crane player only, as either side of a battle only", TrapSprungBow),
        ("The Trap is Sprung!: without Reconnaissance it is not offered", TrapSprungRecon),
        ("Too Much for Mortals: a Spirit bows an enemy Personality, as either side of a battle only", TooMuchBow),
        ("Too Much for Mortals: a Spirit destroys one of an enemy Personality's attachments instead, as either side of a battle only", TooMuchDestroy),
    ]);

    // ---- A Welcome Reprieve -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string Mrika => CardOf("wl1-mrika-bow");

    private static void ReprieveBar() => Windows("w9701-reprieve", (s, w) =>
    {
        var target = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, Mrika, w.Foe, w);
        var card = Add(s, CardOf("w9701-reprieve"), w.Me, Zone.Hand);
        return new(w.Me, "w9701-reprieve", card.Id, target.Id);
    }, (before, e, f, w) =>
    {
        // The other player now holds priority: M'rika (Open: bow a target Personality) would name any Personality, but not the protected one.
        var foe = Batch336Support.Edit(e, st => st.PriorityPlayerId = w.Foe);
        var mrika = foe.State.Cards.Values.Single(c => c.DefinitionId == Mrika && c.OwnerId == w.Foe).Id;
        var offered = Options(foe, w.Foe, "wl1-mrika-bow", mrika).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(offered.Length > 0, "M'rika still has other Personalities to name");
        Check(!offered.Contains(f.Target!), "the protected Personality is not offered to the other player");
        var raw = Use(foe, w.Foe, "wl1-mrika-bow", mrika, f.Target);
        var json = System.Text.Json.JsonSerializer.Serialize(foe.State);
        Rejection(foe, raw);
        Check(System.Text.Json.JsonSerializer.Serialize(foe.State) == json && !foe.State.Cards[f.Target!].Bowed, "the raw command is refused and nothing is bowed");
        var later = Batch336Support.Edit(foe, st => st.TurnNumber++);
        Check(Options(later, w.Foe, "wl1-mrika-bow", mrika).Any(o => o.Enabled && (o.TargetIds?.Contains(f.Target!) ?? false)), "the bar lasts only this phase: in a later turn the Personality may be named again");
    });

    private static void ReprieveTargets()
    {
        var (s, w) = Scene("action-active");
        var honorable = Put(s, "training-crane-samurai", w.Me, w);
        var shamed = Put(s, "training-crane-veteran", w.Me, w); shamed.Dishonorable = true;
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, CardOf("w9701-reprieve"), w.Me, Zone.Hand);
        var mine = Put(s, Mrika, w.Me, w);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "w9701-reprieve", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(honorable.Id) && !targets.Contains(shamed.Id) && !targets.Contains(enemy.Id), "only your honorable Personality can be named: " + string.Join(",", targets));
        Accept(e, Offered(e, new(0, "w9701-reprieve", card.Id, honorable.Id))!);
        Settle(e);
        // The protection is against OTHER players: the owner's own action may still name him.
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0);
        Check(Options(e, 0, "wl1-mrika-bow", mine.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(honorable.Id) ?? false)), "your own M'rika may still target the protected Personality");
    }

    // ---- Bayushi Jin-e ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void JinePoison() => Windows("w9701-jin-e", (s, w) =>
    {
        var jine = Put(s, CardOf("w9701-jin-e"), w.Me, w);
        Ninja(jine, "Ninja");
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w9701-jin-e", jine.Id, target.Id);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        Check(e.State.Cards[f.Card].Bowed, "Jin-e is bowed as the cost");
        Eq(ForceOf(e, f.Target!), ForceOf(old, f.Target!) - 1, "the target has -1F");
        Eq(ChiOf(e, f.Target!), ChiOf(old, f.Target!) - 1, "and -1C");
        Eq(ForceOf(e, f.Card), ForceOf(old, f.Card) - 1, "Jin-e has -1F");
        Eq(ChiOf(e, f.Card), ChiOf(old, f.Card) - 1, "and -1C");
        Eq(e.State.Cards[f.Target!].Tokens.Count(t => t.Type == "Poison"), 1, "one Poison token on the target");
        Eq(e.State.Cards[f.Card].Tokens.Count(t => t.Type == "Poison"), 1, "one Poison token on Jin-e");
    });

    // ---- Cavalry Flankers -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CavalryFlankersAttack() => Windows("cavalry-flankers-attack", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var flankers = Add(s, CardOf("cavalry-flankers-attack"), w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        var enemy = WithForce(Put(s, "training-lion-samurai", w.Foe, w), 2);
        return new(w.Me, "cavalry-flankers-attack", flankers.Id, enemy.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Flankers are bowed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Melee 2 destroyed the Force-2 Personality");
    }, only: ["battle-attacker"], why: sc => sc == "battle-defender" ? "Only the Attacker" : "timing is unavailable now");

    private static void CavalryFlankersStrength()
    {
        var (_, e, f, _) = Once("battle-attacker", (s, w) =>
        {
            var host = Put(s, "training-crane-samurai", w.Me, w);
            var flankers = Add(s, CardOf("cavalry-flankers-attack"), w.Me, Zone.Attached, province: w.Province, parent: host.Id);
            return new(w.Me, "cavalry-flankers-attack", flankers.Id, WithForce(Put(s, "training-lion-samurai", w.Foe, w), 3).Id);
        });
        Check(e.State.Cards[f.Card].Bowed && e.State.Cards[f.Target!].Zone == Zone.Battlefield, "Melee 2 does not destroy a Force-3 Personality");
    }

    // ---- Blighted Region ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BlightedRegionMove() => Windows("blighted-region-move", (s, w) =>
    {
        var region = Add(s, CardOf("blighted-region-move"), w.Me, Zone.Region, province: 0);
        var mover = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "blighted-region-move", region.Id, null, mover.Id);
    }, (before, e, f, w) =>
    {
        var mover = e.State.Cards[f.Performer!];
        Eq(e.State.Cards[f.Card].Bowed, true, "the Region is bowed as the cost");
        Eq(mover.Zone, Zone.Battlefield, "the Personality moved to the battlefield"); Eq(mover.ProvinceIndex, 0, "the current one");
        Check(!mover.Bowed, "opposed, he is straightened");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor + 2, "and his controller gains 2 Honor");
    }, only: ["battle-defender"], why: sc => sc == "battle-attacker" ? "Only the Defender" : "timing is unavailable now");

    private static void BlightedRegionUnopposed()
    {
        var (s, w) = Scene("battle-defender");
        var region = Add(s, CardOf("blighted-region-move"), w.Me, Zone.Region, province: 0);
        var mover = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[w.Me].Honor;
        var command = Offered(e, new(w.Me, "blighted-region-move", region.Id, null, mover.Id));
        Check(command is not null, "offered with no enemy at the battlefield: " + string.Join("; ", Options(e, w.Me, "blighted-region-move", region.Id).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        Accept(e, command!);
        Settle(e);
        Eq(e.State.Cards[mover.Id].Zone, Zone.Battlefield, "he moved");
        Check(e.State.Cards[mover.Id].Bowed, "unopposed, he stays bowed");
        Eq(e.State.Players[w.Me].Honor, honor, "and no Honor is gained");
    }

    // ---- Mirumoto Katagi ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KatagiEquip() => Windows("wl2-katagi-equip", (s, w) =>
    {
        var katagi = Put(s, CardOf("wl2-katagi-equip"), w.Me, w);
        var weapon = Add(s, "training-weapon", w.Me, Zone.FateDiscard);
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        return new(w.Me, "wl2-katagi-equip", katagi.Id, weapon.Id);
    }, (before, e, f, w) =>
    {
        var weapon = e.State.Cards[f.Target!];
        Eq(weapon.Zone, Zone.Attached, "the Weapon is equipped"); Eq(weapon.AttachedTo, f.Card, "to Katagi");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Zone == Zone.Home && c.Bowed) > before.Cards.Values.Count(c => c.OwnerId == f.Me && c.Zone == Zone.Home && c.Bowed), "its Gold cost was paid");
    });

    private static void KatagiTargets()
    {
        var (s, w) = Scene("battle-attacker");
        var katagi = Put(s, CardOf("wl2-katagi-equip"), w.Me, w);
        var plain = Add(s, "training-weapon", w.Me, Zone.FateDiscard);
        var unique = CardCatalog.Cards.Values.Where(c => !c.CreatedOnly && c.Type == CardType.Item && (c.Keywords ?? []).Contains("Weapon") && (c.Keywords ?? []).Contains("Unique")).OrderBy(c => c.Id, StringComparer.Ordinal).First();
        var uniqueWeapon = Add(s, unique.Id, w.Me, Zone.FateDiscard);
        var theirs = Add(s, "training-weapon", w.Foe, Zone.FateDiscard);
        var notWeapon = Add(s, "training-item", w.Me, Zone.FateDiscard);
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, w.Me, "wl2-katagi-equip", katagi.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.SequenceEqual([plain.Id]), "only a non-Unique Weapon in his own discard pile: " + string.Join(",", targets) + " (not " + uniqueWeapon.Id + ", " + theirs.Id + ", " + notWeapon.Id + ")");
    }

    // ---- Yogo Nobukai ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NobukaiWeaken(bool courtier, string[]? only, Func<string, string>? why) => Windows("wl2-nobukai-weaken", (s, w) =>
    {
        var nobukai = Put(s, CardOf("wl2-nobukai-weaken"), w.Me, w);
        if (courtier) Ninja(Put(s, "training-crane-samurai", w.Me, w), "Courtier");
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl2-nobukai-weaken", nobukai.Id, target.Id);
    }, (before, e, f, w) => Eq(ForceOf(e, f.Target!), ForceOf(MatchEngine.FromTestState(before), f.Target!) - 2, "the target has -2F"), only, why);

    private static void NobukaiOwnTurn() => NobukaiWeaken(false, ["action-active"], sc => sc == "action-other" ? "It is not your turn and you control no Courtier" : "timing is unavailable now");
    private static void NobukaiCourtier() => NobukaiWeaken(true, null, null);

    // ---- Soshi Kodanshi -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx KodanshiTable(MatchState s, W w, string? victimKeyword)
    {
        var kodanshi = Put(s, CardOf("wl2-kodanshi-bow"), w.Me, w);
        var ninja = Put(s, "training-crane-samurai", w.Me, w); Ninja(ninja, "Ninja");
        var victim = Put(s, "training-lion-samurai", w.Foe, w);
        if (victimKeyword is not null) Ninja(victim, victimKeyword);
        return new(w.Me, "wl2-kodanshi-bow", kodanshi.Id, null, null, [Pick("ninja", ninja.Id), Pick("target", victim.Id)], SlotTarget: true);
    }

    private static void KodanshiFudo() => Windows("wl2-kodanshi-bow", (s, w) => KodanshiTable(s, w, "Fallen"), (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Sel![0].CardIds![0]].Bowed, "the Ninja is bowed");
        Check(!e.State.Cards[f.Card].Bowed, "Kodanshi herself is not the cost");
        var victim = f.Sel![1].CardIds![0];
        Check(e.State.Cards[victim].Bowed, "the target is bowed");
        Check(!e.StraightenForTest(victim), "a Fallen Personality will not straighten this turn");
    });

    private static void KodanshiPlain()
    {
        var (_, e, f, _) = Once("action-active", (s, w) => KodanshiTable(s, w, null));
        var victim = f.Sel![1].CardIds![0];
        Check(e.State.Cards[victim].Bowed && e.State.Cards[f.Sel[0].CardIds![0]].Bowed, "both are bowed");
        Check(e.StraightenForTest(victim), "a Personality that is neither Fudo nor Fallen straightens as usual");
        (_, e, f, _) = Once("action-active", (s, w) => KodanshiTable(s, w, "Fudo"));
        Check(!e.StraightenForTest(f.Sel![1].CardIds![0]), "a Fudo Personality will not straighten either");
    }

    // ---- Kage's Teachings -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KageBow() => Windows("wl-kage-bow", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var kage = Add(s, CardOf("wl-kage-bow"), w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        Put(s, "training-lion-samurai", w.Foe, w); Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "wl-kage-bow", kage.Id, TargetPlayer: w.Foe);
    }, (before, e, f, w) =>
    {
        var unbowedBefore = before.Cards.Values.Where(c => c.OwnerId == w.Foe && c.Zone is Zone.Battlefield or Zone.Home && !c.Bowed && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality).Select(c => c.Id).Order(StringComparer.Ordinal).ToArray();
        Check(e.State.Cards[f.Card].Bowed, "the item is bowed as the cost");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the chosen player decides"); Eq(e.State.PendingChoice!.PlayerId, w.Foe, "and only he");
        Check(!e.QueryActions(f.Me).Any(o => o.Type == "chooseDecider"), "the acting player chooses nothing");
        var pick = e.QueryActions(w.Foe).Where(o => o.Type == "chooseDecider" && o.Command!.TargetId is not null).Select(o => o.Command!.TargetId!).Order(StringComparer.Ordinal).ToArray();
        Check(pick.SequenceEqual(unbowedBefore) && pick.Length >= 2, "exactly his unbowed Personalities are offered: " + string.Join(",", pick));
        Accept(e, new GameCommand("chooseDecider", w.Foe, e.Version, TargetId: pick[0]));
        Eq(e.State.PendingChoice, null, "the choice is made");
        Check(e.State.Cards[pick[0]].Bowed, "the chosen Personality is bowed");
        Check(pick.Skip(1).All(id => !e.State.Cards[id].Bowed), "and no other");
    }, only: ["action-other"], why: sc => sc == "action-active" ? "only when it is not your turn" : "timing is unavailable now", settle: false);

    // ---- Ikoma Satoru ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static ActionOption EitherOr(MatchEngine e, int player, string labelStart) =>
        e.QueryActions(player).First(o => o.Type == "chooseEitherOr" && o.Label.StartsWith(labelStart, StringComparison.Ordinal));

    private static Fx SatoruTable(MatchState s, W w, int foeForce)
    {
        var satoru = Put(s, CardOf("w821-satoru-gift"), w.Me, w);
        var gift = Add(s, "training-focus-strategy", w.Me, Zone.Hand);
        Add(s, "training-crane-samurai", w.Me, Zone.Hand);
        WithForce(Put(s, "training-lion-samurai", w.Foe, w), foeForce);
        return new(w.Me, "w821-satoru-gift", satoru.Id, gift.Id);
    }

    private static void SatoruAccepted() => Windows("w821-satoru-gift", (s, w) => SatoruTable(s, w, 3), (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the other player decides"); Eq(e.State.PendingChoice!.PlayerId, w.Foe, "and only he");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == "training-focus-strategy"), "the card is shown to him");
        Accept(e, EitherOr(e, w.Foe, "Put ").Command!);
        Check(e.State.Players[w.Foe].Hand.Contains(f.Target!) && !e.State.Players[w.Me].Hand.Contains(f.Target!), "the gift is in his hand");
        Eq(e.State.Players[w.Me].Hand.Count, before.Players[w.Me].Hand.Count - 1 + 2, "the giver draws two cards");
        Check(before.Cards.Values.Where(c => c.OwnerId == w.Foe).All(c => e.State.Cards[c.Id].Zone == c.Zone), "no attack was made: his cards did not move");
    }, settle: false);

    private static void SatoruRefused() => Windows("w821-satoru-gift", (s, w) => SatoruTable(s, w, 3), (before, e, f, w) =>
    {
        Accept(e, EitherOr(e, w.Foe, "Refuse").Command!);
        Eq(e.State.Cards[f.Target!].Zone, Zone.FateDiscard, "the gift is discarded");
        Eq(e.State.Players[w.Me].Hand.Count, before.Players[w.Me].Hand.Count - 1, "nothing is drawn");
        var gone = before.Cards.Values.Where(c => c.OwnerId == w.Foe && c.Zone != e.State.Cards[c.Id].Zone).Select(c => c.Id + ":" + e.State.Cards[c.Id].Zone).ToArray();
        Check(gone.Length == 1 && gone[0].EndsWith(":Dead"), "the Melee 3 Attack (the Focus Value) destroyed one Force-3 Personality in his home and nothing else: " + string.Join(",", gone) + " " + e.State.PendingChoice?.Type);
    }, settle: false);

    // ---- Zansho ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx ZanshoTable(MatchState s, W w, int enemyForce)
    {
        var zansho = Put(s, CardOf("w827-zansho-ranged"), w.Me, w);
        var follower = Add(s, "training-follower", w.Me, Zone.Attached, province: w.Province, parent: zansho.Id);
        var enemy = WithForce(Put(s, "training-lion-samurai", w.Foe, w), enemyForce);
        return new(w.Me, "w827-zansho-ranged", zansho.Id, enemy.Id, CardIds: [follower.Id]);
    }

    private static void ZanshoRanged()
    {
        Windows("w827-zansho-ranged", (s, w) => ZanshoTable(s, w, CardCatalog.Get("training-follower").Force), (before, e, f, w) =>
        {
            Check(e.State.Cards[f.CardIds![0]].Bowed, "the Follower is bowed as the cost");
            Check(!e.State.Cards[f.Card].Bowed, "Zansho is not");
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Ranged Attack of the Follower's Force destroyed an enemy of that Force");
        });
        var (_, e, f, _) = Once("battle-attacker", (s, w) => ZanshoTable(s, w, CardCatalog.Get("training-follower").Force + 1));
        Check(e.State.Cards[f.CardIds![0]].Bowed && e.State.Cards[f.Target!].Zone == Zone.Battlefield, "one more Force survives it");
    }

    // ---- The Clan market Holdings --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Deck(MatchState s, string def, int owner)
    {
        var c = Add(s, def, owner, Zone.FateDeck); s.Players[owner].FateDeck.Add(c.Id); c.FaceUp = false; return c;
    }

    private static void Market(string ability, string clan)
    {
        Windows(ability, (s, w) =>
        {
            s.Players[w.Me].Clan = clan;
            var holding = Add(s, CardOf(ability), w.Me, Zone.Home);
            for (var i = 0; i < 4; i++) Add(s, "training-focus-strategy", w.Me, Zone.Hand);
            return new(w.Me, ability, holding.Id);
        }, (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Card].Bowed, "the Holding is bowed as the cost");
            Eq(e.State.Players[w.Me].Hand.Count, 5, "four cards in hand became five");
            Eq(e.State.Players[w.Me].FateDeck.Count, before.Players[w.Me].FateDeck.Count - 1, "from his Fate deck");
        });
        // Five cards in hand, or another Clan, and nothing may be drawn: not offered and the raw command is refused with the state unchanged.
        foreach (var (hand, other) in new[] { (5, clan), (4, clan == "Crab" ? "Lion" : "Crab") })
        {
            var (s, w) = Scene("action-active");
            s.Players[w.Me].Clan = other;
            var holding = Add(s, CardOf(ability), w.Me, Zone.Home);
            for (var i = 0; i < hand; i++) Add(s, "training-focus-strategy", w.Me, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(Offered(e, new(w.Me, ability, holding.Id)) is null, $"{ability} must not be offered with {hand} cards in hand as a {other} player");
            var json = System.Text.Json.JsonSerializer.Serialize(e.State);
            Rejection(e, Use(e, w.Me, ability, holding.Id));
            Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and the refused command changed nothing");
        }
    }

    private static void MarketSpider() => Market("market-draw-spider", "Spider");
    private static void MarketDragon() => Market("market-draw-dragon", "Dragon");
    private static void MarketUnicorn() => Market("market-draw-unicorn", "Unicorn");

    // ---- Kitsuki Fujimura -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FujimuraExpose() => Windows("fujimura-expose", (s, w) =>
    {
        var fujimura = Put(s, CardOf("fujimura-expose"), w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Foe, w); target.Dishonorable = true;
        return new(w.Me, "fujimura-expose", fujimura.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "Fujimura is bowed as the cost");
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor - 2, "the controller loses 2 Honor");
        Eq(e.State.Players[w.Me].Honor, before.Players[w.Me].Honor, "and his own player loses none");
    });

    private static void FujimuraTargets()
    {
        var (s, w) = Scene("action-active");
        var fujimura = Put(s, CardOf("fujimura-expose"), w.Me, w);
        var honorable = Put(s, "training-lion-samurai", w.Foe, w);
        var mine = Put(s, "training-crane-samurai", w.Me, w); mine.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, w.Me, "fujimura-expose", fujimura.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(mine.Id) && !targets.Contains(honorable.Id), "only a dishonorable Personality is a target: " + string.Join(",", targets));
        var honor = e.State.Players[w.Me].Honor;
        Accept(e, Offered(e, new(w.Me, "fujimura-expose", fujimura.Id, mine.Id))!);
        Eq(e.State.Players[w.Me].Honor, honor - 2, "his controller loses 2 Honor, even when that is the acting player");
    }

    // ---- Hida O-Ushi ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int OUshiForce => CardCatalog.Get(CardOf("o-ushi-inexp-bow")).Force;

    private static void OUshiBow() => Windows("o-ushi-inexp-bow", (s, w) =>
    {
        var oushi = Put(s, CardOf("o-ushi-inexp-bow"), w.Me, w);
        var weaker = WithForce(Put(s, "training-lion-samurai", w.Foe, w), OUshiForce - 1);
        return new(w.Me, "o-ushi-inexp-bow", oushi.Id, weaker.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the lower-Force Personality is bowed");
        Check(!e.State.Cards[f.Card].Bowed, "O-Ushi herself stays straight");
    });

    private static void OUshiTargets()
    {
        var (s, w) = Scene("action-active");
        var oushi = Put(s, CardOf("o-ushi-inexp-bow"), w.Me, w);
        var weaker = WithForce(Put(s, "training-lion-samurai", w.Foe, w), OUshiForce - 1);
        var equal = WithForce(Put(s, "training-lion-samurai", w.Foe, w), OUshiForce);
        var stronger = WithForce(Put(s, "training-lion-samurai", w.Foe, w), OUshiForce + 1);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, w.Me, "o-ushi-inexp-bow", oushi.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(weaker.Id) && !targets.Contains(equal.Id) && !targets.Contains(stronger.Id), "only a lower Force Personality is a target: " + string.Join(",", targets));
    }

    // ---- Rumormonger ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx RumormongerTable(MatchState s, W w, bool dishonorable)
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var rumor = Add(s, CardOf("rumormonger-penalty"), w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        target.Printed = CardCatalog.Get("training-lion-samurai") with { PersonalHonor = 3 };
        target.Dishonorable = dishonorable;
        return new(w.Me, "rumormonger-penalty", rumor.Id, target.Id);
    }

    private static void RumormongerPenalty() => Windows("rumormonger-penalty", (s, w) => RumormongerTable(s, w, true), (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Follower is bowed as the cost");
        Eq(ForceOf(e, f.Target!), ForceOf(MatchEngine.FromTestState(before), f.Target!) - 3, "a Force penalty equal to his base Personal Honor (3), not his current 0 while dishonorable");
    });

    private static void RumormongerCondition()
    {
        var (s, w) = Scene("action-active");
        var fx = RumormongerTable(s, w, false);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, fx) is null, "no dishonorable Personality under his controller, so the action is not offered");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Rejection(e, Use(e, w.Me, fx.Ability, fx.Card, fx.Target));
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and the raw command changes nothing");
    }

    // ---- Legulus and Asahina Kitiaru: searches ---------------------------------------------------------------------------------------------------------------------------------------

    private const string Legionnaire = "oracle-l5r-10688", RingOfAir = "oracle-l5r-6333", RingOfFire = "oracle-l5r-6335";

    private static void LegulusSearch() => Windows("wl1-legulus-search", (s, w) =>
    {
        var legulus = Put(s, CardOf("wl1-legulus-search"), w.Me, w);
        Deck(s, "training-follower", w.Me); Deck(s, Legionnaire, w.Me);
        return new(w.Me, "wl1-legulus-search", legulus.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.PlayerId, w.Me, "the searcher decides");
        var picks = e.QueryActions(w.Me).Where(o => o.Type == "selectSearchCard").Select(o => o.Command!.Choice).ToArray();
        Check(picks.SequenceEqual([Legionnaire]), "only the Yodotai Legionnaire qualifies: " + string.Join(",", picks));
        var hand = e.State.Players[w.Me].Hand.Count;
        Accept(e, e.QueryActions(w.Me).Single(o => o.Type == "selectSearchCard").Command!);
        Eq(e.State.Players[w.Me].Hand.Count, hand + 1, "it is put in his hand");
        Check(e.State.Players[w.Me].Hand.Any(id => e.State.Cards[id].DefinitionId == Legionnaire), "the Legionnaire itself");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == Legionnaire), "and it was shown");
        Eq(e.State.Players[w.Me].FateDeck.Count, before.Players[w.Me].FateDeck.Count - 1, "it left the Fate deck");
        Eq(e.State.PendingChoice, null, "the action is finished");
    }, settle: false);

    private static void KitiaruSearch() => Windows("wl1-kitiaru-search", (s, w) =>
    {
        var kitiaru = Put(s, CardOf("wl1-kitiaru-search"), w.Me, w);
        Add(s, RingOfAir, w.Me, Zone.FateDiscard); Add(s, "training-weapon", w.Me, Zone.FateDiscard);
        Deck(s, RingOfFire, w.Me);
        Add(s, "training-spell", w.Me, Zone.Hand); Add(s, "training-rally", w.Me, Zone.Hand);
        return new(w.Me, "wl1-kitiaru-search", kitiaru.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "Kitiaru is bowed as the cost");
        var picks = e.QueryActions(w.Me).Where(o => o.Type == "selectSearchCard").Select(o => o.Command!.Choice).ToArray();
        Check(picks.SequenceEqual([RingOfAir]), "the discard pile is searched first and only its Ring qualifies: " + string.Join(",", picks));
        var ring = e.State.Cards.Values.Single(c => c.OwnerId == w.Me && c.DefinitionId == RingOfAir && c.Zone == Zone.FateDiscard).Id;
        Accept(e, e.QueryActions(w.Me).Single(o => o.Type == "selectSearchCard").Command!);
        Check(e.State.Players[w.Me].Hand.Contains(ring), "the Ring is in his hand");
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "and he must discard a card");
        var handBefore = e.State.Players[w.Me].Hand.Count;
        var discard = e.State.Players[w.Me].Hand.First(id => id != ring);
        Accept(e, new GameCommand("chooseSearchDiscard", w.Me, e.Version, TargetId: discard));
        Eq(e.State.Cards[discard].Zone, Zone.FateDiscard, "the chosen card is discarded");
        Eq(e.State.Players[w.Me].Hand.Count, handBefore - 1, "one in, one out");
        Check(e.State.Players[w.Me].Hand.Contains(ring), "the Ring stays in hand");
        Eq(e.State.Players[w.Me].FateDeck.Count, before.Players[w.Me].FateDeck.Count, "the Fate deck was not searched or reduced");
        Eq(e.State.PendingChoice, null, "the action is finished");
    }, settle: false);

    private static void KitiaruFateDeck()
    {
        var (s, w) = Scene("action-active");
        var kitiaru = Put(s, CardOf("wl1-kitiaru-search"), w.Me, w);
        Add(s, "training-weapon", w.Me, Zone.FateDiscard);
        var ring = Deck(s, RingOfFire, w.Me);
        Add(s, "training-spell", w.Me, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offered(e, new(w.Me, "wl1-kitiaru-search", kitiaru.Id))!);
        Check(!e.QueryActions(w.Me).Any(o => o.Type == "selectSearchCard"), "no Ring in the discard pile");
        Accept(e, e.QueryActions(w.Me).First(o => o.Type == "passSearch" && o.Command!.Choice != "stop").Command!);
        Accept(e, e.QueryActions(w.Me).Single(o => o.Type == "selectSearchCard").Command!);
        Check(e.State.Players[w.Me].Hand.Contains(ring.Id), "the Fate deck's Ring is in his hand");
        Accept(e, new GameCommand("chooseSearchDiscard", w.Me, e.Version, TargetId: e.State.Players[w.Me].Hand.First(id => id != ring.Id)));
        Eq(e.State.PendingChoice, null, "the action is finished");
    }

    // ---- Sudden Blockade -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BlockadeLock() => Windows("w9701-blockade", (s, w) =>
    {
        s.Players[s.ActivePlayerId].TurnsTaken = 2;
        foreach (var province in s.Players.SelectMany(p => p.Provinces)) s.Cards[province.CardId!].FaceUp = true; // a purchase quote asks for a face-up province card
        var card = Add(s, CardOf("w9701-blockade"), w.Me, Zone.Hand);
        return new(w.Me, "w9701-blockade", card.Id, Province: 1, TargetPlayer: w.Foe);
    }, (before, e, f, w) =>
    {
        string? Locked(MatchEngine x, int owner, int province) => x.QuotePurchase(owner, x.State.Players[owner].Provinces[province].CardId!).Error;
        Check(Locked(e, w.Foe, 1) is { } why && why.Contains("will not enter play from province 2"), "the named province is locked: " + Locked(e, w.Foe, 1));
        Check(Locked(e, w.Foe, 0) is not { } other || !other.Contains("will not enter play"), "another province of his is not");
        Check(Locked(e, w.Me, 1) is not { } mine || !mine.Contains("will not enter play"), "nor is the same province of the other player");
        Check(Locked(Batch336Support.Edit(e, st => st.TurnNumber++), w.Foe, 1) is not { } later || !later.Contains("will not enter play"), "the lock lasts only this turn");
    });

    private static void BlockadeFirstTurn()
    {
        var (s, w) = Scene("action-active");
        s.Players[0].TurnsTaken = 1;
        var card = Add(s, CardOf("w9701-blockade"), 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, new(0, "w9701-blockade", card.Id, Province: 1, TargetPlayer: 1)) is null, "not offered during the active player's first turn");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Rejection(e, Use(e, 0, "w9701-blockade", card.Id) with { Province = 1, TargetPlayerId = 1 });
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and the raw command changes nothing");
    }

    // ---- Training move-to-battle and Vengeful Dismissal ---------------------------------------------------------------------------------------------------------------------

    private static void TrainingMove() => Windows("training-move-to-battle", (s, w) =>
    {
        var card = Add(s, CardOf("training-move-to-battle"), w.Me, Zone.Hand);
        var mover = Add(s, "training-crane-samurai", w.Me, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "training-move-to-battle", card.Id, mover.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the Personality left home for the battlefield"); Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "the current one");
    });

    private static Fx VengefulTable(MatchState s, W w, int targetChi)
    {
        var card = Add(s, CardOf("vengeful-dismissal-move"), w.Me, Zone.Hand);
        var performer = Put(s, "training-crane-samurai", w.Me, w);
        performer.Printed = CardCatalog.Get("training-crane-samurai") with { Chi = 4 };
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        enemy.Printed = CardCatalog.Get("training-lion-samurai") with { Chi = targetChi };
        return new(w.Me, "vengeful-dismissal-move", card.Id, enemy.Id, performer.Id);
    }

    private static void VengefulDismissal()
    {
        Windows("vengeful-dismissal-move", (s, w) => VengefulTable(s, w, 3), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the enemy Personality was moved home"); Eq(e.State.Cards[f.Target!].OwnerId, w.Foe, "his own home");
            Eq(e.State.Cards[f.Performer!].Zone, Zone.Battlefield, "the performer stays at the battlefield");
        });
        var (s, w) = Scene("battle-attacker");
        var fx = VengefulTable(s, w, 4);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, fx) is null, "an enemy Personality of equal Chi is no target");
    }

    // ---- Control the Field ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private const string ExtendedManeuvers = "oracle-l5r-2419", FloodedPass = "oracle-l5r-2613";

    private static CardInstance WithChi(CardInstance c, int chi) { c.Printed = CardCatalog.Get(c.DefinitionId) with { Chi = chi }; return c; }

    private static void ControlFieldDestroy() => Windows("control-field-destroy", (s, w) =>
    {
        var card = Add(s, CardOf("control-field-destroy"), w.Me, Zone.Hand);
        var scout = Put(s, "training-scout-samurai", w.Me, w);
        var terrain = Add(s, ExtendedManeuvers, w.Foe, Zone.Battlefield, province: 0);
        Add(s, FloodedPass, w.Me, Zone.Hand);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "control-field-destroy", card.Id, terrain.Id, scout.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.FateDiscard, "the Terrain was destroyed");
        var handTerrain = e.State.Players[w.Me].Hand.Single(id => e.State.Cards[id].DefinitionId == FloodedPass);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == w.Me && g.ScopeCardIds is { } ids && ids.Contains(handTerrain)), "an additional Battle action to play the Terrain in his hand");
        Eq(e.State.PriorityPlayerId, w.Me, "its player keeps priority");
    });

    private static void ControlFieldPerformers()
    {
        var (s, w) = Scene("battle-attacker");
        var card = Add(s, CardOf("control-field-destroy"), w.Me, Zone.Hand);
        var weak = WithChi(Put(s, "training-crane-samurai", w.Me, w), 2);
        var tactician = WithChi(Put(s, "training-lion-samurai", w.Me, w), 2); Ninja(tactician, "Tactician");
        var scout = WithChi(Put(s, "training-scout-samurai", w.Me, w), 2);
        var strong = WithChi(Put(s, "training-crane-veteran", w.Me, w), 3);
        var terrain = Add(s, ExtendedManeuvers, w.Foe, Zone.Battlefield, province: 0);
        Put(s, "training-lion-samurai", w.Foe, w);
        var e = MatchEngine.FromTestState(s);
        bool Can(CardInstance p) => Offered(e, new(w.Me, "control-field-destroy", card.Id, terrain.Id, p.Id)) is not null;
        Check(Can(scout) && Can(tactician) && Can(strong), "a Scout, a Tactician and a 3-Chi Personality may perform");
        Check(!Can(weak), "a 2-Chi Personality that is neither may not");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Rejection(e, Use(e, w.Me, "control-field-destroy", card.Id, terrain.Id, weak.Id));
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and the raw command changes nothing");
    }

    // ---- Daigotsu Ishibashi ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx IshibashiTable(MatchState s, W w, int enemyForce)
    {
        var ishibashi = Put(s, CardOf("ishibashi-attack"), w.Me, w);
        var virtue = Add(s, "training-dark-virtue", w.Me, Zone.Hand);
        virtue.Printed = CardCatalog.Get("training-dark-virtue") with { Focus = 3 };
        var enemy = WithForce(Put(s, "training-lion-samurai", w.Foe, w), enemyForce);
        return new(w.Me, "ishibashi-attack", ishibashi.Id, enemy.Id, CardIds: [virtue.Id]);
    }

    private static void IshibashiAttack()
    {
        Windows("ishibashi-attack", (s, w) => IshibashiTable(s, w, 4), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Ranged 4 (Focus 3 plus one) destroyed the Force-4 Personality");
            Eq(e.State.Cards[f.CardIds![0]].Zone, Zone.Hand, "the shown Dark Virtue stays in his hand");
            Check(e.State.ShownCards.Any(c => c.DefinitionId == "training-dark-virtue"), "and was shown");
        });
        var (_, e, f, _) = Once("battle-attacker", (s, w) => IshibashiTable(s, w, 5));
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "Force 5 survives strength 4");
    }

    // ---- Den of Iniquity ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx DenTable(MatchState s, W w, int personalHonor)
    {
        var den = Add(s, CardOf("pe-den-of-iniquity-shame"), w.Me, Zone.Home);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        target.Printed = CardCatalog.Get("training-lion-samurai") with { PersonalHonor = personalHonor };
        target.Dishonorable = true;
        return new(w.Me, "pe-den-of-iniquity-shame", den.Id, target.Id);
    }

    private static void DenOfIniquityShame() => Windows("pe-den-of-iniquity-shame", (s, w) => DenTable(s, w, 5), (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Holding is bowed as the cost");
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor - 3, "base Personal Honor 5 caps at a loss of 3");
        Eq(e.State.Players[w.Me].Honor, before.Players[w.Me].Honor, "the acting player loses none");
        Check(e.State.Players[w.Me].MayNotAttackThisTurn, "and may not declare an attack this turn");
    });

    private static void DenOfIniquityLow()
    {
        var (before, e, f, w) = Once("action-active", (s, w) => DenTable(s, w, 2));
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor - 2, "base Personal Honor 2 is the whole loss");
    }

    // ---- Doji's Guidance ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string PoliticalStrategy => CardCatalog.Cards.Values.Where(c => !c.CreatedOnly && c.Type == CardType.Strategy && !(c.Keywords ?? []).Contains("Unique")
        && (c.Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && (d.ActionKeywords ?? []).Contains("Political"))).OrderBy(c => c.Id, StringComparer.Ordinal).First().Id;

    private static Fx DojiTable(MatchState s, W w, string clan)
    {
        s.Players[w.Me].Clan = clan;
        var guidance = Add(s, CardOf("dojis-guidance-return"), w.Me, Zone.Celestial);
        var political = Add(s, PoliticalStrategy, w.Me, Zone.FateDiscard);
        Add(s, "training-focus-strategy", w.Me, Zone.FateDiscard);
        return new(w.Me, "dojis-guidance-return", guidance.Id, political.Id);
    }

    private static void DojisGuidanceReturn()
    {
        Windows("dojis-guidance-return", (s, w) => DojiTable(s, w, "Crane"), (before, e, f, w) =>
        {
            Check(e.State.Players[w.Me].Hand.Contains(f.Target!), "the Political Strategy is back in his hand");
            Check(e.State.Cards[f.Card].Zone != Zone.Celestial, "the Celestial was discarded as the cost");
        });
        var (s, w) = Scene("action-active");
        var fx = DojiTable(s, w, "Crane");
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, w.Me, fx.Ability, fx.Card).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.SequenceEqual([fx.Target!]), "only the Political Strategy is a target, not the other discard: " + string.Join(",", targets));
        var (s2, w2) = Scene("action-active");
        var other = DojiTable(s2, w2, "Lion");
        var e2 = MatchEngine.FromTestState(s2);
        Check(Offered(e2, other) is null, "a non-Crane player may not");
        var json = System.Text.Json.JsonSerializer.Serialize(e2.State);
        Rejection(e2, Use(e2, w2.Me, other.Ability, other.Card, other.Target));
        Check(System.Text.Json.JsonSerializer.Serialize(e2.State) == json, "and the raw command changes nothing");
    }

    // ---- Fading Shadows ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx FadingTable(MatchState s, W w, bool ninja)
    {
        var shadows = Add(s, CardOf("fading-shadows-equip"), w.Me, Zone.Hand);
        var performer = Put(s, "training-crane-samurai", w.Me, w); if (ninja) Ninja(performer, "Ninja");
        for (var i = 0; i < 3; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "fading-shadows-equip", shadows.Id, null, performer.Id);
    }

    private static void FadingShadowsEquip() => Windows("fading-shadows-equip", (s, w) => FadingTable(s, w, true), (before, e, f, w) =>
    {
        var follower = e.State.Cards[f.Card];
        Eq(follower.Zone, Zone.Attached, "the Follower is equipped"); Eq(follower.AttachedTo, f.Performer, "to the performing Ninja");
        Eq(e.State.Players[w.Me].Honor, before.Players[w.Me].Honor - 1, "after it enters play, he loses 1 Honor");
        Eq(CardCatalog.Get(follower.DefinitionId).GoldCost, 0, "its printed Gold cost");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == w.Me && c.Bowed), before.Cards.Values.Count(c => c.OwnerId == w.Me && c.Bowed), "so no Gold source is bowed");
    });

    private static void FadingShadowsNinja()
    {
        var (s, w) = Scene("battle-attacker");
        var fx = FadingTable(s, w, false);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, fx) is null, "a Personality that is not a Ninja may not perform it");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Rejection(e, Use(e, w.Me, fx.Ability, fx.Card, null, fx.Performer));
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and the raw command changes nothing");
    }

    // ---- Fury of a Mob ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx FuryTable(MatchState s, W w, int ownExtra)
    {
        var card = Add(s, CardOf("fury-of-a-mob-destroy"), w.Me, Zone.Hand);
        var first = Put(s, "training-crane-samurai", w.Me, w); Ninja(first, "Nonhuman");
        var second = Put(s, "training-crane-veteran", w.Me, w); Ninja(second, "Nonhuman");
        for (var i = 0; i < ownExtra; i++) Put(s, "training-lion-veteran", w.Me, w);
        var enemy = Put(s, "training-lion-veteran", w.Foe, w);
        Add(s, "training-follower", w.Foe, Zone.Attached, province: w.Province, parent: enemy.Id);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "fury-of-a-mob-destroy", card.Id, Sel: [Pick("performers", first.Id, second.Id), Pick("target", enemy.Id)]);
    }

    private static void FuryOfAMobDestroy() => Windows("fury-of-a-mob-destroy", (s, w) => FuryTable(s, w, 1), (before, e, f, w) =>
    {
        Check(f.Sel![0].CardIds!.All(id => e.State.Cards[id].Bowed), "both Nonhumans were bowed as the cost");
        var enemy = f.Sel[1].CardIds![0];
        Eq(e.State.Cards[enemy].Zone, Zone.Dead, "the enemy unit was destroyed");
        Check(e.State.Cards.Values.Where(c => c.AttachedTo == enemy).All(c => c.Zone != Zone.Attached), "with its Follower");
    });

    private static void FuryOfAMobCount()
    {
        var (s, w) = Scene("battle-attacker");
        var fx = FuryTable(s, w, 0); // two units against two
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, fx) is null, "equal units: not offered");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Check(Rejection(e, Use(e, w.Me, fx.Ability, fx.Card, null, null, fx.Sel!)).Contains("more units"), "the reason names the unit count");
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and nothing changes");
    }

    // ---- Isawa Ryuzo --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RyuzoPairArmy() => Windows("w821-ryuzo-pair", (s, w) =>
    {
        var ryuzo = Put(s, CardOf("w821-ryuzo-pair"), w.Me, w); Ninja(ryuzo, "Fire");
        CardInstance Mad(string def)
        {
            var c = Put(s, def, w.Me, w);
            c.Printed = CardCatalog.Get(def) with { PersonalHonor = 0 };
            c.Tokens.Add(new($"{c.Id}-madness", "Madness", "test"));
            return c;
        }
        var a = Mad("training-crane-samurai"); var b = Mad("training-crane-veteran");
        return new(w.Me, "w821-ryuzo-pair", ryuzo.Id, Sel: [Pick(Wave821Abilities.PairSlot, a.Id, b.Id)], SlotReason: "Target two different Personalities.");
    }, (before, e, f, w) =>
    {
        Eq(e.State.Restrictions.Items.Count(r => r.Kind == RestrictionKind.NoSameArmy), 1, "one pair is stored");
        var ids = f.Sel![0].CardIds!;
        var attack = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.PriorityPlayerId = w.Me; st.ActivePlayerId = w.Me; });
        Reject(attack, new GameCommand("attack", w.Me, attack.Version, Assignments: new Dictionary<string, int> { [ids[0]] = 0, [ids[1]] = 0 }));
        Accept(attack, new GameCommand("attack", w.Me, attack.Version, Assignments: new Dictionary<string, int> { [ids[0]] = 0, [ids[1]] = 1 }));
    });

    // ---- My Ancestor's Steed ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx SteedTable(MatchState s, W w, bool hostAtHome)
    {
        var host = hostAtHome ? Add(s, "training-shugenja", w.Me, Zone.Home) : Put(s, "training-shugenja", w.Me, w);
        Ninja(host, "Water");
        var spell = Add(s, CardOf("ancestors-steed-move"), w.Me, Zone.Attached, province: hostAtHome ? null : w.Province, parent: host.Id);
        var spirit = Add(s, "training-crane-samurai", w.Me, Zone.Home); Ninja(spirit, "Spirit");
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "ancestors-steed-move", spell.Id, spirit.Id);
    }

    private static void SteedMove() => Windows("ancestors-steed-move", (s, w) => SteedTable(s, w, false), (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Spell is bowed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the Spirit moved from home to the battlefield"); Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "the current one");
    });

    private static void SteedHome()
    {
        var (before, e, f, _) = Once("battle-defender", (s, w) => SteedTable(s, w, true));
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "with the Spell's Personality at home, Home lets the Spirit still move");
        var (s, w) = Scene("battle-defender");
        var fx = SteedTable(s, w, false);
        var foeSpirit = Add(s, "training-lion-veteran", w.Foe, Zone.Home); Ninja(foeSpirit, "Spirit");
        var plain = Add(s, "training-crane-veteran", w.Me, Zone.Home);
        var elsewhere = Put(s, "training-crane-veteran", w.Me, w); Ninja(elsewhere, "Spirit");
        var engine = MatchEngine.FromTestState(s);
        var targets = Options(engine, w.Me, fx.Ability, fx.Card).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(fx.Target!) && targets.Contains(elsewhere.Id) && !targets.Contains(foeSpirit.Id) && !targets.Contains(plain.Id), "only your own Spirits, at any location: " + string.Join(",", targets));
    }

    // ---- One Action, Two Strikes --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OneActionTwoStrikes()
    {
        foreach (var owner in new[] { 1, 0 }) // the Attacker, then the Defender
        {
            var foe = 1 - owner;
            var s = Table(Phase.Battle, 1);
            s.PriorityPlayerId = foe; // the enemy leader acts first
            var ownBower = Add(s, "training-battle-bower", owner, Zone.Battlefield, province: 0);
            var ownTarget = Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
            var foeBower = Add(s, "training-battle-bower", foe, Zone.Battlefield, province: 0);
            var foeTarget = Add(s, "training-lion-samurai", foe, Zone.Battlefield, province: 0);
            var strategy = Add(s, CardOf("one-action-two-strikes"), owner, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            int Grants() => e.State.AdditionalActions.Count(g => g.PlayerId == owner);
            // The enemy leader has taken no Battle action yet (a pass is not one): the Strategy may not be played, even when its player holds priority.
            var early = Batch336Support.Edit(e, st => st.PriorityPlayerId = owner);
            Check(Offered(early, new(owner, "one-action-two-strikes", strategy.Id)) is null, $"player {owner}: not offered before the enemy leader acts");
            Check(Rejection(early, Use(early, owner, "one-action-two-strikes", strategy.Id)).Contains("enemy leader"), "and refused for that reason");
            Accept(e, Offered(e, new(foe, "training-bow-at-battle", foeBower.Id, ownTarget.Id))!); // the enemy leader's Battle action
            Eq(e.State.PriorityPlayerId, owner, "priority passes to the other player");
            Accept(e, Offered(e, new(owner, "one-action-two-strikes", strategy.Id))!);
            Eq(Grants(), 2, $"player {owner}: two additional Battle actions");
            Eq(e.State.PriorityPlayerId, owner, "he keeps priority");
            Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            Accept(e, Offered(e, new(owner, "training-bow-at-battle", ownBower.Id, foeTarget.Id))!);
            Check(e.State.Cards[foeTarget.Id].Bowed, "the first additional action resolved");
            Eq(Grants(), 1, "one of the two is left");
            Eq(e.State.PriorityPlayerId, owner, "he still holds priority");
        }
        foreach (var (phase, me) in new[] { (Phase.Action, 0), (Phase.Action, 1) })
        {
            var s = Table(phase, 0); s.PriorityPlayerId = me;
            var strategy = Add(s, CardOf("one-action-two-strikes"), me, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(Offered(e, new(me, "one-action-two-strikes", strategy.Id)) is null, $"player {me}: not offered in the Action phase");
            var json = System.Text.Json.JsonSerializer.Serialize(e.State);
            Check(Rejection(e, Use(e, me, "one-action-two-strikes", strategy.Id)).Contains("timing is unavailable now"), "and refused as untimely");
            Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "with nothing changed");
        }
    }

    // ---- Requesting Reinforcements ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance InProvince(MatchState s, string def, int owner, int province)
    {
        var slot = s.Players[owner].Provinces[province];
        if (slot.CardId is not null) { s.Cards[slot.CardId].Zone = Zone.DynastyDiscard; s.Cards[slot.CardId].ProvinceIndex = null; }
        var card = Add(s, def, owner, Zone.Province, province: province);
        slot.CardId = card.Id;
        return card;
    }

    private static Fx ReinforcementsTable(MatchState s, W w)
    {
        var request = Add(s, CardOf("requesting-reinforcements-recruit"), w.Me, Zone.Hand);
        var scout = Put(s, "training-scout-samurai", w.Me, w);
        var recruit = InProvince(s, "training-scout-samurai", w.Me, 1);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "requesting-reinforcements-recruit", request.Id, recruit.Id, scout.Id);
    }

    private static void RequestingReinforcementsRecruit() => Windows("requesting-reinforcements-recruit", ReinforcementsTable, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed, "the performing Scout is bowed");
        var recruit = e.State.Cards[f.Target!];
        Eq(recruit.Zone, Zone.Battlefield, "the Scout entered play at the battlefield"); Eq(recruit.ProvinceIndex, 0, "the current one");
        Check(e.State.Players[w.Me].Provinces[1].CardId != recruit.Id, "his province was refilled");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == w.Me && c.Bowed) >= before.Cards.Values.Count(c => c.OwnerId == w.Me && c.Bowed) + 2, "he paid the Scout's Gold cost by bowing sources (besides the performer)");
        Eq(e.State.AdditionalActions.Count, 0, "without Reconnaissance no additional action is granted");
    });

    private static void RequestingReinforcementsRecon()
    {
        var (s, w) = Scene("battle-defender");
        var fx = ReinforcementsTable(s, w);
        var plain = InProvince(s, "training-crane-samurai", w.Me, 2);
        var facedown = InProvince(s, "training-scout-samurai", w.Me, 3); facedown.FaceUp = false;
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, w.Me, fx.Ability, fx.Card, fx.Performer).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray(); // issue #1232: one option per way to pay, the same target
        Check(targets.SequenceEqual([fx.Target!]), "only the face-up Scout in a province is a target: " + string.Join(",", targets));
        Check(!targets.Contains(plain.Id) && !targets.Contains(facedown.Id), "not a non-Scout nor a face-down Scout");
        var (s2, w2) = Scene("battle-attacker");
        var fx2 = ReinforcementsTable(s2, w2);
        s2.Players[1 - s2.ActivePlayerId].Provinces[0].Reconnaissance[w2.Me] = true;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offered(e2, fx2)!);
        Settle(e2);
        Eq(e2.State.AdditionalActions.Count(g => g.PlayerId == w2.Me), 1, "with Reconnaissance an additional Battle action is granted");
    }

    // ---- Shameful Death ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShamefulDeathReduce() => Windows("shameful-death-reduce", (s, w) =>
    {
        var card = Add(s, CardOf("shameful-death-reduce"), w.Me, Zone.Hand);
        var magistrate = Put(s, "training-magistrate-personality", w.Me, w);
        var target = Put(s, "training-lion-veteran", w.Foe, w); target.Dishonorable = true;
        return new(w.Me, "shameful-death-reduce", card.Id, target.Id, magistrate.Id);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        var lost = ForceOf(old, f.Target!);
        Check(lost > 0, "the target had Force to lose");
        Eq(ForceOf(e, f.Target!), 0, "the dishonorable target's Force is reduced to 0");
        Eq(ForceOf(e, f.Performer!), ForceOf(old, f.Performer!) + lost, "the Magistrate gains a Force bonus equal to the amount lost");
        // "Until the turn ends": FinishTurn sweeps every non-permanent modifier, so both modifiers must be non-permanent (a whole turn is not replayed here).
        var mods = e.State.Cards.Values.SelectMany(c => c.Modifiers).Where(m => m.Effect == "shameful-death-reduce").ToArray();
        Check(mods.Length == 2 && mods.All(m => !m.Permanent), "both the reduction and the bonus are temporary");
    });

    // ---- The Price of Honor -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx PriceTable(MatchState s, W w)
    {
        var courtier = Put(s, "training-courtier-personality", w.Me, w);
        var price = Add(s, CardOf("p5-price-of-honor"), w.Me, Zone.Hand);
        var target = Put(s, "training-samurai", w.Foe, w); target.Dishonorable = true;
        Add(s, "training-crane-samurai", w.Foe, Zone.Hand); Add(s, "training-lion-veteran", w.Foe, Zone.Hand);
        return new(w.Me, "p5-price-of-honor", price.Id, target.Id, courtier.Id);
    }

    private static void PriceOfHonorShow() => Windows("p5-price-of-honor", PriceTable, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the target's controller decides"); Eq(e.State.PendingChoice!.PlayerId, w.Foe, "and only he");
        Accept(e, new GameCommand("chooseEitherOr", w.Foe, e.Version, CardId: f.Card, Choice: "show"));
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the acting player may discard a card from the shown hand");
        var theirs = e.State.Players[w.Foe].Hand.ToArray();
        var offered = e.QueryActions(w.Me).Where(o => o.Type == "chooseDecider" && o.Command!.TargetId is not null).Select(o => o.Command!.TargetId!).ToArray();
        Check(offered.Length == theirs.Length && theirs.All(offered.Contains), "each card of the shown hand is his to choose from");
        Check(!e.QueryActions(w.Foe).Any(o => o.Type == "chooseDecider"), "the other player is not offered the choice");
        Accept(e, new GameCommand("chooseDecider", w.Me, e.Version, TargetId: theirs[1]));
        Eq(e.State.Cards[theirs[1]].Zone, Zone.FateDiscard, "the chosen card was discarded");
        Eq(e.State.Cards[theirs[0]].Zone, Zone.Hand, "the other stays in hand");
        Eq(e.State.PendingChoice, null, "nothing is left pending");
    }, settle: false);

    private static void PriceOfHonorRefuse() => Windows("p5-price-of-honor", PriceTable, (before, e, f, w) =>
    {
        Accept(e, new GameCommand("chooseEitherOr", w.Foe, e.Version, CardId: f.Card, Choice: "refuse"));
        Eq(e.State.PendingChoice, null, "nothing is left pending");
        var theirs = Batch336Support.Edit(e, st => { st.PriorityPlayerId = w.Foe; st.ActivePlayerId = w.Foe; });
        Check(!theirs.QueryActions(w.Foe).Any(o => o.Type == "orderSeppuku" && o.CardId == f.Target), "seppuku may not be ordered for him this turn");
        Check(Rejection(theirs, new GameCommand("orderSeppuku", w.Foe, theirs.Version, f.Target!)).Contains("Price of Honor"), "and the command is refused");
        var next = Batch336Support.Edit(theirs, st => st.TurnNumber++);
        Check(next.QueryActions(w.Foe).Any(o => o.Type == "orderSeppuku" && o.CardId == f.Target), "next turn it may be ordered again");
    }, settle: false);

    // ---- The Trap is Sprung! ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx TrapTable(MatchState s, W w, bool recon)
    {
        var card = Add(s, CardOf("trap-sprung-bow"), w.Me, Zone.Hand);
        var scout = Put(s, "training-scout-samurai", w.Me, w);
        var target = Put(s, "training-lion-veteran", w.Foe, w);
        Add(s, "training-follower", w.Foe, Zone.Attached, province: w.Province, parent: target.Id);
        if (recon) s.Players[1 - s.ActivePlayerId].Provinces[0].Reconnaissance[w.Me] = true;
        return new(w.Me, "trap-sprung-bow", card.Id, target.Id, scout.Id);
    }

    private static void TrapSprungBow() => Windows("trap-sprung-bow", (s, w) => TrapTable(s, w, true), (before, e, f, w) =>
    {
        var crane = e.State.Players[w.Me].Clan == "Crane";
        Check(e.State.Cards[f.Target!].Bowed, "the target is bowed");
        var follower = e.State.Cards.Values.Single(c => c.AttachedTo == f.Target && CardCatalog.Get(c.DefinitionId).Type == CardType.Follower);
        Eq(follower.Bowed, crane, crane ? "a Crane Clan player bows the target's whole unit" : "a player of another Clan bows only the target");
    });

    private static void TrapSprungRecon()
    {
        var (s, w) = Scene("battle-attacker");
        var fx = TrapTable(s, w, false);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, fx) is null, "without Reconnaissance it is not offered");
        var json = System.Text.Json.JsonSerializer.Serialize(e.State);
        Rejection(e, Use(e, w.Me, fx.Ability, fx.Card, fx.Target, fx.Performer));
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == json, "and the raw command changes nothing");
    }

    // ---- Too Much for Mortals --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static Fx TooMuchTable(MatchState s, W w)
    {
        var card = Add(s, CardOf("too-much-for-mortals"), w.Me, Zone.Hand);
        var spirit = Put(s, "training-crane-samurai", w.Me, w); Ninja(spirit, "Spirit");
        var target = Put(s, "training-lion-veteran", w.Foe, w);
        Add(s, "training-item", w.Foe, Zone.Attached, province: w.Province, parent: target.Id);
        return new(w.Me, "too-much-for-mortals", card.Id, target.Id, spirit.Id);
    }

    private static void TooMuchBow() => Windows("too-much-for-mortals", TooMuchTable, (before, e, f, w) =>
    {
        var item = e.State.Cards.Values.Single(c => c.AttachedTo == f.Target && c.DefinitionId == "training-item").Id;
        var choices = e.QueryActions(w.Me).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice!).ToArray();
        Check(choices.Contains("bow") && choices.Contains($"destroy-{item}"), "bow or destroy: " + string.Join(",", choices));
        Accept(e, new GameCommand("chooseEffect", w.Me, e.Version, Choice: "bow"));
        Check(e.State.Cards[f.Target!].Bowed, "he is bowed");
        Eq(e.State.Cards[item].Zone, Zone.Attached, "and his attachment stays");
    }, settle: false);

    private static void TooMuchDestroy() => Windows("too-much-for-mortals", TooMuchTable, (before, e, f, w) =>
    {
        var item = e.State.Cards.Values.Single(c => c.AttachedTo == f.Target && c.DefinitionId == "training-item").Id;
        Accept(e, new GameCommand("chooseEffect", w.Me, e.Version, Choice: $"destroy-{item}"));
        Eq(e.State.Cards[item].Zone, Zone.FateDiscard, "the attachment is destroyed");
        Check(!e.State.Cards[f.Target!].Bowed, "and he is not bowed");
    }, settle: false);
}
