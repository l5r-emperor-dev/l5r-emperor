using static RegressionAssertions;
using L5R.Core;

// Intervening Spirit (oracle-l5r-3788, Second City): "Battle: Even if this unit is not at the current battlefield, if any enemy units are there, target your
// Personality: Move him there. Straighten him if he moved. If he is an Ancestor and this Shugenja is Water, you may take an additional Battle action."
// A user report said it did not reliably work as a Home Battle action. Every case reaches the action through QueryActions, as the client does: the acting
// player has NO unit at the current battlefield, the Shugenja holding the Spell is at home, and a free Personality waits at home.
internal static class InterveningSpiritTests
{
    private const string Spirit = "oracle-l5r-3788", Ability = "intervening-spirit-move";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Intervening Spirit: with no unit at the battlefield, the Attacker and the Defender are each offered it from home and it moves and straightens their Personality into the battle", MovesFromHomeForAttackerAndDefender),
            ("Intervening Spirit: an Ancestor moved by a Water Shugenja grants an additional Battle action that keeps priority and is then spent on another Battle action", WaterAncestorBonusIsUsable),
            ("Intervening Spirit: no additional action unless both halves hold -- a non-Water Shugenja, or a Spirit Personality that is not an Ancestor", BonusNeedsWaterAndAncestor),
            ("Intervening Spirit: with no enemy unit at the battlefield it is not offered as playable (it used to be offered and then refused); if the enemy is gone when it resolves it does nothing instead of refusing (issue #967)", NeedsEnemyWhenAnnouncedFizzlesAtResolution),
        ];
        runner.Run("intervening-spirit", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) =>
        Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static CardDefinition PersonalityWith(params string[] keywords) =>
        CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && keywords.All(k => c.Keywords?.Contains(k) == true));
    private static CardDefinition PersonalityWithout(string[] with, string[] without) =>
        CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && with.All(k => c.Keywords?.Contains(k) == true) && !without.Any(k => c.Keywords?.Contains(k) == true));

    private sealed record Rig(MatchState S, CardInstance Host, CardInstance Card, CardInstance Mover, CardInstance Enemy);

    // A battle at province 0. The actor holds priority, has no unit there, and keeps a Shugenja (carrying the Spell) and the mover at home; the enemy has one unit there.
    private static Rig Table(int actor, bool actorAttacks, string hostDefinition, string moverDefinition, bool moverBowed = false)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Battle; s.ActivePlayerId = actorAttacks ? actor : 1 - actor; s.PriorityPlayerId = actor;
        s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var host = Add(s, hostDefinition, actor, Zone.Home);
        var card = Add(s, Spirit, actor, Zone.Attached, parent: host.Id);
        var mover = Add(s, moverDefinition, actor, Zone.Home, bowed: moverBowed);
        var enemy = Add(s, "training-lion-veteran", 1 - actor, Zone.Battlefield, province: 0);
        Eq(s.Cards.Values.Count(c => c.OwnerId == actor && c.Zone == Zone.Battlefield), 0, "precondition: the actor has no unit at the battlefield");
        return new(s, host, card, mover, enemy);
    }

    private static ActionOption Offer(MatchEngine e, int player, CardInstance card)
    {
        var offer = e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Command?.AbilityId == Ability && o.CardId == card.Id);
        Check(offer is { Enabled: true }, $"player {player} is offered Intervening Spirit: {offer?.Reason ?? "not listed"}");
        return offer!;
    }

    private static void MovesFromHomeForAttackerAndDefender()
    {
        var water = PersonalityWith("Shugenja", "Water").Id;
        foreach (var attacks in new[] { true, false })
            foreach (var bowed in new[] { false, true })
            {
                var r = Table(0, attacks, water, "training-crane-samurai", moverBowed: bowed);
                var e = MatchEngine.FromTestState(r.S);
                var offer = Offer(e, 0, r.Card);
                var role = attacks ? "Attacker" : "Defender";
                Check(offer.TargetIds!.Contains(r.Mover.Id), $"{role}: the Personality at home is a target");
                Check(offer.TargetIds!.Contains(r.Host.Id), $"{role}: so is the Shugenja himself");
                Check(!offer.TargetIds!.Contains(r.Enemy.Id), $"{role}: an enemy is not");
                Accept(e, offer.Command! with { TargetId = r.Mover.Id });
                Eq(e.State.Cards[r.Mover.Id].Zone, Zone.Battlefield, $"{role}: he moved"); Eq(e.State.Cards[r.Mover.Id].ProvinceIndex, (int?)0, $"{role}: to the current battlefield");
                Check(!e.State.Cards[r.Mover.Id].Bowed, $"{role} (bowed at home: {bowed}): he is straightened");
                Eq(e.State.Cards[r.Host.Id].Zone, Zone.Home, $"{role}: the Shugenja stays home");
                Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), $"{role}: not an Ancestor, no additional action");
                Eq(e.State.PriorityPlayerId, 1, $"{role}: priority passes on");
                // He now counts as the actor's presence: an ordinary Battle action needing a unit there is no longer refused for want of one.
                Check(e.QueryActions(0).All(o => o.Reason != "You need a unit at the current battlefield."), $"{role}: the moved Personality is presence at the battle");
            }
    }

    private static void WaterAncestorBonusIsUsable()
    {
        var water = PersonalityWith("Shugenja", "Water").Id;
        var ancestor = PersonalityWith("Ancestor").Id;
        foreach (var attacks in new[] { true, false })
        {
            var role = attacks ? "Attacker" : "Defender";
            var r = Table(0, attacks, water, ancestor);
            var second = Add(r.S, "training-crane-samurai", 0, Zone.Home);
            var again = Add(r.S, Spirit, 0, Zone.Attached, parent: r.Host.Id); // a second copy: the additional action is another Battle action
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, r.Card).Command! with { TargetId = r.Mover.Id });
            Eq(e.State.Cards[r.Mover.Id].Zone, Zone.Battlefield, $"{role}: the Ancestor moved");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 1, $"{role}: one additional Battle action");
            Eq(e.State.PriorityPlayerId, 0, $"{role}: the actor keeps priority to take it");
            Accept(e, Offer(e, 0, again).Command! with { TargetId = second.Id });
            Eq(e.State.Cards[second.Id].Zone, Zone.Battlefield, $"{role}: the additional action resolved");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 0, $"{role}: and spent the grant");
            Eq(e.State.PriorityPlayerId, 1, $"{role}: then priority passes on");
        }
    }

    private static void BonusNeedsWaterAndAncestor()
    {
        var water = PersonalityWith("Shugenja", "Water").Id;
        var dry = PersonalityWithout(["Shugenja"], ["Water"]).Id;
        var ancestor = PersonalityWith("Ancestor").Id;
        var spiritOnly = PersonalityWithout(["Spirit"], ["Ancestor"]).Id;
        foreach (var (host, mover, why) in new[] { (dry, ancestor, "an Ancestor moved by a Shugenja who is not Water"), (water, spiritOnly, "a Spirit who is not an Ancestor") })
        {
            var r = Table(0, true, host, mover);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, r.Card).Command! with { TargetId = r.Mover.Id });
            Eq(e.State.Cards[r.Mover.Id].Zone, Zone.Battlefield, $"{why}: he moved");
            Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), $"{why}: no additional action");
            Eq(e.State.PriorityPlayerId, 1, $"{why}: priority passes on");
        }
    }

    private static void NeedsEnemyWhenAnnouncedFizzlesAtResolution()
    {
        var water = PersonalityWith("Shugenja", "Water").Id;
        var r = Table(0, false, water, "training-crane-samurai");
        r.S.Cards.Remove(r.Enemy.Id); // nobody at the battlefield
        var e = MatchEngine.FromTestState(r.S);
        var offer = e.QueryActions(0).FirstOrDefault(o => o.Type == "useAbility" && o.Command?.AbilityId == Ability && o.CardId == r.Card.Id);
        Check(offer is null || !offer.Enabled, "with no enemy there it is not playable");
        if (offer is not null) Check((offer.Reason ?? "").Contains("enemy units"), "and says why: " + offer.Reason);
        Reject(e, new("useAbility", 0, e.Version, CardId: r.Card.Id, TargetId: r.Mover.Id, AbilityId: Ability));
        Eq(e.State.Cards[r.Mover.Id].Zone, Zone.Home, "nothing moved");
        // Announced while opposed, the opposition gone when it resolves (a reaction window closing later): resolution does nothing and does not throw.
        var resolve = typeof(MatchEngine).GetMethod("ExecuteAbilityEffects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        resolve.Invoke(e, [new GameCommand("useAbility", 0, e.Version, CardId: r.Card.Id, TargetId: r.Mover.Id, AbilityId: Ability), false]);
        Eq(e.State.Cards[r.Mover.Id].Zone, Zone.Home, "resolved with nobody opposing: he stays home");
        Check(e.State.Events.Any(ev => ev.Type == "abilityFizzled" && ev.CardId == r.Card.Id), "the fizzle is in the log");
    }
}
