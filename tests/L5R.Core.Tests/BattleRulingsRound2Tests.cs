using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// The rules lead's second round of Battle Phase rulings (issue #354; docs/decisions/2026-09-19-battle-rulings-round-2.md): the Declaration
// Segment, the one ordered set of before-resolution effects (traits, delayed effects, Reactions), Reactions are actions, the pinned Rebuilding
// Kyuden Hida reading, and the deferred Attack's source card. Every test drives the engine through the commands it offers.
internal static class BattleRulingsRound2Tests
{
    private const string Ebizo = "oracle-l5r-1629", Yuki = "oracle-l5r-1696", MasterScouts = "oracle-l5r-4867", Sakuma = "oracle-l5r-9624", Kakihara = "oracle-l5r-223";
    private const string Rebuilding = "oracle-l5r-6200", KyudenHidaStronghold = "oracle-l5r-4638";
    private const string Claim = "training-engage-claim-strategy", PoliticalClaim = "training-engage-political-claim-strategy";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Declaration: declining ends the Attack Phase at once -- no Maneuvers, no defender prompts, no battlefield -- and the decision is one explicit choice the engine offers", DeclineEndsAttackPhase),
            ("Declaration: declaring creates a battlefield at every province regardless of assignments; an unoccupied one is still fought once; defenders may defend any province", DeclareCreatesBattlefields),
            ("Declaration: only the Attacker declares, only in the Declaration Segment, and assignments need a declared attack", DeclarationGuards),
            ("Declaration: BattleStepView says Declaration/DeclareOrDecline, then Infantry Maneuvers, and reads None once the attack is declined", DeclarationStepView),
            ("Before resolution: ONE ordered set holds a trait, a delayed effect and a Reaction; the active player's order decides who wins; everything resolves before Force is read", OneSetThreeKinds),
            ("Before resolution: a delayed effect a Battle action registered bows / boosts / is skipped when its target is gone, and lapses with its battle", DelayedEffectPrimitive),
            ("Before resolution: a 'Before a battle resolves' Reaction is asked of its OWN controller only when usable, may be declined, and both players' Reactions join the one set", ReactionWindow),
            ("Before resolution: Akodo Dairuko's Reaction (Tireless): the battle's resolution does not bow the user's army; only that battle, only that player", DairukoNoBow),
            ("Reactions are actions: IsAction is the one classification (Limited, Open, Battle, Battle/Open, Reaction) and traits and Focus Effects are not", IsActionClassification),
            ("Reactions are actions: a Recon Reaction resolving in the Engage window gives Ebizo and Master Scouts +1F, stacking with later Recon actions until the end of the turn", EngageReconReaction),
            ("Reactions are actions: a Political Engage claim that names a target is negated like any action; the claim is never recorded and the Engage window is restored", EngageClaimNegated),
            ("Reactions are actions: a Reaction whose bow triggers an 'after this action' window opens it over the window it was played in, and a spent watcher opens nothing", AfterActionWindowForReaction),
            ("Reactions are actions: a Reaction that waited behind a before-action window resolves inside the window it was played in, so what it queues is deferred to that window's close", DeferredReactionResolvesInItsWindow),
            ("Reactions are actions: the targeted-by-action reactions (Jinn-Kuen's shape) apply to a Reaction that names their Merchant, and negate it", TargetedActionReactionsApplyToReactions),
            ("Issue #825: a held action whose Gold source was bowed while its window was open is aborted when the window closes (nothing paid, nothing resolves); the window can always be closed", HeldPaymentBrokenAborts),
            ("Reactions are actions: a window set aside behind a Reaction's own window is plain serialized state -- a snapshot mid-way continues identically", SuspendedWindowIsState),
            ("Rebuilding Kyuden Hida (the Event, not the Stronghold): an Attacker holding the first opportunity through Naval Invasion, Yuki or Sneak Attack must pass; that pass counts; the Defender's first normal opportunity follows", KyudenHidaEventPinned),
            ("Deferred Attack: the pending Attack keeps the ability's SOURCE card, so an Item, Spell or Follower's Recon keyword makes the Attack a Recon action -- and the performer's keywords do not", DeferredAttackSource),
            ("Deferred duel: a duel-issuing Strategy's own Recon keyword makes it a Recon action when the duel resolves, and its performer's keywords do not", DeferredDuelSource),
        ];
        runner.Run("battle-rulings-2", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"r2-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }
    // The economy scenario with published Strongholds (player abilities such as Naval Invasion exist), the Battle Selection moment: player 0 attacks.
    private static MatchState Selection(params int[] battlefields)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { s.Cards[p.StrongholdId].DefinitionId = "oracle-l5r-1218"; foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null;
        s.UnresolvedBattlefields = (battlefields.Length == 0 ? [0] : battlefields).ToList();
        return s;
    }
    // A battle already in its Combat Segment (the Defender to act) with a unit each: both players pass to resolve it.
    private static MatchState Combat()
    {
        var s = Selection(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 1;
        return s;
    }
    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);
    private static GameCommand Use(MatchEngine e, int player, string ability, string? card = null, string? performer = null, string? target = null)
        => new("useAbility", player, e.Version, CardId: card, AbilityId: ability, PerformerId: performer, TargetId: target);
    private static void Pass(MatchEngine e, int? player = null) => Accept(e, Cmd(e, "pass", player));
    private static void Decline(MatchEngine e, int? player = null) => Accept(e, Cmd(e, "declineReaction", player));
    // Passes for whoever still holds a reaction until the window closes (issue #644: a player with none is never asked to pass, so how many passes it takes depends on who holds one).
    private static void DeclineAll(MatchEngine e) { for (var guard = 0; guard < 6 && e.State.PendingChoice is not null; guard++) Decline(e); }
    private static BattleStepView Step(MatchEngine e, int viewer = 0) => e.GetView(viewer).BattleStep!;
    private static ActionOption[] Offered(MatchEngine e, int player, string type) => e.QueryActions(player).Where(o => o.Type == type).ToArray();
    // Takes the offered command (never a hand-built one) for an action of this type, optionally narrowed.
    private static void Take(MatchEngine e, int player, string type, Func<ActionOption, bool>? pick = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(o => o.Type == type && o.Enabled && o.Command is not null && (pick?.Invoke(o) ?? true));
        Check(option is not null, $"{type} was not offered to player {player}: " + string.Join("; ", e.QueryActions(player).Select(o => $"{o.Type}:{o.Label}[{o.Enabled}]")));
        Accept(e, option!.Command!);
    }
    // Takes the offered action of this ability against one of the targets the engine lists for it.
    private static void Target(MatchEngine e, int player, string ability, string targetId)
    {
        var option = e.QueryActions(player).FirstOrDefault(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == ability && (o.TargetIds?.Contains(targetId) ?? false));
        Check(option is not null, $"{ability} against {targetId} was not offered to player {player}: " + string.Join("; ", e.QueryActions(player).Select(o => $"{o.Type}:{o.Label}[{o.Enabled}]")));
        Accept(e, option!.Command! with { TargetId = targetId });
    }
    private static int Modifier(MatchEngine e, CardInstance c, string stat = "Force") => e.State.Cards[c.Id].Modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount);
    private static bool Fought(MatchEngine e, string message) => e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains(message));
    // A fresh engine per offered command: every command the engine offers must be one it accepts.
    private static void AllOffersAccepted(MatchEngine e, int player)
    {
        foreach (var option in e.QueryActions(player).Where(o => o.Enabled && o.Command is not null && o.Type is not "concede"))
            Accept(MatchEngine.FromTestState(e.State), option.Command!);
    }

    // ---- 1. the Declaration Segment -----------------------------------------------------------------------------
    private static MatchEngine AttackPhase(Action<MatchState>? arrange = null)
    {
        var e = MatchEngine.CreateStarterMatch(); var s = e.State;
        s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        arrange?.Invoke(s);
        e = MatchEngine.FromTestState(s);
        Pass(e, 0); Pass(e, 1); // the Action Phase ends on two consecutive passes
        Eq(e.State.Phase, Phase.AttackDeclaration);
        return e;
    }

    private static void DeclineEndsAttackPhase()
    {
        var e = AttackPhase(s => Add(s, "training-crane-samurai", 0, Zone.Home));
        var declaration = Step(e); Eq(declaration.Segment, BattleSteps.Declaration); Eq(declaration.Step, BattleSteps.DeclareOrDecline); Eq(declaration.DecisionPlayerId, (int?)0);
        var offered = e.QueryActions(0).Where(o => o.Enabled).Select(o => o.Type).ToArray();
        Check(offered.Contains("declareAttack") && offered.Contains("pass"), "the decision is one explicit choice: declare, or pass to decline");
        Check(!offered.Contains("attack") && !offered.Contains("assignDefense"), "no assignment is offered before the attack is declared");
        Check(e.QueryActions(1).All(o => o.Type is "wait" or "concede"), "the Defender only waits");
        AllOffersAccepted(e, 0);
        Pass(e, 0);
        Eq(e.State.Phase, Phase.Dynasty, "Declining ends the Attack Phase at once: not four empty passes");
        Eq(e.State.PriorityPlayerId, 0); Check(e.State.UnresolvedBattlefields.Count == 0 && e.State.CurrentBattleProvince is null, "no battlefield exists");
        Check(e.State.Cards.Values.All(c => c.Zone != Zone.Battlefield), "nobody was assigned anywhere");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "attackDeclined") && !e.GetView(0).Events.Any(ev => ev.Type == "attackDeclared"), "declined, not declared");
        Check(!e.GetView(1).Events.Any(ev => ev.Type == "phaseChanged" && ev.Message.Contains("DefenseAssignment")), "the defender was never asked to assign");
        Eq(Step(e).Segment, BattleSteps.None);
        // A declined attack is not a battlefield-less battle: the same turn's next phase is the ordinary Dynasty Phase.
        Check(e.QueryActions(0).Any(o => o.Type == "endDynasty"));
    }

    private static void DeclareCreatesBattlefields()
    {
        var e = AttackPhase(s => { Add(s, "training-crane-samurai", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home); s.Players[1].Provinces[2].Destroyed = true; });
        Take(e, 0, "declareAttack");
        Eq(e.State.Phase, Phase.InfantryDeclaration);
        Eq(string.Join(",", e.State.UnresolvedBattlefields), "0,1,3", "a battlefield at every one of the Defender's provinces still standing, before anyone is assigned");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "attackDeclared"));
        var view = e.GetView(1); Eq(string.Join(",", view.UnresolvedBattlefields), "0,1,3");
        Eq(Step(e).Segment, BattleSteps.InfantryManeuvers); Eq(Step(e).Step, BattleSteps.AttackerAssigns);
        Check(Offered(e, 0, "pass").Length == 1, "an attacker is not compelled to assign any unit");
        // The attacker assigns nobody to attack at all (a declared attack with no attackers).
        Pass(e, 0);
        Eq(e.State.Phase, Phase.DefenseAssignment);
        Eq(string.Join(",", Offered(e, 1, "assignDefense").SelectMany(o => o.Provinces ?? []).Distinct().Order()), "0,1,3", "the Defender may defend ANY battlefield, attacked or not");
        var defend = e.QueryActions(1).First(o => o.Type == "assignDefense" && o.Enabled && o.Command!.Assignments!.Values.All(p => p == 3));
        Accept(e, defend.Command!);
        // Cavalry Maneuvers: nobody has Cavalry, so both segments are skipped (issue #627) and no pass is needed
        Eq(e.State.Phase, Phase.BattleSelection, "battlefields exist, so battles are fought");
        Eq(Offered(e, 0, "selectBattle").Length, 3);
        // The battlefield with only the Defender's unit: the Attacker has no presence, so nothing to do but pass; the Defender wins, nobody dies.
        Take(e, 0, "selectBattle", o => o.Command!.Province == 3);
        Check(e.State.Cards.Values.Any(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield && c.ProvinceIndex == 3));
        var fought = 0;
        while (e.State.Phase == Phase.Battle) Pass(e);
        Check(Fought(e, "Province 4: attacking Force 0, defending Force"), "the defender-only battle resolved");
        Eq(e.State.Phase, Phase.BattleSelection);
        // Battlefields with no units at all are still fought, once each (core rules: a battlefield with zero units on both sides still has its one battle).
        foreach (var province in new[] { 0, 1 })
        {
            Eq(e.State.UnresolvedBattlefields.Contains(province), true);
            Take(e, 0, "selectBattle", o => o.Command!.Province == province);
            while (e.State.Phase == Phase.Battle) { Pass(e); fought++; }
            Check(Fought(e, $"Province {province + 1}: attacking Force 0, defending Force 0"), $"province {province + 1} had its one battle");
        }
        Eq(fought >= 4, true, "each empty battle needed its two passes");
        Eq(e.State.Phase, Phase.Dynasty, "every battlefield fought exactly once: the Attack Phase is over");
        Check(e.State.UnresolvedBattlefields.Count == 0);
        Check(e.State.Players.All(p => p.Honor == MatchEngine.CreateStarterMatch().State.Players[p.Id].Honor), "battles with no outcome change nothing");
    }

    private static void DeclarationGuards()
    {
        var e = AttackPhase(s => Add(s, "training-crane-samurai", 0, Zone.Home));
        Reject(e, new GameCommand("attack", 0, e.Version, Assignments: new()));               // assigning before declaring
        Reject(e, new GameCommand("assignDefense", 1, e.Version, Assignments: new()));
        Reject(e, new GameCommand("declareAttack", 1, e.Version));                         // the Defender does not declare
        Accept(e, new GameCommand("declareAttack", 0, e.Version));
        Reject(e, new GameCommand("declareAttack", 0, e.Version));                         // once: not again in Infantry Maneuvers
        var e2 = MatchEngine.CreateStarterMatch();
        Reject(e2, new GameCommand("declareAttack", 0, e2.Version));                       // not outside the Declaration Segment (the Dynasty Phase here)
        var s = MatchEngine.CreateStarterMatch().State; s.Phase = Phase.Action; s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var e3 = MatchEngine.FromTestState(s);
        Reject(e3, new GameCommand("declareAttack", 0, e3.Version));                       // nor in the Action Phase
    }

    private static void DeclarationStepView()
    {
        var e = AttackPhase();
        foreach (var viewer in new[] { 0, 1 })
        {
            var v = Step(e, viewer);
            Eq(v.Segment, BattleSteps.Declaration); Eq(v.Step, BattleSteps.DeclareOrDecline); Eq(v.DecisionPlayerId, (int?)0); Eq(v.AttackerPlayerId, (int?)0);
        }
        Eq(Json(Step(e, 0)), Json(Step(e, 1)), "identical for both viewers");
        var known = typeof(BattleSteps).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();
        Check(known.Contains(Step(e).Segment) && known.Contains(Step(e).Step), "both names are published constants");
        Take(e, 0, "declareAttack");
        Eq(Step(e).Segment, BattleSteps.InfantryManeuvers); Eq(Step(e).Step, BattleSteps.AttackerAssigns);
        var round = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(e.GetView(0), new JsonSerializerOptions(JsonSerializerDefaults.Web)), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Eq(round.Phase, Phase.InfantryDeclaration, "the new phase crosses the wire as a name");
        var declined = AttackPhase(); Pass(declined, 0);
        Eq(Step(declined).Segment, BattleSteps.None); Eq(Step(declined).Step, BattleSteps.None);
    }

    // ---- 2. the one ordered set of before-resolution effects ----------------------------------------------------
    // Attacker: X (Force 2, Samurai) and a delayed-effect caster C (Force 1); Defender: Y (Force 5) with a send-home trait terrain (Force <= 3 go home);
    // player 0 also holds Steady Resolve in hand. Player 0 has C boost X by 2 (a delayed effect) before both pass.
    private sealed record SetRig(MatchEngine E, CardInstance X, CardInstance C, CardInstance Y, CardInstance Terrain, CardInstance Steady);
    private static SetRig BuildSet()
    {
        var s = Combat();
        var x = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); var c = Add(s, "training-delayed-caster", 0, Zone.Battlefield, province: 0);
        var y = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var terrain = Add(s, "training-before-resolution-send-home-terrain", 1, Zone.Battlefield, province: 0);
        var steady = Add(s, "training-steady-resolve", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Pass(e, 1);
        Target(e, 0, "training-delayed-boost", x.Id);
        return new(e, x, c, y, terrain, steady);
    }
    private static void OneSetThreeKinds()
    {
        var r = BuildSet(); var e = r.E;
        Eq(Modifier(e, r.X), 0, "a delayed effect is registered, not applied: nothing changes while the players may still act");
        Check(e.State.DelayedBattleEffects.Count == 1, "one registered");
        Pass(e, 1); Pass(e, 0);
        Eq(e.State.PendingChoice?.Type, "beforeBattleOrder"); Eq(e.State.PriorityPlayerId, 0, "the ACTIVE player orders the whole set");
        var step = Step(e);
        Eq(step.Segment, BattleSteps.Resolution); Eq(step.Step, BattleSteps.BeforeResolution); Eq(step.DecisionPlayerId, (int?)0);
        var kinds = step.BeforeResolutionEffects!.ToDictionary(x => x.Kind, x => x);
        Eq(kinds.Count, 3, "one trait, one delayed effect, one Reaction: ONE set");
        Eq(kinds[BattleSteps.EffectTrait].CardId, r.Terrain.Id); Eq(kinds[BattleSteps.EffectTrait].PlayerId, 1);
        Check(kinds[BattleSteps.EffectDelayed].Id.StartsWith("delayed:") && kinds[BattleSteps.EffectDelayed].CardId == r.C.Id && kinds[BattleSteps.EffectDelayed].PlayerId == 0);
        Eq(kinds[BattleSteps.EffectReaction].CardId, r.Steady.Id); Eq(kinds[BattleSteps.EffectReaction].PlayerId, 0);
        Eq(step.BeforeResolutionSourceIds.Length, 3);
        Eq(Offered(e, 0, "beforeBattleOrder").Length, 3, "the active player chooses among all of them"); Check(Offered(e, 1, "beforeBattleOrder").Length == 0, "and only the active player");
        AllOffersAccepted(e, 0);
        // Order 1: the delayed boost, the Reaction, then the trait: X is 6, the trait sends home only the caster, and X beats Y (5).
        {
            var e1 = MatchEngine.FromTestState(e.State);
            Take(e1, 0, "beforeBattleOrder", o => o.Command!.TargetId!.StartsWith("delayed:"));
            Eq(Modifier(e1, r.X), 2, "applied when its turn came");
            Take(e1, 0, "beforeBattleOrder", o => o.Command!.TargetId!.StartsWith("reaction:"));
            Eq(e1.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e1.State.PriorityPlayerId, 0, "its own controller decides");
            Take(e1, 0, "useAbility", o => o.Command!.PerformerId == r.X.Id);
            Eq(Modifier(e1, r.X), 4);
            Check(e1.State.PendingChoice is null && e1.State.PendingBeforeBattleResolve is null, "the trait was the last item: it resolved directly");
            Check(Fought(e1, "attacking Force 4, defending Force 5") == false, "...and the Force was NOT read before the effects");
            Check(Fought(e1, "attacking Force 6, defending Force 5"), "X carries both boosts (2+2+2), the caster went home: 6 against 5");
            Eq(e1.State.Cards[r.Y.Id].Zone, Zone.Dead, "the Attacker won");
            var log = e1.GetView(0).Events.Select(ev => ev.Type).ToList();
            Check(log.LastIndexOf("beforeBattleEffect") < log.IndexOf("battleResolved"), "every before-resolution effect precedes the Force comparison");
        }
        // Order 2: the trait first. X (2) and the caster (1) go home before either boost: the Reaction has no performer left, and the Defender wins.
        {
            var e2 = MatchEngine.FromTestState(e.State);
            Take(e2, 0, "beforeBattleOrder", o => o.Command!.TargetId == r.Terrain.Id);
            Eq(e2.State.PendingChoice?.Type, "beforeBattleOrder", "two items remain: the active player orders again");
            Take(e2, 0, "beforeBattleOrder", o => o.Command!.TargetId!.StartsWith("delayed:"));
            Check(e2.State.PendingChoice is null, "the Reaction is no longer usable (its performer went home): it is skipped, nobody is asked");
            Check(Fought(e2, "attacking Force 0, defending Force 5"), "the Attacker's army is gone");
            Check(e2.State.Cards[r.X.Id].Zone == Zone.Home && e2.State.Cards[r.Y.Id].Zone != Zone.Battlefield, "both units have left the battlefield");
            Eq(e2.State.Cards[r.X.Id].Zone, Zone.Home); Eq(e2.State.Cards[r.Y.Id].Zone, Zone.Home, "the winning Defender's unit returns home at the end of the Attack Phase, unharmed");
            Eq(e2.State.Cards[r.Y.Id].Bowed, false);
        }
    }

    private static void DelayedEffectPrimitive()
    {
        // "Before this battle resolves, bow him": Y (Force 5) would beat X (Force 4).
        var s = Combat();
        var x = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var c = Add(s, "training-delayed-caster", 0, Zone.Battlefield, province: 0);
        var y = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Pass(e, 1);
        Target(e, 0, "training-delayed-bow", y.Id);
        Check(!e.State.Cards[y.Id].Bowed, "not yet");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "delayedEffectRegistered" && ev.CardId == c.Id), "public");
        Pass(e, 1); Pass(e, 0);
        Check(e.State.PendingChoice is null && e.State.PendingBeforeBattleResolve is null, "a single item in the set: nothing to order, it just applies");
        Check(Fought(e, "attacking Force 5, defending Force 0"), "the bowed Defender contributes nothing when Force is totalled");
        Eq(e.State.Cards[y.Id].Zone, Zone.Dead, "the Defender's whole army lost, bowed or not"); Eq(e.State.DelayedBattleEffects.Count, 0, "spent");
        // The target leaves play before its turn: the effect is skipped, nothing throws, and the effect is gone with the battle.
        var s2 = Combat();
        Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s2, "training-delayed-caster", 0, Zone.Battlefield, province: 0);
        var y2 = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Pass(e2, 1);
        Target(e2, 0, "training-delayed-bow", y2.Id);
        var st = e2.State; st.Cards[y2.Id].Zone = Zone.Dead; st.Cards[y2.Id].ProvinceIndex = null;
        var e3 = MatchEngine.FromTestState(st);
        Pass(e3, 1); Pass(e3, 0);
        Check(e3.State.PendingChoice is null && e3.State.DelayedBattleEffects.Count == 0, "skipped, then cleared");
        // A delayed effect that missed the set (registered after it was gathered) never carries into the next battle: it is cleared with its battle.
        var s4 = Combat(); s4.UnresolvedBattlefields = [0, 1];
        Add(s4, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var y4 = Add(s4, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s4, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, province: 0); Add(s4, "training-before-resolution-send-home-terrain", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Pass(e4, 1); Pass(e4, 0);
        Eq(e4.State.PendingChoice?.Type, "beforeBattleOrder", "two traits: the active player orders them");
        var late = e4.State; late.DelayedBattleEffects.Add(new(99, "bow", 0, null, y4.Id));
        e4 = MatchEngine.FromTestState(late);
        Take(e4, 0, "beforeBattleOrder");
        Eq(e4.State.DelayedBattleEffects.Count, 0, "applied or not, a delayed effect lapses with its battle"); Check(!e4.State.Cards[y4.Id].Bowed, "it was never part of the set, so it never applied");
        Eq(e4.State.Phase, Phase.BattleSelection, "the next battlefield is chosen");
    }

    private static void ReactionWindow()
    {
        // Only usable Reactions ask: Steady Resolve needs a performing opposed SAMURAI at the battlefield. A non-Samurai unit is no performer: no window.
        var s0 = Combat();
        Add(s0, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s0, "training-crane-veteran", 1, Zone.Battlefield, province: 0); Add(s0, "training-steady-resolve", 0, Zone.Hand);
        var e0 = MatchEngine.FromTestState(s0);
        Pass(e0, 1); Pass(e0, 0);
        Check(e0.State.PendingChoice is null && Fought(e0, "attacking Force 3, defending Force 5"), "no legal performer: the Reaction is never asked about");
        // An unusable Reaction is no member of the set: with one trait and a Steady Resolve nobody could use, there is nothing to order and nothing to ask.
        var s1 = Combat();
        Add(s1, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s1, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        Add(s1, "training-before-resolution-boost-terrain", 0, Zone.Battlefield, province: 0); Add(s1, "training-steady-resolve", 0, Zone.Hand);
        var e1 = MatchEngine.FromTestState(s1);
        Pass(e1, 1); Pass(e1, 0);
        Check(e1.State.PendingChoice is null && Fought(e1, "attacking Force 5, defending Force 5"), "the trait applied directly: the unusable Reaction is not in the set");
        // The Defender's Reaction: its own controller (not the active player) is asked; the window offers the plays, then the pass.
        var s = Combat();
        var x = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); var y = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var steady = Add(s, "training-steady-resolve", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Pass(e, 1); Pass(e, 0);
        Eq(e.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e.State.PriorityPlayerId, 1, "the Reaction's own controller decides");
        Eq(Step(e).DecisionPlayerId, (int?)1); Eq(Step(e).Window, "beforeBattleReaction");
        Check(e.QueryActions(0).All(o => o.Type is "wait" or "concede"), "the active player waits");
        var mine = e.QueryActions(1);
        Check(mine.Any(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "steady-resolve-boost" && o.Command.PerformerId == y.Id), "the plays of the Reaction are offered");
        Check(mine.Any(o => o.Type == "declineReaction"), "and the pass");
        Check(!mine.Any(o => o.Type is "pass" or "playStrategy"), "no Battle action is offered in this window");
        AllOffersAccepted(e, 1);
        Reject(e, Use(e, 1, "steady-resolve-boost", steady.Id, performer: x.Id));       // the opposing side's Samurai is no performer of yours
        Reject(e, Use(e, 0, "steady-resolve-boost", steady.Id, performer: x.Id));       // not your opportunity
        Decline(e, 1);
        Check(e.State.PendingChoice is null && Fought(e, "attacking Force 4, defending Force 2"), "declined: nothing changed, and the card stays in hand");
        Eq(e.State.Cards[steady.Id].Zone, Zone.Hand);
        // Used: +2F before Force is read, and the Strategy is discarded like any other.
        var e2 = MatchEngine.FromTestState(s);
        Pass(e2, 1); Pass(e2, 0);
        Take(e2, 1, "useAbility", o => o.Command!.PerformerId == y.Id);
        Check(Fought(e2, "attacking Force 4, defending Force 4"), "the Reaction resolved before the totals"); Eq(e2.State.Cards[steady.Id].Zone, Zone.FateDiscard);
        // Both players' Reactions are in the one set: the active player orders them, each owner answers their own.
        var s3 = Combat();
        Add(s3, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s3, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var sr0 = Add(s3, "training-steady-resolve", 0, Zone.Hand); var sr1 = Add(s3, "training-steady-resolve", 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Pass(e3, 1); Pass(e3, 0);
        Eq(e3.State.PendingChoice?.Type, "beforeBattleOrder"); Eq(Step(e3).BeforeResolutionEffects!.Length, 2); Eq(e3.State.PriorityPlayerId, 0);
        Take(e3, 0, "beforeBattleOrder", o => o.Command!.TargetId == $"reaction:{sr1.Id}");
        Eq(e3.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e3.State.PriorityPlayerId, 1);
        Take(e3, 1, "useAbility");
        Eq(e3.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e3.State.PriorityPlayerId, 0, "then the other one, answered by ITS controller");
        Decline(e3, 0);
        Check(Fought(e3, "attacking Force 2, defending Force 4"), "one used, one declined");
        Eq(e3.State.Cards[sr0.Id].Zone, Zone.Hand);
    }

    private static void DairukoNoBow()
    {
        (MatchEngine E, CardInstance Commander, CardInstance Ally)? Build(Action<MatchState, CardInstance, CardInstance>? arrange = null, bool commanderBowed = false, Zone zone = Zone.Battlefield)
        {
            var s = Combat();
            var commander = Add(s, "training-rallying-commander", 0, zone, bowed: commanderBowed, province: zone == Zone.Battlefield ? 0 : null);
            var ally = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            s.UnresolvedBattlefields = [0, 1];
            arrange?.Invoke(s, commander, ally);
            return (MatchEngine.FromTestState(s), commander, ally);
        }
        // Without the Reaction the winning attackers return home bowed.
        var (plain, _, plainAlly) = Build()!.Value;
        Pass(plain, 1); Pass(plain, 0); Decline(plain, 0);
        Check(plain.State.Cards[plainAlly.Id].Zone == Zone.Home && plain.State.Cards[plainAlly.Id].Bowed, "the attackers bow after the resolution");
        // With it: nothing in the Attacker's army bows.
        var (e, commander, ally) = Build()!.Value;
        Pass(e, 1); Pass(e, 0);
        Eq(e.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e.State.PendingChoice?.CardId, commander.Id);
        Take(e, 0, "useAbility", o => o.Command!.AbilityId == "dairuko-rally");
        Check(e.State.Cards[ally.Id].Zone == Zone.Home && !e.State.Cards[ally.Id].Bowed && !e.State.Cards[commander.Id].Bowed, "the resolution did not bow the army");
        Eq(e.State.ResolutionWillNotBowPlayerIds.Count, 0, "and the promise lapsed with the battle");
        Eq(e.State.Phase, Phase.BattleSelection);
        // Only that battle: the next one bows as usual.
        var st = e.State; foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home)) { c.Zone = Zone.Battlefield; c.ProvinceIndex = 1; }
        var e5 = MatchEngine.FromTestState(st);
        Take(e5, 0, "selectBattle"); Pass(e5, 1); Pass(e5, 0);
        if (e5.State.PendingChoice is not null) Decline(e5, 0);
        Check(e5.State.Cards[ally.Id].Bowed, "a later battle bows again");
        // Tireless: usable while bowed. Not at the battlefield: not offered at all.
        var (bowedRig, bowedCommander, _) = Build(commanderBowed: true)!.Value;
        Pass(bowedRig, 1); Pass(bowedRig, 0);
        Eq(bowedRig.State.PendingChoice?.CardId, bowedCommander.Id, "a bowed Dairuko may still use it");
        var (home, _, _) = Build(zone: Zone.Home)!.Value;
        Pass(home, 1); Pass(home, 0);
        Check(home.State.PendingChoice is null, "at Home he is not at the battlefield: no window");
        // The promise covers the user's OWN army only: a Defender's use does not stop the Attacker bowing.
        var s6 = Combat(); s6.UnresolvedBattlefields = [0, 1];
        var a6 = Add(s6, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s6, "training-rallying-commander", 1, Zone.Battlefield, province: 0);
        var e6 = MatchEngine.FromTestState(s6);
        Pass(e6, 1); Pass(e6, 0);
        Take(e6, 1, "useAbility", o => o.Command!.AbilityId == "dairuko-rally");
        Check(e6.State.Cards[a6.Id].Bowed || e6.State.Cards[a6.Id].Zone == Zone.Dead, "the Attacker's army is not the Defender's");
    }

    // ---- 3. Reactions are actions ----------------------------------------------------------------------------------
    private static void IsActionClassification()
    {
        var isAction = typeof(MatchEngine).GetMethod("IsAction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, [typeof(AbilityDefinition)])!;
        bool Is(AbilityDefinition a) => (bool)isAction.Invoke(null, [a])!;
        foreach (var (id, ability) in AbilityCatalog.All)
        {
            var expected = ability.Timing is "Limited" or "Open" or "Battle" or "BattleOrOpen" or "Reaction";
            Eq(Is(ability), expected, $"{id} ({ability.Timing})");
        }
        Check(AbilityCatalog.All.Values.Any(a => a.Timing == "Reaction") && AbilityCatalog.All.Values.Where(a => a.Timing == "Reaction").All(Is), "every Reaction is an action");
        Check(AbilityCatalog.All.Values.Where(a => a.Timing is "BeforeResolution" or "FocusEffect").All(a => !Is(a)), "a trait and a Focus Effect are not");
    }

    private static void EngageReconReaction()
    {
        var s = Selection();
        var yuki = Add(s, Yuki, 0, Zone.Battlefield, province: 0); var ebizo = Add(s, Ebizo, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); var scouts = Add(s, MasterScouts, 0, Zone.Attached, parent: host.Id);
        var theirEbizo = Add(s, Ebizo, 1, Zone.Home); Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var actor = Add(s, "training-recon-actor", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        int Bonus(CardInstance c) => e.State.Cards[c.Id].Modifiers.Count(m => m.Effect == "force-after-own-recon-action");
        Take(e, 0, "selectBattle");
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        Take(e, 0, "useAbility", o => o.Command!.AbilityId == "yuki-engage-recon");
        Eq(Bonus(ebizo), 1, "a Recon Reaction resolving in the Engage window is a Recon action: +1F for Ebizo");
        Eq(Bonus(scouts), 1, "and for Master Scouts, on the Follower itself"); Eq(e.State.Cards[host.Id].Modifiers.Count, 0);
        Eq(Bonus(theirEbizo), 0, "never the opponent's");
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "(Yuki did his own job too)");
        Decline(e); Decline(e); // the window closes; Yuki's claim gives the Attacker the first opportunity
        // The bonus lasts until the end of the turn and stacks with the next Recon action.
        Take(e, 0, "useAbility", o => o.Command!.AbilityId == "training-recon-attack" && o.CardId == actor.Id);
        Eq(Bonus(ebizo), 2, "stacks: two Recon actions, +2F"); Eq(Bonus(scouts), 2);
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == ebizo.Id).Force, 6, "his live Force");
    }

    private static void EngageClaimNegated()
    {
        var s = Selection();
        var sakuma = Add(s, Sakuma, 1, Zone.Battlefield, province: 0); var ally = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, PoliticalClaim, 0, Zone.Hand);
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true; // he cannot pay Sakuma's 2 Gold: the negation is automatic
        var e = MatchEngine.FromTestState(s);
        Take(e, 0, "selectBattle");
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        Target(e, 0, "training-engage-claim-political", ally.Id);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "a Reaction that names a target is an action other players may react to, even in the Engage window");
        Eq(e.State.SuspendedWindows.Count, 1); Eq(e.State.SuspendedWindows[0].Choice.Type, "engageReaction", "the Engage window is set aside, not overwritten");
        Check(e.State.PendingEngage is not null && e.State.PendingEngage.Claims.Count == 0, "the claim has not been made: it is the deferred action's effect");
        Take(e, 1, "useAbility", o => o.Command!.AbilityId == "sakuma-political-negate");
        Check(e.State.DeferredActionNegated || e.State.PendingChoice?.Type == "beforeActionReaction");
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Decline(e);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open again"); Eq(e.State.SuspendedWindows.Count, 0);
        Eq(e.State.PendingEngage!.Claims.Count, 0, "negated: the claim never happened");
        Check(e.GetView(0).Events.Any(ev => ev.Type == "actionNegated"));
        Decline(e); Decline(e);
        Eq(e.State.PriorityPlayerId, 1, "the Defender keeps the first opportunity"); Eq(Step(e).FirstActionReason, BattleSteps.ReasonDefault);
        // Un-negated (Sakuma passes), the same claim works: the Attacker holds the first opportunity.
        var s2 = Selection();
        var ally2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0); Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, PoliticalClaim, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Take(e2, 0, "selectBattle");
        Target(e2, 0, "training-engage-claim-political", ally2.Id);
        Eq(e2.State.PendingEngage!.Claims.Count, 1, "nobody could react: it simply resolved inside the Engage window");
        Decline(e2); Decline(e2);
        Eq(e2.State.PriorityPlayerId, 0); Eq(Step(e2).FirstActionReason, BattleSteps.ReasonEngageGrant);
    }

    private static void AfterActionWindowForReaction()
    {
        // Ruya's Reaction (ruya-bow) bows a Personality at Hizumi's battlefield: "after the resolution of an action that bowed a Personality" is a
        // window Hizumi (the watcher of that battlefield) gets, though the action is a Reaction.
        var s = Combat(); s.ActivePlayerId = 0;
        var ruya = Add(s, "oracle-l5r-7789", 0, Zone.Battlefield, province: 0); var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var first = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); var second = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var hizumi = Add(s, "oracle-l5r-8521", 1, Zone.Battlefield, province: 0);
        Check(CardCatalog.Get(hizumi.DefinitionId).Name.Contains("Hizumi"), "fixture: Togashi Hizumi");
        hizumi.AbilityUses["hizumi-negate-straighten"] = s.TurnNumber; // spent: only Ruya's window opens for the first bow
        s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-bow-at-battle", bower.Id, target: first.Id));
        Eq(e.State.PendingChoice?.Type, "bowReaction"); Eq(e.State.PendingChoice?.CardId, ruya.Id, "a spent Hizumi opens nothing: Ruya's window, for her controller's own action");
        var st = e.State; st.Cards[hizumi.Id].AbilityUses.Clear(); // his use is available again by the time Ruya's Reaction bows
        e = MatchEngine.FromTestState(st);
        var window = e.State.PendingChoice;
        Accept(e, Use(e, 0, "ruya-bow", ruya.Id, target: second.Id));
        Eq(e.State.PendingChoice?.Type, "bowReaction"); Eq(e.State.PendingChoice?.CardId, hizumi.Id, "Ruya's Reaction bowed a Personality: Hizumi's window opens over hers");
        Eq(e.State.SuspendedWindows.Count, 1); Eq(e.State.SuspendedWindows[0].Choice, window, "hers is set aside, exactly as it was");
        Decline(e); // Hizumi's controller passes; Ruya's side holds nothing to react with here, so his window closes
        // Issue #644: Ruya's window comes back (nothing is left suspended) but she has spent her use and holds nothing else: it is closed rather than offered to be passed.
        Eq(e.State.SuspendedWindows.Count, 0);
        Check(e.State.Cards[second.Id].Bowed, "Ruya's Reaction resolved");
        Check(e.State.PendingChoice is null, "her own Reaction's bow did not reopen her window over itself, and it has nothing left to offer");
    }

    private static void DeferredReactionResolvesInItsWindow()
    {
        var s = Selection();
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-overconfident-personality", 1, Zone.Battlefield, province: 0); Add(s, "training-before-action-suppressor", 1, Zone.Home);
        Add(s, "training-engage-destroy-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Take(e, 0, "selectBattle");
        Eq(e.State.PendingChoice?.Type, "engageReaction");
        Target(e, 0, "training-engage-destroy", victim.Id);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the suppressor's controller may react to the targeting: the destroy waits behind that window");
        Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "not yet destroyed");
        Decline(e); Decline(e);
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "then it resolved");
        Eq(e.State.PendingChoice?.Type, "engageReaction", "inside the Engage window it was played in: the draw offer its destruction queued waits for that window, as after a Reaction played directly");
        Eq(e.State.PendingOverconfidentDraws.Count, 1); Eq(e.State.SuspendedWindows.Count, 0);
        Eq(e.State.PriorityPlayerId, 1, "the opportunity passed to the other player");
        Decline(e); Decline(e);
        Eq(e.State.PendingChoice?.Type, "overconfidentDraw", "and is offered once the Engage window closes");
    }

    private static void TargetedActionReactionsApplyToReactions()
    {
        var s = Selection();
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var guard = Add(s, "training-merchant-free-guard", 1, Zone.Battlefield, province: 0); Add(s, "training-engage-destroy-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Take(e, 0, "selectBattle");
        Target(e, 0, "training-engage-destroy", guard.Id);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "a Reaction that names his Merchant is an action that targets it: the guard may react");
        Take(e, 1, "useAbility", o => o.Command!.AbilityId == "training-negate-merchant-free");
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Decline(e);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open again");
        Eq(e.State.Cards[guard.Id].Zone, Zone.Battlefield, "the Reaction's destruction of him was negated");
        Check(e.State.Events.Any(ev => ev.Type.Contains("egated")), "and the negation is announced");
    }

    private static void HeldPaymentBrokenAborts()
    {
        // The deferred action of seat 0 names a Gold source; the other player's Reaction then bows it. Before #825 the resolution failed ("Every Gold source must be yours ... unbowed") and
        // declineReaction was refused for ever, a deadlock the bot arena found. The action is aborted instead and the window closes.
        var s = Selection();
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var guard = Add(s, "training-merchant-free-guard", 1, Zone.Battlefield, province: 0); Add(s, "training-engage-destroy-strategy", 0, Zone.Hand);
        var holding = s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).GoldProduction > 0);
        var e = MatchEngine.FromTestState(s);
        Take(e, 0, "selectBattle");
        Target(e, 0, "training-engage-destroy", guard.Id);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the guard may react, so the action is held");
        var held = e.State; held.PendingNegatableAbility = held.PendingNegatableAbility! with { SourceIds = [holding.Id] }; held.Cards[holding.Id].Bowed = true;
        e = MatchEngine.FromTestState(held);
        for (var step = 0; step < 6 && e.State.PendingChoice?.Type == "beforeActionReaction"; step++) Decline(e);
        Check(e.State.PendingChoice?.Type != "beforeActionReaction", "the window closed instead of deadlocking");
        Check(e.State.Events.Any(ev => ev.Type == "actionNegated" && ev.Message.Contains("Gold source")), "the aborted action is announced: " + string.Join(" || ", e.State.Events.TakeLast(8).Select(ev => ev.Type + ":" + ev.Message)));
        Eq(e.State.Cards[guard.Id].Zone, Zone.Battlefield, "nothing resolved");
    }

    private static void SuspendedWindowIsState()
    {
        var s = Selection();
        var sakuma = Add(s, Sakuma, 1, Zone.Battlefield, province: 0); var ally = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Add(s, PoliticalClaim, 0, Zone.Hand);
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        Take(e, 0, "selectBattle");
        Target(e, 0, "training-engage-claim-political", ally.Id);
        Eq(e.State.SuspendedWindows.Count, 1);
        var snapshot = JsonSerializer.Serialize(e.State);
        var restored = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(snapshot)!);
        Eq(Json(restored.State), snapshot, "round-trips");
        foreach (var engine in new[] { e, restored })
        {
            Take(engine, 1, "useAbility", o => o.Command!.AbilityId == "sakuma-political-negate");
            while (engine.State.PendingChoice?.Type == "beforeActionReaction") Decline(engine);
            Decline(engine); Decline(engine);
        }
        Eq(Json(e.State), Json(restored.State), "a snapshot taken behind a nested window continues identically");
        Check(e.State.SuspendedWindows.Count == 0);
    }

    // ---- 4. Rebuilding Kyuden Hida ---------------------------------------------------------------------------------
    private static void KyudenHidaEventPinned()
    {
        // The two cards are different: the Event's effect id belongs to the Event alone; the Stronghold (a Recon Reaction, wired in stronghold-wave-8) carries its own ability.
        Eq(CardCatalog.Get(Rebuilding).Type, CardType.Event); Eq(CardCatalog.Get(Rebuilding).Effect, "rebuilding-kyuden-hida");
        Check(CardCatalog.Cards.Values.Count(c => c.Effect == "rebuilding-kyuden-hida") == 1 && CardCatalog.Cards.Values.All(c => c.Type != CardType.Stronghold || c.Effect != "rebuilding-kyuden-hida"), "no Stronghold carries the Event's effect");
        Check(CardCatalog.Get(KyudenHidaStronghold).Type == CardType.Stronghold && CardCatalog.Get(KyudenHidaStronghold).Effect == "none" && CardCatalog.Get(KyudenHidaStronghold).Abilities!.SequenceEqual(["kyuden-hida-exp-scouts"]), "the Stronghold Kyuden Hida - exp is its own card with its own Recon Reaction: nothing confuses it with the Event");
        // Each way to hold the first opportunity: a claim (Sneak Attack's shape), an attacker-side Yuki, Naval Invasion.
        var grants = new (string Name, Action<MatchState> Arrange, Action<MatchEngine> Claim)[]
        {
            ("Sneak Attack",s=>Add(s,Claim,0,Zone.Hand),e=>Accept(e,Use(e,0,"training-engage-attacker-first",e.State.Cards.Values.First(c=>c.DefinitionId==Claim).Id))),
            ("Daidoji Yuki",s=>Add(s,Yuki,0,Zone.Battlefield,province:0),e=>Accept(e,Use(e,0,"yuki-engage-recon",e.State.Cards.Values.First(c=>c.DefinitionId==Yuki).Id))),
            ("Naval Invasion",s=>Add(s,"oracle-l5r-8840",0,Zone.Battlefield,province:0),e=>Accept(e,Use(e,0,"naval-invasion",null,e.State.Cards.Values.First(c=>c.DefinitionId=="oracle-l5r-8840").Id))),
        };
        foreach (var controller in new[] { 0, 1 })
            foreach (var (name, arrange, claim) in grants)
            {
                var s = Selection(); Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
                Add(s, Rebuilding, controller, Zone.EventInPlay); arrange(s);
                var e = MatchEngine.FromTestState(s);
                Take(e, 0, "selectBattle"); claim(e);
                Decline(e); Decline(e);
                Eq(e.State.PriorityPlayerId, 0, $"{name}: the Attacker holds the first opportunity...");
                Eq(Step(e).FirstActionLimit, BattleSteps.LimitMustPass, $"{name}: ...but may only pass it");
                Check(!e.QueryActions(0).Any(o => o.Type is "useAbility" or "playStrategy" or "recruitReserve" && o.Enabled), $"{name}: no Battle action is offered");
                Check(e.QueryActions(0).Any(o => o.Type == "pass" && o.Enabled), $"{name}: passing is");
                Pass(e, 0);
                Eq(e.State.PriorityPlayerId, 1, $"{name}: the Defender's first normal opportunity follows");
                Eq(Step(e).ConsecutivePasses, 1, $"{name}: the forced pass IS a pass and counts");
                Pass(e, 1);
                Eq(e.State.Phase, Phase.Dynasty, $"{name}: the Defender's pass after it ends the Combat Segment");
            }
        // The Defender who acts instead is followed by the Attacker, freed: the limit was only on the FIRST opportunity.
        var s2 = Selection(); Add(s2, "training-scout-samurai", 0, Zone.Battlefield, province: 0); Add(s2, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var bower = Add(s2, "training-delayed-caster", 0, Zone.Battlefield, province: 0); Add(s2, Rebuilding, 1, Zone.EventInPlay); Add(s2, Claim, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Take(e2, 0, "selectBattle"); Accept(e2, Use(e2, 0, "training-engage-attacker-first", e2.State.Cards.Values.First(c => c.DefinitionId == Claim).Id)); Decline(e2); Decline(e2);
        Pass(e2, 0); Pass(e2, 1);
        Eq(e2.State.Phase, Phase.Dynasty);
        // No claim: the Event changes nothing at all -- the Defender acts first, unrestricted, and the Attacker acts after them.
        var s3 = Selection(); Add(s3, "training-scout-samurai", 0, Zone.Battlefield, province: 0); var d3 = Add(s3, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var c3 = Add(s3, "training-delayed-caster", 0, Zone.Battlefield, province: 0); Add(s3, Rebuilding, 1, Zone.EventInPlay);
        var e3 = MatchEngine.FromTestState(s3);
        Take(e3, 0, "selectBattle");
        Eq(e3.State.PriorityPlayerId, 1); Eq(Step(e3).FirstActionLimit, (string?)null);
        Pass(e3, 1);
        Target(e3, 0, "training-delayed-bow", d3.Id);
    }

    // ---- 5. the deferred Attack's source card ----------------------------------------------------------------------
    private sealed record AttackRig(MatchEngine E, CardInstance Ebizo, CardInstance Source, CardInstance Performer, CardInstance Target);
    // player 0's Ebizo at Home; an Attack ability (Melee 2, training-weapon-attack) on `sourceDef` performed by `performerDef`; an enemy attack reactor
    // holds the Attack open so it defers behind its reaction window.
    private static AttackRig BuildAttack(string sourceDef, string performerDef)
    {
        var s = Selection(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 0;
        var ebizo = Add(s, Ebizo, 0, Zone.Home);
        var performer = Add(s, performerDef, 0, Zone.Battlefield, province: 0); if (CardCatalog.Get(sourceDef).Type == CardType.Spell) performer.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var source = sourceDef == performerDef ? performer : Add(s, sourceDef, 0, CardCatalog.Get(sourceDef).Type == CardType.Follower || CardCatalog.Get(sourceDef).Type is CardType.Item or CardType.Spell ? Zone.Attached : Zone.Battlefield, province: 0, parent: performer.Id);
        var target = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-attack-reactor", 1, Zone.Battlefield, province: 0);
        return new(MatchEngine.FromTestState(s), ebizo, source, performer, target);
    }
    private static int ReconBonuses(AttackRig r) => r.E.State.Cards[r.Ebizo.Id].Modifiers.Count(m => m.Effect == "force-after-own-recon-action");
    private static void DeferredAttackSource()
    {
        foreach (var sourceDef in new[] { "training-recon-weapon", "training-recon-spell", "training-recon-follower" })
        {
            var r = BuildAttack(sourceDef, "training-recon-actor");   // the performer is a Scout, not Recon: only the SOURCE card is
            Check(!CardCatalog.Get("training-recon-actor").Keywords!.Contains("Recon") && CardCatalog.Get(sourceDef).Keywords!.Contains("Recon"));
            Accept(r.E, Use(r.E, 0, "training-weapon-attack", r.Source.Id, target: r.Target.Id));
            Eq(r.E.State.PendingChoice?.Type, "attackReaction", $"{sourceDef}: the Attack defers behind its reaction window");
            Eq(r.E.State.PendingAttack!.Command!.CardId, r.Source.Id, $"{sourceDef}: the pending Attack keeps the card the ability is printed on");
            Eq(r.E.State.PendingAttack.SourceId, CardCatalog.Get(sourceDef).Type == CardType.Follower ? r.Source.Id : r.Performer.Id, $"{sourceDef}: and still the performer (a Follower performs its own action; an Item or Spell's is performed by its wielder)");
            Eq(ReconBonuses(r), 0, "announced, not resolved: no bonus yet");
            DeclineAll(r.E);
            Eq(ReconBonuses(r), 1, $"{sourceDef}: the source card's Recon keyword makes the deferred Attack a Recon action when it resolves");
        }
        // The Personality-only case: a Recon Personality's own ability.
        var own = BuildAttack("training-recon-attacker", "training-recon-attacker");
        Accept(own.E, Use(own.E, 0, "training-weapon-attack", own.Source.Id, target: own.Target.Id));
        Eq(own.E.State.PendingAttack!.Command!.CardId, own.Source.Id); Eq(ReconBonuses(own), 0);
        DeclineAll(own.E);
        Eq(ReconBonuses(own), 1, "a Personality's own Recon keyword still makes its Attack a Recon action");
        // The performer's keywords are NOT the ability's: a plain weapon wielded by a Recon Personality is no Recon action.
        var plain = BuildAttack("training-weapon-with-attack", "training-recon-attacker");
        Accept(plain.E, Use(plain.E, 0, "training-weapon-attack", plain.Source.Id, target: plain.Target.Id));
        Eq(plain.E.State.PendingAttack!.Command!.CardId, plain.Source.Id); Eq(plain.E.State.PendingAttack.SourceId, plain.Performer.Id);
        DeclineAll(plain.E);
        Eq(ReconBonuses(plain), 0, "an ability inherits the keywords of the card it is on, not of whoever performs it");
        // Synchronous actions agree: the same weapon, nothing able to react, resolves at once and is still a Recon action by its own card.
        var s = Selection(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 0;
        var eb = Add(s, Ebizo, 0, Zone.Home); var host = Add(s, "training-recon-actor", 0, Zone.Battlefield, province: 0); var sword = Add(s, "training-recon-weapon", 0, Zone.Attached, province: 0, parent: host.Id);
        var syncTarget = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var sync = MatchEngine.FromTestState(s);
        Accept(sync, Use(sync, 0, "training-weapon-attack", sword.Id, target: syncTarget.Id));
        Eq(sync.State.Cards[eb.Id].Modifiers.Count(m => m.Effect == "force-after-own-recon-action"), 1);
    }

    private static void DeferredDuelSource()
    {
        int Duel(string strategyDef, string performerDef)
        {
            var s = Selection(); s.Phase = Phase.Battle; s.CurrentBattleProvince = 0; s.PriorityPlayerId = 0;
            var eb = Add(s, Ebizo, 0, Zone.Home); var performer = Add(s, performerDef, 0, Zone.Battlefield, province: 0); var strategy = Add(s, strategyDef, 0, Zone.Hand);
            var enemy = Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 0, "steel-on-steel-duel", strategy.Id, performer.Id, enemy.Id));
            Check(e.State.PendingDuel is not null, "the duel is under way");
            Eq(e.State.PendingDuel!.SourceCardId, strategy.Id, "the pending duel keeps the card the ability is printed on");
            Eq(e.State.Cards[eb.Id].Modifiers.Count(m => m.Effect == "force-after-own-recon-action"), 0, "announced, not resolved");
            Accept(e, new GameCommand("strike", e.State.PriorityPlayerId, e.Version));
            Check(e.State.PendingDuel is null, "the duel is over");
            return e.State.Cards[eb.Id].Modifiers.Count(m => m.Effect == "force-after-own-recon-action");
        }
        Eq(Duel("training-recon-duel-strategy", "training-recon-actor"), 1, "a Recon Strategy's duel is a Recon action though its performer is not Recon");
        Eq(Duel("training-plain-duel-strategy", "training-recon-attacker"), 0, "a plain Strategy's duel is not, though its performer has the Recon keyword");
    }
}
