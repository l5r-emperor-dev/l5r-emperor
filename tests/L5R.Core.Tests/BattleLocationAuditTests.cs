using L5R.Core;

// Rule of Presence and Rule of Location (Comprehensive Rules, Sequence of play): a Battle action printed on a Personality or an attachment in play can only be taken when that card's unit is at the
// current battlefield. The exceptions are the card saying so (Absent, "Even if ... is not at the current battlefield"), the action moving the unit to the battlefield, and a Home ability used while the
// unit is home. Kitsu Kouki is a Shugenja, so under Halls of Memory ("Your Shugenja may use their base Battle abilities even if they are not at the current battlefield") he may act from home.
internal static class BattleLocationAuditTests
{
    public static void Run(TestRunner runner) => runner.Run("battle-location-audit", [
        ("Kitsu Kouki's Battle action needs him at the battlefield, and is refused from home or from a battlefield that is not the current one", KoukiNeedsToBeThere),
        ("Halls of Memory lets a Shugenja (Kouki) act from home, but not a non-Shugenja, and the target must still be at the battlefield", HallsLetsShugenjaAct),
        ("Ring of Fire cannot use a Personality at home as its performer; it needs the performer at the battlefield", RingOfFirePerformer),
        ("Every Battle ability with a performing card is refused when that card is home, unless it moves him to the battlefield or prints an exception", EveryPerformerNeedsLocation),
        ("Every Battle ability printed on a catalogue Personality or attachment that is offered at the battlefield is refused from home, unless it prints an exception", EveryBattleAbilityNeedsLocation),
    ]);

    private const string Kouki = "oracle-l5r-10063", Halls = "oracle-l5r-2949";

