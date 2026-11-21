using L5R.Core;
using static RegressionAssertions;

// Issue #830, slice 1: hand-built fixtures that satisfy each ability's own card-specific preconditions (the generic kitchen-sink table of
// MultiWindowAuditTests cannot), then prove the action is offered AND accepted in every window its printed timing allows, is refused in every
// other window, and does what the card says. A scenario is a (phase, who is active, who acts) triple: the attacker/defender of a Battle phase
// (player 1 attacks) and the active/non-active player of an Action phase (player 0 is active).
internal static partial class MultiWindowAudit1Tests
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
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null);

    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.Target ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
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

    // Runs one ability through all four windows. `build` lays out the table for the acting player; `effect` sees the state before and the engine after
    // an accepted use. Allowed windows must offer and accept it; every other window must not offer it enabled and must refuse a direct command.
    private static void Windows(string ability, Func<MatchState, W, Fx> build, Action<MatchState, MatchEngine, Fx, W>? effect = null, string[]? only = null, Func<string, string>? why = null)
    {
        var timing = AbilityCatalog.All[ability].Timing;
        var allowed = only ?? MultiWindowAuditTests.Expected(timing);
        foreach (var scenario in Scenarios)
        {
            var battle = scenario.StartsWith("battle");
            var me = scenario is "battle-defender" or "action-active" ? 0 : 1;
            var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0);
            s.PriorityPlayerId = me;
            var w = new W(me, 1 - me, battle, scenario);
            var fx = build(s, w);
            var e = MatchEngine.FromTestState(s);
            var before = e.State;
            var command = Offered(e, fx);
            if (allowed.Contains(scenario))
            {
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
                var result = e.Execute(command!);
                Check(result.Accepted, $"{ability} offered but refused in {scenario}: {result.Error}");
                Settle(e);
                effect?.Invoke(before, e, fx, w);
            }
            else
            {
                Check(command is null, $"{ability} [{timing}] must not be offered in {scenario}");
                var direct = Use(e, fx.Me, ability, fx.Card, fx.Target, fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds };
                var error = Rejection(e, direct);
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
            }
        }
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit-1", [
        ("A Quiet Transaction: the Gold-paying lock on a dishonorable Personality is offered and accepted in either player's Action phase and nowhere else", QuietTransactionLock),
        ("A Quiet Transaction: the two -2 Gold Cost tokens on a dishonorable Personality are offered and accepted in either player's Action phase and nowhere else", QuietTransactionTokens),
        ("Asako Usohara: the Limited action puts a Fudo Ring from hand into play and removes her, for the active player's Action phase only", UsoharaRing),
        ("Brotherhood Schism: the Limited action shuffles an enemy discard-pile Event into its owner's deck, for the active player's Action phase only", BrotherhoodSchismShuffle),
        ("Hidden Weapons: the Battle action equips a Weapon from under the Holding, in either player's Battle phase only", HiddenWeaponsBattle),
        ("Hidden Weapons: the Limited action equips a Weapon from under the Holding, in the active player's Action phase only", HiddenWeaponsLimited),
        ("Daigotsu Kazuko: destroying her Follower makes a Melee 6 Attack that kills a Force-6 Personality, in either player's Battle phase only", KazukoDestroy),
        ("False Ring of Fire: at a maximum hand size of 10 the Open action puts it into play for 2 Honor, in either player's Action phase only", FalseRingOfFireRecruit),
        ("Fudo's Assurances: the Battle/Open action straightens a Ring in both the Battle phase and either player's Action phase", FudoStraighten),
        ("Fudo's Assurances: with five Fudo Rings the Open action destroys a Province, in either player's Action phase only", FudoDestroy),
        ("Kamalakar: his Battle action moves him to a battlefield with enemy units, as either side of a battle and never in the Action phase", KamalakarMove),
        ("Ninube Hajime: the Ninja Battle action moves an enemy Personality to another unresolved battlefield, as either side and never in the Action phase", HajimeMove),
        ("Tamori Tamoko: the Open action works only on another player's turn (non-active player in the Action phase), never on her own turn or in a battle", TamokoOpen),
        ("Sound the Retreat: with the Kata in play the Battle action moves the performer home and straightens his unit, as either side of a battle only", SoundTheRetreatMove),
        ("Void Tattoo: the discarding Battle entry returns a Tattoo to hand and grants another Battle action, as either side of a battle only", VoidTattooReturn),
        ("Void Tattoo: the no-discard Battle entry for a Void Personality returns a Tattoo to hand, as either side of a battle only", VoidTattooReturnVoid),
        ("The Tao of Fudo: the Battle/Open action straightens a Ring in play and destroys the Spell, in the Battle phase and both players' Action phases", TaoStraighten),
        ("The Tao of Fudo: the Battle/Open action puts a Ring from the discard pile into hand and destroys the Spell, in the Battle phase and both players' Action phases", TaoHand),
        ("Togashi's Guidance: a Dragon player's Battle/Open action puts a Ring from hand into play, in the Battle phase and both players' Action phases", TogashiRingPlay),
        ("Togashi's Guidance: a Dragon player's Battle/Open action searches the discard pile for a Ring, in the Battle phase and both players' Action phases", TogashiRingSearch),
        ("Ring of Water: with the battle's recruit grant, the Ring enters play in a Battle phase for either side and never in the Action phase", RingOfWaterRecruit),
        ("Might of the Sun: a non-Fire Shugenja bows to destroy an enemy card without attachments, as either side of a battle only", MightOfTheSunBow),
        ("Tsubute: discarded from hand with a Ninja at the battle, its Ranged 3 Attack kills a 3F enemy, as either side of a battle only", TsubuteAttackHand),
        ("The Isha's Yumi: a Mantis player discards it from hand for a Ranged 6 Attack that kills a Force-6 Personality, as either side of a battle only", YumiAttackHand),
        ("The Dark Naga: the Open action equips a Nonhuman Follower from the discard pile for 4 less Gold, in either player's Action phase only", DarkNagaEquip),
        ("Recruitment Station: the Battle/Open action equips a Follower for 2 less Gold, in the Battle phase and both players' Action phases", RecruitmentStationEquip),
        ("Rugashi Bazaar: after losing Honor to another player's action, Revenge gives performers Force equal to their base Personal Honor, in the Battle phase and both Action phases", RugashiRevenge),
        ("Shadow Dragon: the Open action brings him back from the dead pile paying all costs, in either player's Action phase only", ShadowDragonReturn),
        ("Kuon's Legacy: the Battle/Open action straightens a 7-Force performer, gives +3F, locks his actions and bows a card at his location, in the Battle phase and both Action phases", KuonsLegacyRage),
        ("Ring of Water: the fourth Battle action in a battle from a different title grants the recruit, for the attacker and for the defender", RingOfWaterAfterARealBattle),
    ]);

    private const string QuietTransaction = "oracle-l5r-70", Usohara = "oracle-l5r-10493", FalseRingOfFire = "oracle-l5r-10533", Schism = "oracle-l5r-10138";

    private static void QuietTransactionLock() => Windows("quiet-transaction-lock", (s, w) =>
    {
        var target = Put(s, "training-crane-veteran", w.Foe, w); target.Dishonorable = true;
        Put(s, "training-lion-samurai", w.Me, w);
        var card = Add(s, QuietTransaction, w.Me, Zone.Hand);
        return new(w.Me, "quiet-transaction-lock", card.Id, target.Id);
    }, (before, e, f, w) =>
        Check(e.State.Cards[f.Target!].MovementLocks is [{ Kinds: var kinds }] && kinds == (MovementKinds.ToBattlefield | MovementKinds.Assign), "the target may not be assigned or move to battlefields"));

    private static void QuietTransactionTokens() => Windows("quiet-transaction-tokens", (s, w) =>
    {
        var target = Put(s, "training-battle-maiden", w.Foe, w); target.Dishonorable = true;
        var card = Add(s, QuietTransaction, w.Me, Zone.Hand);
        return new(w.Me, "quiet-transaction-tokens", card.Id, target.Id);
    }, (before, e, f, w) => Eq(e.State.Cards[f.Target!].Tokens.Count(t => t.Stat == "GoldCost" && t.Amount == -2), 2, "two -2 Gold Cost tokens"));

    private static void UsoharaRing() => Windows("w739-usohara-ring", (s, w) =>
    {
        var usohara = Put(s, Usohara, w.Me, w);
        var ring = Add(s, FalseRingOfFire, w.Me, Zone.Hand);
        return new(w.Me, "w739-usohara-ring", usohara.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Ring is in play " + e.State.PendingChoice?.Type + " " + string.Join("|", e.State.Events.TakeLast(4).Select(x => x.Message)));
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "Usohara is removed from the game");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 2, "a False Ring played by its own text loses 2 Honor");
    });

    private static void BrotherhoodSchismShuffle() => Windows("brotherhood-schism-shuffle", (s, w) =>
    {
        var evt = Add(s, "oracle-l5r-8494", w.Foe, Zone.DynastyDiscard);
        var card = Add(s, Schism, w.Me, Zone.Hand);
        return new(w.Me, "brotherhood-schism-shuffle", card.Id, evt.Id);
    }, (before, e, f, w) => Check(e.State.Cards[f.Target!].Zone == Zone.DynastyDeck && e.State.Players[w.Foe].DynastyDeck.Contains(f.Target!), "shuffled into its owner's Dynasty deck"));

    private const string HiddenWeapons = "oracle-l5r-10574", Kazuko = "oracle-l5r-1734", FudoAssurances = "oracle-l5r-10416", RingOfAir = "oracle-l5r-6333",
        FalseAir = "oracle-l5r-10531", FalseEarth = "oracle-l5r-10532", FalseVoid = "oracle-l5r-10534", FalseWater = "oracle-l5r-10535";

    private static void HiddenEquip(string ability) => Windows(ability, (s, w) =>
    {
        var holding = Add(s, HiddenWeapons, w.Me, Zone.Home);
        var wielder = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        var weapon = Add(s, "training-weapon", w.Me, Zone.Underneath); weapon.HostCardId = holding.Id; weapon.FaceUp = false;
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        return new(w.Me, ability, holding.Id, null, null, [Pick("attachment", weapon.Id), Pick("personality", wielder.Id)]);
    }, (before, e, f, w) =>
    {
        var weapon = e.State.Cards.Values.Single(c => c.DefinitionId == "training-weapon");
        Eq(weapon.Zone, Zone.Attached, "the Weapon is attached");
        Eq(weapon.AttachedTo, f.Sel![1].CardIds![0], "to the chosen Personality");
    });
    private static void HiddenWeaponsBattle() => HiddenEquip("w739-hidden-battle");
    private static void HiddenWeaponsLimited() => HiddenEquip("w739-hidden-limited");

    private static void KazukoDestroy() => Windows("kazuko-fear-melee-destroy", (s, w) =>
    {
        var kazuko = Put(s, Kazuko, w.Me, w);
        var follower = Add(s, "training-follower", w.Me, Zone.Attached, province: w.Province, parent: kazuko.Id);
        var veteran = Put(s, "training-lion-veteran", w.Foe, w); // Force 6
        return new(w.Me, "kazuko-fear-melee-destroy", kazuko.Id, veteran.Id, CardIds: [follower.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.CardIds![0]].Zone, Zone.FateDiscard, "the Follower was destroyed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Melee 6 destroyed the Force-6 Personality");
    });

    private static void FalseRingOfFireRecruit() => Windows("wl-false-ring-of-fire-recruit", (s, w) =>
    {
        var ring = Add(s, "oracle-l5r-10533", w.Me, Zone.Hand);
        s.Players[w.Me].MaxHandBonus = 2;
        return new(w.Me, "wl-false-ring-of-fire-recruit", ring.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.Home, "the Ring is in play");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 2, "lose 2 Honor");
    });

    private static void FudoStraighten() => Windows("wl-fudo-assurances-straighten", (s, w) =>
    {
        var fudo = Add(s, FudoAssurances, w.Me, Zone.Celestial);
        var ring = Add(s, RingOfAir, w.Foe, Zone.Home, bowed: true);
        Put(s, "training-crane-samurai", w.Me, w); // a Battle/Open action in a battle needs a unit at the battlefield
        return new(w.Me, "wl-fudo-assurances-straighten", fudo.Id, ring.Id);
    }, (before, e, f, w) => Check(!e.State.Cards[f.Target!].Bowed, "the Ring is straightened"));

    private static void FudoDestroy() => Windows("wl-fudo-assurances-destroy", (s, w) =>
    {
        var fudo = Add(s, FudoAssurances, w.Me, Zone.Celestial);
        foreach (var def in new[] { FalseAir, FalseEarth, "oracle-l5r-10533", FalseVoid, FalseWater }) Add(s, def, w.Me, Zone.Home);
        return new(w.Me, "wl-fudo-assurances-destroy", fudo.Id, Province: 2, TargetPlayer: w.Foe);
    }, (before, e, f, w) => Check(e.State.Players[w.Foe].Provinces[2].Destroyed, "the named Province is destroyed"));

    private const string Kamalakar = "oracle-l5r-4253", Hajime = "oracle-l5r-10090", Tamoko = "oracle-l5r-10056", SoundTheRetreat = "oracle-l5r-10169", VoidTattoo = "oracle-l5r-9943",
        TsunamiTattoo = "oracle-l5r-8793", TaoOfFudo = "oracle-l5r-10709", TogashisGuidance = "oracle-l5r-8603", MightOfTheSun = "oracle-l5r-5046", Tsubute = "oracle-l5r-8772",
        IshasYumi = "oracle-l5r-9979", DarkNaga = "oracle-l5r-10501", StalkingTiger = "oracle-l5r-7450", SpittingLlama = "oracle-l5r-10686", RecruitmentStation = "oracle-l5r-10430",
        RugashiBazaar = "oracle-l5r-6396", ShadowDragon = "oracle-l5r-6653", KuonsLegacy = "oracle-l5r-9913", RingOfWater = "oracle-l5r-6339";

    private static void Kw(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void Marketplaces(MatchState s, int player, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", player, Zone.Home); }

    private static void KamalakarMove() => Windows("kamalakar-move-battlefield", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1, 2];
        var k = Put(s, Kamalakar, w.Me, w);
        Add(s, "training-lion-samurai", w.Foe, Zone.Battlefield, province: 1);
        return new(w.Me, "kamalakar-move-battlefield", k.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.Battlefield, "he is still at a battlefield");
        Eq(e.State.Cards[f.Card].ProvinceIndex, 1, "now at the one with enemy units");
    });

    private static void HajimeMove() => Windows("hajime-move-battlefield", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        var h = Put(s, Hajime, w.Me, w);
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "hajime-move-battlefield", h.Id, enemy.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the enemy is at a battlefield");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 1, "moved to the other unresolved battlefield");
    });

    // Open, but only "if it is not your turn": the one Open action that is refused to the active player.
    private static void TamokoOpen() => Windows("tamoko-open", (s, w) =>
    {
        var tamoko = Put(s, Tamoko, w.Me, w);
        var activeOwner = w.Battle ? 1 : 0;
        var target = Add(s, "training-lion-samurai", activeOwner, w.Zone, province: w.Province);
        return new(w.Me, "tamoko-open", tamoko.Id, target.Id);
    }, (before, e, f, w) => Check(e.State.PendingTamokoHonorChecks.Any(c => c.TargetId == f.Target && c.BeneficiaryPlayerId == f.Me), "the honor check on the target is pending"),
        only: ["action-other"], why: scenario => scenario == "action-active" ? "not your own turn" : "timing is unavailable now");

    private static void SoundTheRetreatMove() => Windows("sound-the-retreat-move", (s, w) =>
    {
        var kata = Add(s, SoundTheRetreat, w.Me, Zone.Home);
        var performer = Put(s, "training-crane-samurai", w.Me, w, bowed: true);
        var follower = Add(s, "training-follower", w.Me, Zone.Attached, bowed: true, parent: performer.Id); follower.ProvinceIndex = w.Province;
        Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "sound-the-retreat-move", kata.Id, null, performer.Id);
    }, (before, e, f, w) =>
    {
        var follower = e.State.Cards.Values.Single(c => c.AttachedTo == f.Performer);
        Check(e.State.Cards[f.Performer!].Zone == Zone.Home && !e.State.Cards[f.Performer!].Bowed && !follower.Bowed, "moved home and the unit straightened");
    });

    private static void VoidTattooFixture(MatchState s, W w, bool isVoid, out CardInstance card, out CardInstance tattooed, out CardInstance tattoo, out CardInstance spare)
    {
        card = Add(s, VoidTattoo, w.Me, Zone.Hand);
        tattooed = Put(s, "training-tattooed", w.Me, w);
        if (isVoid) Kw(tattooed, "Void");
        Put(s, "training-lion-samurai", w.Foe, w);
        tattoo = Add(s, TsunamiTattoo, w.Me, Zone.FateDiscard);
        spare = Add(s, "training-focus-strategy", w.Me, Zone.Hand);
    }

    private static void VoidTattooReturn() => Windows("void-tattoo-return", (s, w) =>
    {
        VoidTattooFixture(s, w, false, out var card, out var tattooed, out var tattoo, out var spare);
        return new(w.Me, "void-tattoo-return", card.Id, null, tattooed.Id, [Pick("target", tattoo.Id), Pick("discard", spare.Id)]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Players[f.Me].Hand.Contains(f.Sel![0].CardIds![0]), "the Tattoo is in the hand");
        Eq(e.State.Cards[f.Sel![1].CardIds![0]].Zone, Zone.FateDiscard, "a card was discarded (not Void)");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "Void Tattoo was removed from the game");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == f.Me), "an additional Battle action was granted");
    });

    private static void VoidTattooReturnVoid() => Windows("void-tattoo-return-void", (s, w) =>
    {
        VoidTattooFixture(s, w, true, out var card, out var tattooed, out var tattoo, out var spare);
        return new(w.Me, "void-tattoo-return-void", card.Id, tattoo.Id, tattooed.Id);
    }, (before, e, f, w) =>
    {
        var hand = e.State.Players[f.Me].Hand;
        Check(hand.Contains(f.Target!) && hand.Count(h => CardCatalog.Get(e.State.Cards[h].DefinitionId).Name == "Training focus strategy") == 1, "the Tattoo returned and no card was discarded");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "Void Tattoo was removed from the game");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == f.Me), "an additional Battle action was granted");
    });

    private static void TaoOfFudoAbility(string ability, bool fromDiscard) => Windows(ability, (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Add(s, TaoOfFudo, w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        var ring = fromDiscard ? Add(s, RingOfAir, w.Me, Zone.FateDiscard) : Add(s, RingOfAir, w.Me, Zone.Home, bowed: true);
        return new(w.Me, ability, spell.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Zone != Zone.Attached, "the Spell is destroyed");
        if (ability == "wl-tao-of-fudo-straighten") Check(!e.State.Cards[f.Target!].Bowed && e.State.Cards[f.Target!].Zone == Zone.Home, "the Ring is straightened");
        else Check(e.State.Players[f.Me].Hand.Contains(f.Target!), "the Ring is in the hand");
    });
    private static void TaoStraighten() => TaoOfFudoAbility("wl-tao-of-fudo-straighten", false);
    private static void TaoHand() => TaoOfFudoAbility("wl-tao-of-fudo-hand", true);

    private static void TogashiRingPlay() => Windows("togashis-guidance-ring-play", (s, w) =>
    {
        s.Players[w.Me].Clan = "Dragon";
        var togashi = Add(s, TogashisGuidance, w.Me, Zone.Celestial);
        var ring = Add(s, RingOfAir, w.Me, Zone.Hand);
        Put(s, "training-crane-samurai", w.Me, w); // a Battle/Open action in a battle needs a unit at the battlefield
        return new(w.Me, "togashis-guidance-ring-play", togashi.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Ring is in play");
        Check(!e.State.Players[f.Me].Hand.Contains(f.Target!), "and out of the hand");
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
    });

    private static void TogashiRingSearch() => Windows("togashis-guidance-ring-search", (s, w) =>
    {
        s.Players[w.Me].Clan = "Dragon";
        var togashi = Add(s, TogashisGuidance, w.Me, Zone.Celestial);
        var pile = Add(s, RingOfWater, w.Me, Zone.FateDiscard);
        Put(s, "training-crane-samurai", w.Me, w);
        return new(w.Me, "togashis-guidance-ring-search", togashi.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingSearch!.Deck, SearchDeck.FateDiscard, "the discard pile is searched first");
        Accept(e, e.QueryActions(f.Me).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e.State.Cards.Values.Single(c => c.DefinitionId == RingOfWater).Zone, Zone.Hand, "the Ring from the pile is in the hand");
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
    });

    private static void RingOfWaterRecruit() => Windows("ring-of-water-recruit", (s, w) =>
    {
        var ring = Add(s, RingOfWater, w.Me, Zone.Hand);
        // The grant an eligible battle hands out (a Terrain played, a Terrain destroyed, something destroyed in the resolution).
        s.AdditionalActions.Add(new AdditionalActionGrant(w.Me, [ring.Id], Battle: s.BattleNumber));
        return new(w.Me, "ring-of-water-recruit", ring.Id);
    }, (before, e, f, w) => Eq(e.State.Cards[f.Card].Zone, Zone.Home, "the Ring is in play"));

    private static void MightOfTheSunBow() => Windows("pe-might-of-the-sun-bow", (s, w) =>
    {
        var shugenja = Put(s, "training-shugenja", w.Me, w);
        var spell = Add(s, MightOfTheSun, w.Me, Zone.Attached, province: w.Province, parent: shugenja.Id);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-might-of-the-sun-bow", spell.Id, foe.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the target is destroyed");
        Check(e.State.Cards.Values.Single(c => c.DefinitionId == "training-shugenja" && c.OwnerId == f.Me).Bowed, "the Shugenja bowed as the cost");
    });

    private static void TsubuteAttackHand() => Windows("p5-tsubute-attack-hand", (s, w) =>
    {
        var ninja = Put(s, "training-ninja-source", w.Me, w);
        var tsubute = Add(s, Tsubute, w.Me, Zone.Hand);
        var enemy = Put(s, "training-crane-samurai", w.Foe, w);
        return new(w.Me, "p5-tsubute-attack-hand", tsubute.Id, enemy.Id, ninja.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "Tsubute was discarded from the hand");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Ranged 3 Attack destroyed the 3F enemy");
    });

    private static void YumiAttackHand() => Windows("pf-yumi-attack-hand", (s, w) =>
    {
        s.Players[w.Me].Clan = "Mantis";
        Put(s, "training-crane-samurai", w.Me, w);
        var yumi = Add(s, IshasYumi, w.Me, Zone.Hand);
        var big = Put(s, "training-lion-veteran", w.Foe, w); // Force 6
        return new(w.Me, "pf-yumi-attack-hand", yumi.Id, big.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "The Isha's Yumi was discarded from the hand");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Ranged 6 destroyed the Force-6 Personality");
    });

    private static void DarkNagaEquip() => Windows("wl-pc-darknaga-equip", (s, w) =>
    {
        var naga = Put(s, DarkNaga, w.Me, w);
        Add(s, StalkingTiger, w.Me, Zone.Hand);
        var llama = Add(s, SpittingLlama, w.Me, Zone.FateDiscard);
        Marketplaces(s, w.Me, 4);
        return new(w.Me, "wl-pc-darknaga-equip", naga.Id, llama.Id);
    }, (before, e, f, w) => Eq(e.State.Cards[f.Target!].AttachedTo, f.Card, "equipped to The Dark Naga from the discard pile"));

    private static void RecruitmentStationEquip() => Windows("wl-recruitment-station-equip", (s, w) =>
    {
        var station = Add(s, RecruitmentStation, w.Me, Zone.Home);
        var host = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Add(s, "training-follower", w.Me, Zone.Hand);
        follower.Printed = CardCatalog.Get("training-follower") with { GoldCost = 4 };
        Marketplaces(s, w.Me, 2);
        return new(w.Me, "wl-recruitment-station-equip", station.Id, follower.Id, host.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].AttachedTo, f.Performer, "the Follower was equipped to the performing Personality");
        Check(e.State.Cards[f.Card].Bowed, "bowing is the cost");
    });

    private static void RugashiRevenge() => Windows("rugashi-revenge", (s, w) =>
    {
        s.HonorLostToOtherActionSinceTurnEnded[w.Me] = true;
        var bazaar = Add(s, RugashiBazaar, w.Me, Zone.Home);
        var a = Put(s, "training-crane-samurai", w.Me, w); var b = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "rugashi-revenge", bazaar.Id, null, null, [Pick("performers", a.Id, b.Id)]);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        foreach (var id in f.Sel![0].CardIds!) Eq(e.CardForceForTest(id), old.CardForceForTest(id) + 2, "a performer has +2F (his base Personal Honor)");
    });

    private static void ShadowDragonReturn() => Windows("shadow-dragon-exp2-return", (s, w) =>
    {
        s.Players[w.Me].Honor = 20;
        Marketplaces(s, w.Me, 11);
        var dragon = Add(s, ShadowDragon, w.Me, Zone.Dead);
        return new(w.Me, "shadow-dragon-exp2-return", dragon.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Zone == Zone.Home && e.State.Cards[f.Card].OwnerId == f.Me, "he is in play at home");
        Eq(e.State.Players[f.Me].Honor, 15, "his entry costs 5 Honor");
    });

    private static void KuonsLegacyRage() => Windows("kuons-legacy-rage", (s, w) =>
    {
        var kuon = Add(s, KuonsLegacy, w.Me, Zone.Hand);
        var berserker = Put(s, "training-high-force-personality", w.Me, w, bowed: true);
        var bowTarget = w.Battle ? Put(s, "training-lion-samurai", w.Foe, w) : Put(s, "training-crane-samurai", w.Me, w);
        return new(w.Me, "kuons-legacy-rage", kuon.Id, null, berserker.Id, [Pick("bow", bowTarget.Id)]);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        Check(!e.State.Cards[f.Performer!].Bowed, "he straightened");
        Eq(e.CardForceForTest(f.Performer!), old.CardForceForTest(f.Performer!) + 3, "+3F");
        Check(e.State.Cards[f.Performer!].ActionLocks.Count > 0, "he may not perform actions");
        Check(e.State.Cards[f.Sel![0].CardIds![0]].Bowed, "the card at his location was bowed");
    });

    // The real route to Ring of Water's grant: the fourth Battle action in a battle from a different title (three are already counted), taken by the attacker and by the defender; the recruit is then offered.
    private static void RingOfWaterAfterARealBattle()
    {
        foreach (var me in new[] { 1, 0 }) // the attacker, then the defender
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
            var ring = Add(s, RingOfWater, me, Zone.Hand);
            var veteran = Add(s, "training-lion-veteran", me, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 1 - me, Zone.Battlefield, province: 0);
            var strategy = Add(s, "training-performer-strategy", me, Zone.Hand);
            s.Players[me].BattleActionsStamp = s.BattleNumber.ToString(); s.Players[me].BattleActionTitles = ["Title A", "Title B", "Title C"];
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, me, "ring-of-water-recruit", ring.Id).All(o => !o.Enabled), "no grant before the fourth Battle action");
            Accept(e, new GameCommand("useAbility", me, e.Version, CardId: strategy.Id, PerformerId: veteran.Id, AbilityId: "training-performer-boost"));
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); // the opponent passes back
            var command = Offered(e, new(me, "ring-of-water-recruit", ring.Id));
            Check(command is not null, $"player {me}: the fourth Battle action from a new title offers the Ring of Water");
            Accept(e, command!);
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "the Ring is in play");
        }
    }
}
