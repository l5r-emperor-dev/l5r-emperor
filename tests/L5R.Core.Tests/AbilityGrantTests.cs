using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

internal static class AbilityGrantTests
{
    private const string Spirit = "oracle-l5r-7424", Give = "spirit-of-the-monk", Bow = "spirit-monk-bow";
    public static void Run(TestRunner runner) => runner.Run("ability-grants", [
        ("The real Strategy grants a keyword first, then a permanent ability to only its performer", PermanentGrant),
        ("Granted Battle action is offered, validates target Armor and location, and spends only the receiver's use", BattleUse),
        ("A new permanent grant survives a temporary grant and never resets a used ability", Regrant),
        ("Both players receive readable ability source, timing, duration and authoritative remaining uses; hidden cards stay hidden", PublicViews),
        ("Permanent grants survive death and a later turn; temporary grants are not active on a dead card", DeathAndTurn),
        ("Battle-duration grants expire before the next battlefield, permanent grants do not", BattleDuration),
        ("An attachment's removal revokes only its own attachment-duration abilities", AttachmentDuration),
        ("A permanently granted Reaction opens its window, resolves and uses the receiver's stamp", Reaction),
        ("Shared usage keys, twice-per-turn and unlimited grants retain catalogue limits in the public view", UsageLimits)
    ]);
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static CardInstance Add(MatchState s, string def, int owner = 0, Zone zone = Zone.Home, bool bowed = false, string? parent = null)
    {
        var c = new CardInstance { Id = $"grant-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand,
            Bowed = bowed, ProvinceIndex = zone == Zone.Battlefield ? 0 : null, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }
    private static MatchState Table()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = s.PriorityPlayerId = 0;
        return s;
    }
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit)
    { var s = e.State; edit(s); return MatchEngine.FromTestState(s); }
    private static GameCommand Offered(MatchEngine e, string card, string ability, string? target = null)
    {
        var a = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.CardId == card && a.Command?.AbilityId == ability
            && (target == null || a.TargetIds?.Contains(target) == true));
        Check(a?.Command != null, $"Not offered: {ability}; " + string.Join("; ", e.QueryActions(e.State.PriorityPlayerId).Where(a => a.CardId == card).Select(a => $"{a.Label}: {a.Reason}")));
        return target == null ? a!.Command! : a!.Command! with { TargetId = target };
    }
    private static CardView View(MatchEngine e, string id, int viewer = 0) => e.GetView(viewer).Players
        .SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.Hand)).Single(c => c.Id == id);
    private static MatchEngine Grant(bool keyword = true)
    {
        var s = Table();
        var receiver = Add(s, "training-crane-samurai");
        Add(s, "training-crane-samurai");
        if (keyword) receiver.GrantedKeywords.Add(new("Monk", "fixture", GrantDuration.Permanent));
        var strategy = Add(s, Spirit, zone: Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var offer = e.QueryActions(0).Single(a => a.Enabled && a.Command?.AbilityId == Give && a.Command.PerformerId == receiver.Id);
        Accept(e, offer.Command!);
        return e;
    }
    private static string Receiver(MatchEngine e) => e.State.Cards.Values.First(c => c.Id.StartsWith("grant-")).Id;
    private static void PermanentGrant()
    {
        var def = CardCatalog.Get(Spirit);
        Check(!def.ScenarioOnly && def.Focus == 4 && def.GoldCost == 0 && def.Abilities!.SequenceEqual([Give]));
        var e = Grant(false); var id = Receiver(e);
        Check(e.HasKeyword(id, "Monk") && e.State.Cards[id].GrantedAbilities.Count == 0);
        string strategy = "";
        e = Edit(e, s => { s.PriorityPlayerId = 0; strategy = Add(s, Spirit, zone: Zone.Hand).Id; });
        var cmd = e.QueryActions(0).Single(a => a.Enabled && a.Command?.CardId == strategy && a.Command.PerformerId == id).Command!;
        Accept(e, cmd);
        var grant = e.State.Cards[id].GrantedAbilities.Single();
        Check(grant.AbilityId == Bow && grant.Duration == GrantDuration.Permanent && grant.Source == strategy);
        Check(e.State.Cards[strategy].Zone == Zone.FateDiscard, "the Strategy is spent");
        Check(e.State.Cards.Values.Where(c => c.DefinitionId == "training-crane-samurai" && c.Id != id).All(c => c.GrantedAbilities.Count == 0));
        Check(!CardCatalog.Get("training-crane-samurai").Abilities!.Contains(Bow), "shared definition was not changed");
    }
    private static void BattleUse()
    {
        var e = Grant(); var id = Receiver(e); string target = "", armored = "", other = "";
        e = Edit(e, s => {
            s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
            s.Cards[id].Zone = Zone.Battlefield; s.Cards[id].ProvinceIndex = 0;
            other = s.Cards.Values.Last(c => c.DefinitionId == "training-crane-samurai").Id;
            s.Cards[other].Zone = Zone.Battlefield; s.Cards[other].ProvinceIndex = 0;
            target = Add(s, "training-lion-samurai", 1, Zone.Battlefield).Id;
            armored = Add(s, "training-lion-samurai", 1, Zone.Battlefield).Id;
            Add(s, "training-armor", 1, Zone.Attached, bowed: true, parent: armored);
        });
        var cmd = Offered(e, id, Bow, target);
        Check(e.QueryActions(0).Single(a => a.Enabled && a.CardId == id && a.Command?.AbilityId == Bow).Label.Contains("granted from Spirit of the Monk"));
        Reject(e, cmd with { CardId = other }); Reject(e, cmd with { PlayerId = 1 }); Reject(e, cmd with { TargetId = armored });
        var away = Edit(e, s => s.Cards[target].ProvinceIndex = 1); Reject(away, cmd);
        var bowed = Edit(e, s => s.Cards[id].Bowed = true); Reject(bowed, cmd);
        Accept(e, cmd); Check(e.State.Cards[target].Bowed);
        Check(View(e, id).GrantedAbilities!.Single().UsesRemaining == 0);
        e = Edit(e, s => { s.PriorityPlayerId = 0; s.Cards[target].Bowed = false; });
        Reject(e, cmd with { ExpectedVersion = e.Version });
        e = Edit(e, s => s.TurnNumber++);
        Accept(e, Offered(e, id, Bow, target));
    }
    private static void Regrant()
    {
        var e = Grant(); var id = Receiver(e); string strategy = "";
        e = Edit(e, s => {
            s.PriorityPlayerId = 0; s.Cards[id].GrantedAbilities.Clear();
            s.Cards[id].GrantedAbilities.Add(new(Bow, "temporary-source", GrantDuration.Turn));
            s.Cards[id].AbilityUses[Bow] = s.TurnNumber;
            strategy = Add(s, Spirit, zone: Zone.Hand).Id;
        });
        Accept(e, e.QueryActions(0).Single(a => a.Enabled && a.Command?.CardId == strategy && a.Command.PerformerId == id).Command!);
        Check(e.State.Cards[id].GrantedAbilities.Count == 2 && View(e, id).GrantedAbilities!.All(a => a.UsesRemaining == 0));
        e = Edit(e, s => { s.Phase = Phase.Dynasty; s.PriorityPlayerId = 0; });
        Accept(e, new("endDynasty", 0, e.Version));
        Check(e.State.Cards[id].GrantedAbilities.Single().Duration == GrantDuration.Permanent);
    }
    private static void PublicViews()
    {
        var e = Grant(); var id = Receiver(e);
        foreach (var viewer in new[] { 0, 1 })
        {
            var grant = View(e, id, viewer).GrantedAbilities!.Single();
            Check(grant.SourceName == "Spirit of the Monk" && grant.Timing == "Battle" && grant.Label.Contains("without Armor"));
            Check(grant.UsesPerTurn == 1 && grant.UsesRemaining == 1 && grant.Duration == "Permanent");
            var roundtrip = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(e.GetView(viewer)))!;
            Check(roundtrip.Players[0].Home.Single(c => c.Id == id).GrantedAbilities!.Single() == grant);
        }
        e = Edit(e, s => {
            var hidden = s.Cards[s.Players[0].Provinces[0].CardId!]; hidden.FaceUp = false;
            hidden.GrantedAbilities.Add(new(Bow, "secret-grant-source", GrantDuration.Permanent));
            var hand = Add(s, Spirit, zone: Zone.Hand); hand.GrantedAbilities.Add(new(Bow, "private-hand-grant", GrantDuration.Permanent));
        });
        var json = JsonSerializer.Serialize(e.GetView(1));
        Check(!json.Contains("secret-grant-source") && !json.Contains("private-hand-grant"), "grants must not reveal hidden cards");
        var state = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!;
        Check(View(MatchEngine.FromTestState(state), id).GrantedAbilities!.Single().AbilityId == Bow);
    }
    private static void DeathAndTurn()
    {
        var e = Grant(); var id = Receiver(e);
        e = Edit(e, s => { s.PriorityPlayerId = 0; s.Cards[id].DefinitionId = "training-skin-donor"; s.Cards[id].Dishonorable = true;
            s.Cards[id].GrantedAbilities.Add(new("training-bow-any", "temporary", GrantDuration.Turn)); });
        Accept(e, e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == id).Command!);
        Check(e.State.Cards[id].Zone == Zone.Dead && View(e, id, 1).GrantedAbilities!.Single().AbilityId == Bow);
        e = Edit(e, s => { s.Phase = Phase.Dynasty; s.PriorityPlayerId = 0; s.PendingChoice = null; });
        Accept(e, new("endDynasty", 0, e.Version));
        Check(e.State.Cards[id].GrantedAbilities.Single().AbilityId == Bow);
    }
    private static void BattleDuration()
    {
        var s = Table(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0, 1];
        var card = Add(s, "training-crane-samurai");
        card.GrantedAbilities = [new(Bow, "permanent", "Permanent"), new("training-bow-any", "battle", "Battle")];
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
        Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Phase == Phase.BattleSelection);
        Check(e.State.Cards[card.Id].GrantedAbilities.Single().AbilityId == Bow);
    }
    private static void AttachmentDuration()
    {
        var s = Table(); var receiver = Add(s, "training-skin-donor"); receiver.Dishonorable = true;
        var attachment = Add(s, "training-weapon", zone: Zone.Attached, parent: receiver.Id);
        receiver.GrantedAbilities = [new(Bow, attachment.Id, "Attachment"), new("training-bow-any", attachment.Id, "Permanent"), new("training-create-token", "another-source", "Attachment")];
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == receiver.Id).Command!);
        Check(!e.State.Cards[receiver.Id].GrantedAbilities.Any(g => g.AbilityId == Bow), "the removed attachment's ability ended");
        Check(e.State.Cards[receiver.Id].GrantedAbilities.Count == 2, "other durations and sources retained");
    }
    private static void Reaction()
    {
        var s = Table(); var receiver = Add(s, "training-crane-samurai", 0, Zone.Battlefield); // #1133: a Reaction in a battle needs its unit at the battlefield
        receiver.GrantedAbilities.Add(new("tamoko-negate", "permanent-reaction", "Permanent"));
        s.Phase = Phase.Battle; s.ActivePlayerId = s.PriorityPlayerId = 1; s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0];
        var straightener = Add(s, "training-battle-straightener", 1, Zone.Battlefield);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offered(e, straightener.Id, "training-straighten-at-battle", target.Id));
        Check(e.State.PendingChoice?.Type == "beforeActionReaction" && e.State.PriorityPlayerId == 0);
        Accept(e, Offered(e, receiver.Id, "tamoko-negate"));
        Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
        Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Cards[target.Id].Bowed && View(e, receiver.Id).GrantedAbilities!.Single().UsesRemaining == 0);
    }
    private static void UsageLimits()
    {
        var s = Table(); var card = Add(s, "training-crane-samurai");
        card.GrantedAbilities = [new("horibe-bow", "a", "Permanent"), new("horibe-straighten", "b", "Permanent"), new("mochitoko-negate", "c", "Permanent"), new("ring-of-air-straighten", "d", "Permanent")];
        card.AbilityUses["horibe-bow-straighten"] = s.TurnNumber;
        var e = MatchEngine.FromTestState(s);
        var grants = View(e, card.Id).GrantedAbilities!;
        Check(grants.Take(2).All(g => g.UsesRemaining == 0), "shared usage key");
        Check(grants[2].UsesPerTurn == 2 && grants[2].UsesRemaining == 2);
        Check(grants[3].UsesPerTurn == 0 && grants[3].UsesRemaining == null);
        Accept(e, Offered(e, card.Id, "ring-of-air-straighten", card.Id));
        e = Edit(e, state => state.PriorityPlayerId = 0);
        Accept(e, Offered(e, card.Id, "ring-of-air-straighten", card.Id));
    }
}