    private static MatchEngine Rig(string actionCard, Zone where, int? province, string? stronghold = null, bool actionCardIsAttachment = false, bool enemyTarget = true)
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        if (stronghold is not null) s.Cards[s.Players[0].StrongholdId].DefinitionId = stronghold;
        Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // our presence at the current battlefield
        if (enemyTarget) Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        if (actionCardIsAttachment)
        {
            var host = Batch336Support.Add(s, "training-water-shugenja", 0, where, province: province);
            Batch336Support.Add(s, actionCard, 0, Zone.Attached, province: province, parent: host.Id);
        }
        else Batch336Support.Add(s, actionCard, 0, where, province: province);
        return MatchEngine.FromTestState(s);
    }

    private static string KoukiId(MatchEngine e) => e.State.Cards.Values.First(c => c.DefinitionId == Kouki).Id;

    private static void KoukiNeedsToBeThere()
    {
        var at = Rig(Kouki, Zone.Battlefield, 0);
        Batch336Support.Check(Batch336Support.CanUse(at, 0, "kouki-force", KoukiId(at)), "control: Kouki at the current battlefield with an enemy there can act");

        var home = Rig(Kouki, Zone.Home, null);
        var kid = KoukiId(home);
        Batch336Support.Check(!Batch336Support.CanUse(home, 0, "kouki-force", kid), "Kouki at home is not offered the action");
        var enemy = home.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id;
        var refused = Batch336Support.Rejection(home, Batch336Support.Use(home, 0, "kouki-force", kid, targetId: enemy));
        Batch336Support.Check(refused.Contains("battlefield", StringComparison.OrdinalIgnoreCase), "a forced command is refused for the location: " + refused);
        Batch336Support.Check(!home.State.Cards[kid].Bowed, "and he did not bow");

        // At a battlefield that is not the current one (he is assigned elsewhere)
        var s = Batch336Support.Table(Phase.Battle, 0);
        s.UnresolvedBattlefields = [0, 1];
        Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var k = Batch336Support.Add(s, Kouki, 0, Zone.Battlefield, province: 1);
        var other = MatchEngine.FromTestState(s);
        Batch336Support.Check(!Batch336Support.CanUse(other, 0, "kouki-force", k.Id), "Kouki assigned to another battlefield is not offered the action here");
    }

    private static void HallsLetsShugenjaAct()
    {
        var halls = Rig(Kouki, Zone.Home, null, stronghold: Halls);
        Batch336Support.Check(Batch336Support.CanUse(halls, 0, "kouki-force", KoukiId(halls)), "Halls of Memory: a Shugenja's base Battle ability may be used from home");
        // The target must still be at the current battlefield (Halls ignores only the second part of the Rule of Location), and we must still have a unit there (Presence).
        var noEnemy = Rig(Kouki, Zone.Home, null, stronghold: Halls, enemyTarget: false);
        Batch336Support.Check(!Batch336Support.CanUse(noEnemy, 0, "kouki-force", KoukiId(noEnemy)), "with no target at the battlefield there is nothing to act on");
        var s = Batch336Support.Table(Phase.Battle, 0);
        s.Cards[s.Players[0].StrongholdId].DefinitionId = Halls;
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var k = Batch336Support.Add(s, Kouki, 0, Zone.Home);
        Batch336Support.Check(!Batch336Support.CanUse(MatchEngine.FromTestState(s), 0, "kouki-force", k.Id), "Halls does not waive the Rule of Presence: no unit of ours at the battlefield");
        // The same stronghold does not help a Personality who is not a Shugenja.
        var t = Batch336Support.Table(Phase.Battle, 0);
        t.Cards[t.Players[0].StrongholdId].DefinitionId = Halls;
        Batch336Support.Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Batch336Support.Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var samurai = Batch336Support.Add(t, "training-harmless-battle-action", 0, Zone.Home); // a non-Shugenja with kouki-force
        Batch336Support.Check(!Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, "kouki-force", samurai.Id), "a non-Shugenja at home is refused even under Halls of Memory");
    }

    private static bool PrintsLocationException(AbilityDefinition a) =>
        a.Absent || a.MovesToBattle || a.SourceUnitMayBeAway || a.SourceUnitMayBeAwayIfWielder is not null || a.Home;

    private static void EveryBattleAbilityNeedsLocation()
    {
        var failures = new List<string>();
        int checkedCards = 0, offeredAtBattle = 0, exceptions = 0;
        var cards = CardCatalog.Cards.Values
            .Where(c => c.Type is CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell && c.Abilities is { Length: > 0 })
            .Where(c => c.Abilities is not null).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        foreach (var def in cards)
        {
            var battleAbilities = def.Abilities!.Where(id => AbilityCatalog.All.TryGetValue(id, out var a) && a.Timing is "Battle" or "BattleOrOpen" && !a.PlayerAbility).ToArray();
            if (battleAbilities.Length == 0) continue;
            checkedCards++;
            var attachment = def.Type != CardType.Personality;
            try
            {
                var at = Rich(def.Id, Zone.Battlefield, 0, attachment);
                var home = Rich(def.Id, Zone.Home, null, attachment);
                var atId = LastId(at, def.Id);
                var homeId = LastId(home, def.Id);
                foreach (var id in battleAbilities)
                {
                    var ability = AbilityCatalog.All[id];
                    if (!Batch336Support.CanUse(at, 0, id, atId)) continue; // not usable even where the card is: nothing to compare
                    offeredAtBattle++;
                    if (PrintsLocationException(ability)) { exceptions++; continue; }
                    if (Batch336Support.CanUse(home, 0, id, homeId)) failures.Add($"{def.Name} ({def.Id}) {id}: offered from home as well as at the battlefield, and prints no location exception");
                }
            }
            catch (Exception ex) { failures.Add($"{def.Name} ({def.Id}) threw {ex.GetType().Name}: {ex.Message}"); }
        }
        Batch336Support.Check(offeredAtBattle >= 40, $"the sweep must actually reach abilities: only {offeredAtBattle} offered at the battlefield across {checkedCards} cards");
        Batch336Support.Check(failures.Count == 0, $"{failures.Count} Battle abilities usable away from the battlefield ({offeredAtBattle} compared, {exceptions} with a printed exception, {checkedCards} cards):\n  " + string.Join("\n  ", failures));
    }

    private const string RingOfFire = "oracle-l5r-6335";

    private static void RingOfFirePerformer()
    {
        foreach (var (where, province, expected) in new[] { (Zone.Battlefield, (int?)0, true), (Zone.Home, (int?)null, false) })
        {
            var s = Batch336Support.Table(Phase.Battle, 0);
            var present = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // our presence, and a weaker Chi than the foe so it is not the performer being tested
            var performer = Batch336Support.Add(s, "training-crane-veteran", 0, where, province: province);
            var foe = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var ring = Batch336Support.Add(s, RingOfFire, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var offered = Batch336Support.CanUse(e, 0, "ring-of-fire-destroy", ring.Id, performer.Id);
            Batch336Support.Check(offered == expected, $"Ring of Fire with the performer at {where}: offered={offered}, expected {expected}");
            if (!expected)
            {
                var refused = Batch336Support.Rejection(e, Batch336Support.Use(e, 0, "ring-of-fire-destroy", ring.Id, targetId: foe.Id, performerId: performer.Id));
                Batch336Support.Check(refused.Length > 0 && e.State.Cards[performer.Id].Zone == Zone.Home && e.State.Cards[ring.Id].Zone == Zone.Hand, "a refused use costs nothing");
            }
        }
    }

    private static bool MovesPerformer(AbilityDefinition a) => a.Move is { } m && m.Movers is MoveSpec.Performer or MoveSpec.PerformerAndTarget or MoveSpec.PerformingSlot;

    private static void EveryPerformerNeedsLocation()
    {
        var keywords = CardCatalog.Cards.Values.Where(c => c.Type == CardType.Personality).SelectMany(c => c.Keywords ?? []).Where(k => k != "Unique").Distinct().ToArray();
        var failures = new List<string>();
        int compared = 0;
        var done = new HashSet<string>();
        foreach (var def in CardCatalog.Cards.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (def.Abilities is null || def.Type is CardType.Region or CardType.Stronghold or CardType.Personality or CardType.Follower or CardType.Item or CardType.Spell) continue;
            foreach (var id in def.Abilities)
            {
                if (!AbilityCatalog.All.TryGetValue(id, out var a) || a.PlayerAbility || a.Performer is null || a.Timing is not ("Battle" or "BattleOrOpen") || !done.Add(id)) continue;
                try
                {
                    var (atHome, atBattle) = (Build(def, id, keywords, Zone.Home, null), Build(def, id, keywords, Zone.Battlefield, 0));
                    if (!Batch336Support.CanUse(atBattle.E, 0, id, atBattle.Source, atBattle.Performer)) continue; // not usable even with the performer there: nothing to compare
                    compared++;
                    if (a.MovesToBattle || MovesPerformer(a) || a.SourceUnitMayBeAway || a.Home || def.Text.Contains("at any location", StringComparison.OrdinalIgnoreCase)) continue; // a printed exception: "performing Courtier at any location"
                    if (Batch336Support.CanUse(atHome.E, 0, id, atHome.Source, atHome.Performer)) failures.Add($"{def.Name} ({def.Id}) {id} [{a.Performer}]: offered with the performer at home");
                }
                catch (Exception ex) { failures.Add($"{def.Name} ({def.Id}) {id} threw {ex.GetType().Name}: {ex.Message}"); }
            }
        }
        Batch336Support.Check(compared >= 40, $"only {compared} performer abilities were reachable");
        Batch336Support.Check(failures.Count == 0, $"{failures.Count} performer Battle abilities usable with the performer at home ({compared} compared):\n  " + string.Join("\n  ", failures));
    }

    private static (MatchEngine E, string Source, string Performer) Build(CardDefinition def, string abilityId, string[] keywords, Zone performerAt, int? province)
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var foe = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Batch336Support.Add(s, "training-item", 1, Zone.Attached, province: 0, parent: foe.Id);
        for (var i = 0; i < 6; i++) Batch336Support.Add(s, "training-marketplace", 0, Zone.Home);
        var performer = Batch336Support.Add(s, "training-crane-veteran", 0, performerAt, province: province);
        foreach (var k in keywords) performer.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent));
        var inHand = def.Type is CardType.Strategy or CardType.Ring or CardType.Event;
        var source = Batch336Support.Add(s, def.Id, 0, inHand ? Zone.Hand : def.Type == CardType.Holding ? Zone.Home : Zone.Celestial);
        return (MatchEngine.FromTestState(s), source.Id, performer.Id);
    }

    private static string LastId(MatchEngine e, string def) => e.State.Cards.Values.Where(c => c.DefinitionId == def && c.OwnerId == 0).Select(c => c.Id).OrderByDescending(i => int.Parse(i[3..])).First();

    // A table with a full set of targets at the battlefield (an enemy Personality with a Follower and an Item), our own presence there, the card either at the battlefield or at home, and gold to pay with.
    private static MatchEngine Rich(string def, Zone where, int? province, bool attachment)
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var foe = Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Batch336Support.Add(s, "training-item", 1, Zone.Attached, province: 0, parent: foe.Id);
        Batch336Support.Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        for (var i = 0; i < 6; i++) Batch336Support.Add(s, "training-marketplace", 0, Zone.Home);
        if (attachment)
        {
            var host = Batch336Support.Add(s, "training-water-shugenja", 0, where, province: province);
            Batch336Support.Add(s, def, 0, Zone.Attached, province: province, parent: host.Id);
        }
        else Batch336Support.Add(s, def, 0, where, province: province);
        return MatchEngine.FromTestState(s);
    }
}
