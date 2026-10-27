using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

/// <summary>Isolated native integration scene. Prepared states use the core test factory;
/// every tested action executes through the real MatchSession/MatchEngine.</summary>
public partial class PersistentHomeProbe : Node
{
    private Main _main = null!;
    private MatchSession _session = null!;
    private string _output = "";
    private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private T Field<T>(string key) => (T)typeof(Main).GetField(key, Private)!.GetValue(_main)!;
    private void Set(string key, object? value) => typeof(Main).GetField(key, Private)!.SetValue(_main, value);
    // Overloads (Choose has two) are told apart by argument count.
    private object? Call(string key, params object?[] values) => typeof(Main).GetMethods(Private).Single(m => m.Name == key && m.GetParameters().Length == values.Length).Invoke(_main, values);
    private MatchEngine Engine => (MatchEngine)typeof(MatchSession).GetField("_engine", Private)!.GetValue(_session)!;
    private List<HomeReminder> Reminders => Field<List<HomeReminder>>("_homeReminders");
    private List<CardTile> Tiles => Field<List<CardTile>>("_tiles");
    private static void Require(bool test, string message) { if (!test) throw new Exception(message); }
    private async Task Settle(double seconds = .2) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static IEnumerable<T> Descendants<T>(Node parent) where T : Node => parent.GetChildren().SelectMany(n => (n is T t ? new[] { t } : []).Concat(Descendants<T>(n)));
    private Button Button(string text) => Descendants<Button>(_main).First(b => b.Text == text && b.IsVisibleInTree() && !b.Disabled);
    private async Task Click(Control control)
    {
        var point = (Vector2)Call("ScreenCenter", control)!;
        Require(new Rect2(Vector2.Zero, GetViewport().GetVisibleRect().Size).HasPoint(point), $"Control is outside viewport: {control.Name} {point}");
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point }); await Settle(.06);
        GD.Print($"HOME_CLICK: {control.GetPath()} at {point}; root hover {GetViewport().GuiGetHoveredControl()?.GetPath()}; plane hover {Field<TableSurface>("_tableSurface").Canvas.GuiGetHoveredControl()?.GetPath()}; on table {Call("PointerOnTable", point)}");
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        Input.ParseInputEvent(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await Settle();
    }
    private void RequireInOverview(Control control)
    {
        var rect = control.GetGlobalRect(); var surface = Field<TableSurface>("_tableSurface");
        var viewport = Field<Control>("_tableViewport").GetGlobalRect().Grow(1);
        foreach (var corner in new[] { rect.Position, rect.End, new Vector2(rect.End.X, rect.Position.Y), new Vector2(rect.Position.X, rect.End.Y) })
            Require(viewport.HasPoint(surface.ProjectCanvas(corner)), "New Home content clipped outside fitted table viewport");
    }
    private async Task Capture(string name)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = new Vector2(8, 8) });
        Call("HideTableTooltip"); await Settle(.3);
        GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output, name + ".png"));
    }
    private static CardInstance Add(MatchState state, int definition, int owner = 0, Zone zone = Zone.Home, bool bowed = false, int? province = null)
    {
        var card = new CardInstance
        {
            Id = "home-probe-" + state.Cards.Count,
            DefinitionId = "oracle-l5r-" + definition,
            OwnerId = owner,
            Zone = zone,
            Bowed = bowed,
            ProvinceIndex = province,
            FaceUp = zone != Zone.Hand
        };
        state.Cards.Add(card.Id, card);
        var player = state.Players[owner];
        if (zone == Zone.Hand) player.Hand.Add(card.Id);
        if (zone == Zone.FateDeck) player.FateDeck.Add(card.Id);
        if (zone == Zone.DynastyDeck) player.DynastyDeck.Add(card.Id);
        if (zone == Zone.Province) player.Provinces[province!.Value].CardId = card.Id;
        return card;
    }
    private static MatchState State(bool battle = false)
    {
        var state = MatchEngine.CreatePublishedTestMatch(11).State;
        foreach (var card in state.Cards.Values.Where(c => c.Zone != Zone.Stronghold).ToArray()) state.Cards.Remove(card.Id);
        foreach (var p in state.Players)
        {
            p.Hand.Clear(); p.FateDeck.Clear(); p.DynastyDeck.Clear(); p.TurnsTaken = 2; p.Honor = p.Id == 0 ? 10 : 7;
            foreach (var province in p.Provinces) province.CardId = null;
        }
        state.Events.Clear(); state.ActivePlayerId = 0; state.PriorityPlayerId = battle ? 1 : 0; state.Phase = battle ? Phase.Battle : Phase.Action;
        if (battle) { state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; Add(state, 1645, 0, Zone.Battlefield, province: 0); Add(state, 3636, 1, Zone.Battlefield, province: 0); }
        return state;
    }
    private async Task Load(MatchState state, int seat = 0)
    {
        Call("StartFromMenu", seat); await Settle(.1);
        var engine = typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state]);
        typeof(MatchSession).GetField("_engine", Private)!.SetValue(_session, engine);
        typeof(MatchSession).GetField("_localSeat", Private)!.SetValue(_session, seat);
        Set("_presentationSnapshot", null); Set("_motionView", null); Set("_battlePresentationStage", 0);
        Field<TurnBloom>("_turnBloom").Cancel();
        typeof(MatchSession).GetMethod("RefreshLocal", Private)!.Invoke(_session, null);
        await Settle(.3); Call("Render"); Call("ResetTableCamera"); await Settle(.3); Field<TurnBloom>("_turnBloom").Cancel();
        GD.Print($"HOME_FIXTURE: {state.Phase}, seat {seat}, favor {Engine.State.FavorPlayerId}, tiles {string.Join(",", Tiles.Select(t => t.Card.Definition?.Name))}");
    }
    private async Task Ability(string id)
    {
        var option = _session.Snapshot!.Actions.First(a => a.Enabled && a.Command?.AbilityId == id);
        var version = Engine.Version;
        Call("Choose", option, false); await Settle(); // Choose(option, keepTarget): reflection does not fill optional parameters
        // Issue #581: the client chooses no target for the player; pick the first legal one through the real picker (the first row of the legal-card list) before confirming.
        if (Field<ActionOption?>("_chosen") is { TargetIds.Length: > 0, Command.TargetId: null })
        {
            var picker = Descendants<CardChoiceList>(_main).First(b => b.IsVisibleInTree()); // issue #635: the list of legal cards
            picker.Choose(0); await Settle();
        }
        // Generic reviewed actions use their stable commit field; Region/Celestial/
        // Ring drafts retain their own separate, unchanged support controls.
        var commit = Field<Button?>("_paymentConfirm") ?? Button("Confirm action");
        Require(commit.IsVisibleInTree() && !commit.Disabled, "Reviewed ability has no visible enabled commit");
        await Click(commit); await Settle(.5);
        Require(Engine.Version > version, id + " did not execute through native confirmation");
    }
    private async Task Pass()
    {
        await _session.Submit(new GameCommand("pass", Engine.State.PriorityPlayerId, Engine.Version)); await Settle(.1);
    }
    public override async void _Ready()
    {
        try
        {
            _output = OS.GetCmdlineUserArgs().Single(a => a.StartsWith("--output="))[9..]; Directory.CreateDirectory(_output);
            _main = new Main(); Set("_profile", "persistent-home-probe"); Set("_autoOpponent", false); AddChild(_main);
            Set("_reducedMotion", !OS.GetCmdlineUserArgs().Contains("--normal-motion")); _session = Field<MatchSession>("_session");
            await Settle();
            var state = State(); var lobby = Add(state, 521); var discard = Add(state, 2619, zone: Zone.Hand); var drawn = Add(state, 6051, zone: Zone.FateDeck);
            await Load(state);
            Require(Reminders.Count == 0 && Field<Dictionary<int, Rect2>>("_persistentBounds").Count == 0, "Empty Home section must stay hidden");
            await Ability("lobby");
            Require(Reminders.Count(r => r.IsFavor) == 1 && Reminders.Single(r => r.IsFavor).OwnerId == 0, "Lobby must create one Favor at its holder");
            Require(Engine.GetView(0).FavorPlayerId == 0 && Engine.GetView(1).FavorPlayerId == 0, "Favor ownership must agree for both public views");
            await Pass();
            RequireInOverview(Reminders.Single(r => r.IsFavor));
            await Click(Reminders.Single(r => r.IsFavor));
            Require(Field<object?>("_selectedHomeReminder") is not null, $"Projected Favor click did not inspect. Chosen={Field<ActionOption?>("_chosen")?.Type}; hovered={typeof(Main).GetProperty("TableHovered", Private)!.GetValue(_main)}; lastMouse={Field<Vector2>("_lastTableMouse")}; favor={Call("ScreenCenter", Reminders.Single(r => r.IsFavor))}");
            Require(!Tiles.Any(t => t.Highlighted), "Reminder selection left a card highlighted");
            Require(Descendants<Button>(Field<VBoxContainer>("_inspector")).Any(b => b.Text.Contains("Discard Favor") && !b.Disabled), "Available Favor action missing from inspection");
            await Capture("favor-gained");
            // Hover is inspection-only and inherits the currently approved menu material.
            var favor = Reminders.Single(r => r.IsFavor);
            var point = (Vector2)Call("ScreenCenter", favor)!;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point });
            await Settle(.8);
            Require(Field<Control?>("_tableTooltip") is not null, "Favor hover did not open reading preview");
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_output, "favor-hover.png"));
            Call("HideTableTooltip");
            await Ability("favor-cycle");
            Require(Engine.State.FavorPlayerId is null && Reminders.All(r => !r.IsFavor), "Spent Favor did not disappear");
            Require(Engine.State.Cards[discard.Id].Zone == Zone.FateDiscard && Engine.State.Players[0].Hand.Contains(drawn.Id), "Favor discard/draw destinations wrong");
            Require(Field<object?>("_selectedHomeReminder") is null, "Spent Favor left stale inspector");
            GD.Print("HOME_PASS: lobby, public ownership, projected inspection, Limited spend/draw, empty hide");

            state = State(); state.FavorPlayerId = 1; Add(state, 521); await Load(state); await Ability("lobby");
            Require(Reminders.Single(r => r.IsFavor).OwnerId == 0, "Favor transfer duplicated or retained the old holder");
            _session.SwitchPracticeSeat(); await Settle(.4);
            await Click(Reminders.Single(r => r.IsFavor));
            Require(Reminders.Single(r => r.IsFavor).Selected, $"Opponent cannot inspect Favor; selected={Field<object?>("_selectedHomeReminder")}; chosen={Field<ActionOption?>("_chosen")?.Type}; hovering={typeof(Main).GetProperty("TableHovered", Private)!.GetValue(_main)}");
            Require(!Descendants<Button>(Field<VBoxContainer>("_inspector")).Any(b => b.Text.Contains("Discard Favor") && !b.Disabled), "Opponent received holder's Favor action");
            await Capture("opponent-inspection");

            state = State(true); state.FavorPlayerId = 1; var attacker = state.Cards.Values.First(c => c.Zone == Zone.Battlefield && c.OwnerId == 0);
            await Load(state, 1); await Click(Reminders.Single(r => r.IsFavor));
            Require(Descendants<Button>(Field<VBoxContainer>("_inspector")).Any(b => b.Text.Contains("Discard Favor") && !b.Disabled), "Battle flow swallowed the selected Favor action");
            await Ability("favor-battle");
            Require(Engine.State.Cards[attacker.Id].Zone == Zone.Home && !Engine.State.Cards[attacker.Id].Bowed, "Battle Favor did not return attacker home unbowed");
            Require(Reminders.All(r => !r.IsFavor), "Battle spend left Favor visible");
            state = State(true); state.FavorPlayerId = 1; state.Cards.Values.First(c => c.Zone == Zone.Battlefield && c.OwnerId == 1).Zone = Zone.Home;
            await Load(state, 1); await Click(Reminders.Single(r => r.IsFavor));
            Require(!Descendants<Button>(Field<VBoxContainer>("_inspector")).Any(b => b.Text.Contains("Discard Favor") && !b.Disabled), "Battle action shown without engine-required presence");
            state = State(); state.FavorPlayerId = 0; Add(state, 2158); await Load(state); await Ability("shunya-honor");
            Require(Engine.State.FavorPlayerId is null && Reminders.All(r => !r.IsFavor), "Card effect spending Favor left stale display");
            GD.Print("HOME_PASS: transfer, opponent inspection, Battle spend/presence, card-effect loss");

            // Consolidation regression: newer engine commands must remain reachable
            // through the older battle-flow UI, without re-creating legality here.
            state = State(true); var reserve = Add(state, 10832, 1, Zone.Province, province: 0); Add(state, 6086, 1);
            await Load(state, 1);
            var reserveOption = _session.Snapshot!.Actions.First(a => a.Enabled && a.Type == "recruitReserve");
            Require(!Descendants<Button>(Field<VBoxContainer>("_inspector")).Any(b => b.Text == "Concede"), "Concede bypassed the table menu's confirmation flow");
            var reserveButton = Descendants<Button>(Field<VBoxContainer>("_inspector")).FirstOrDefault(b => b.Text.EndsWith(reserveOption.Label) && !b.Disabled);
            Require(reserveButton is not null, "Battle flow hid engine-provided Reserve recruitment");
            reserveButton!.EmitSignal(BaseButton.SignalName.Pressed); await Settle(.4);
            // The Reserve button opens the reviewed Gold payment panel (issue #303); the engine action is submitted on Confirm.
            var confirmReserve = Field<Button?>("_paymentConfirm");
            // A fixed cost shows Confirm only once the sources chosen cover it: tick the offered Gold sources one at a time, as a player does, until the commit appears.
            for (var pick = 0; pick < 6 && !(confirmReserve is { Disabled: false } && confirmReserve.IsVisibleInTree()); pick++)
            {
                var source = Descendants<CheckBox>(Field<VBoxContainer>("_inspector")).FirstOrDefault(b => b.IsVisibleInTree() && !b.Disabled && !b.ButtonPressed);
                if (source is null) break;
                await Click(source); await Settle(.3); confirmReserve = Field<Button?>("_paymentConfirm");
            }
            Require(confirmReserve is not null && confirmReserve.Name == "CommitAction" && confirmReserve.IsVisibleInTree() && !confirmReserve.Disabled, $"Reserve did not open its reviewed payment with a visible enabled commit: confirm={(confirmReserve is null ? "none" : confirmReserve.Name + "/" + confirmReserve.Text + "/disabled=" + confirmReserve.Disabled + "/visible=" + confirmReserve.IsVisibleInTree())}, chosen={Field<ActionOption?>("_chosen")?.Type}, buttons: {string.Join(" | ", Descendants<Button>(Field<VBoxContainer>("_inspector")).Where(x => x.IsVisibleInTree()).Select(x => (x.Disabled ? "[off] " : "") + x.Text))}");
            await Click(confirmReserve!); await Settle(.5);
            Require(Engine.State.Cards[reserve.Id].Zone == Zone.Battlefield, "Reserve button did not submit the engine action");
            GD.Print("HOME_PASS: consolidated battle flow exposes and executes authoritative Reserve recruitment");

            state = State(); var air = Add(state, 6333); var bowed = Add(state, 521, bowed: true); state.FavorPlayerId = 0;
            await Load(state); await Click(Tiles.Single(t => t.Card.Id == air.Id));
            Require(Field<CardView?>("_selected")?.Id == air.Id, $"Persistent Ring must retain normal card selection: selected={Field<CardView?>("_selected")?.Definition?.Name}, snapshot={_session.Snapshot!.View.FavorPlayerId}, engine={Engine.State.FavorPlayerId}");
            await Ability("ring-of-air-straighten");
            Require(Engine.State.Cards[air.Id].Bowed && !Engine.State.Cards[bowed.Id].Bowed, "Persistent Ring action did not execute normally");
            await Click(Reminders.Single(r => r.IsFavor));

            var pending = Engine.State; pending.Phase = Phase.End; pending.ActivePlayerId = pending.PriorityPlayerId = 0;
            for (var i = 0; i < 9; i++) Add(pending, 2619, zone: Zone.Hand);
            pending.PendingChoice = new PendingChoice("discardHand", 0, null, "Discard down to your hand limit.");
            var pendingEngine = typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [pending]);
            typeof(MatchSession).GetField("_engine", Private)!.SetValue(_session, pendingEngine);
            typeof(MatchSession).GetMethod("RefreshLocal", Private)!.Invoke(_session, null); await Settle(.4);
            // The hand-limit decision is now "click cards in your hand, then Confirm discard": the sidebar names the choice and counts the selection, and the draft is the chosen action.
            Require(Field<ActionOption?>("_chosen")?.Type == "discardHand" && Field<VBoxContainer>("_inspector").FindChild("DiscardStatus", true, false) is not null, "Inspected Favor swallowed the new hand-limit decision");
            await Capture("pending-decision");
            Call("StartFromMenu", 0); await Settle(); Require(Field<object?>("_selectedHomeReminder") is null, "New game retained old reminder selection");
            GD.Print("HOME_PASS: Ring input/action/bowing, required decision while inspecting, new-game reset");

            state = MatchEngine.CreateEconomyScenario().State;
            var suspicions = Add(state, 7675, 1, Zone.Province, province: 0); suspicions.FaceUp = false;
            var honors = state.Players.Select(p => p.Honor).ToArray(); await Load(state);
            for (var i = 0; Engine.State.Phase != Phase.Dynasty && i < 30; i++) await Pass();
            Require(Engine.State.Phase == Phase.Dynasty, "Event fixture did not reach Dynasty");
            await _session.Submit(new GameCommand("endDynasty", Engine.State.PriorityPlayerId, Engine.Version)); await Settle(.6);
            Require(Engine.State.Cards[suspicions.Id].Zone == Zone.EventInPlay && Reminders.Any(r => r.Effect?.Id == suspicions.Id), "Resolved Suspicions did not create reminder");
            Require(Engine.State.Players[0].Honor == honors[0] - 1 && Engine.State.Players[1].Honor == honors[1] - 1, "Suspicions resolution changed");
            Require(!Tiles.Any(t => t.Card.Id == suspicions.Id), "Ongoing effect was represented by a targetable CardTile");
            foreach (var seat in new[] { 0, 1 })
            {
                if (_session.Snapshot!.View.ViewerId != seat) { _session.SwitchPracticeSeat(); await Settle(.3); }
                var before = Engine.Version; await Click(Reminders.Single(r => r.Effect?.Id == suspicions.Id));
                Require(Engine.Version == before && Field<object?>("_selectedHomeReminder") is not null, "Reminder inspection submitted an action");
                Require(!Descendants<Button>(Field<VBoxContainer>("_inspector")).Any(b => b.Text.Contains("Confirm")), "Ongoing effect has a gameplay confirmation");
                await Capture("suspicions-seat-" + seat); Call("DeselectCard");
            }
            GD.Print("HOME_PASS: actual Suspicions reveal, Honor effect, both-seat inspection, no card targeting");

            // Capacity fixture, explicitly not a claim that these duplicate Rings were recruited legally.
            state = State(); state.FavorPlayerId = 0;
            foreach (var owner in new[] { 0, 1 }) { for (var i = 0; i < 6; i++) Add(state, i % 2 == 0 ? 6333 : 6339, owner); Add(state, 7675, owner, Zone.EventInPlay); }
            foreach (var owner in new[] { 0, 1 }) { Add(state, 521, owner, Zone.RemovedFromGame); Add(state, 2619, owner, Zone.RemovedFromGame); }
            await Load(state); await Capture("crowded-overview");
            var removed = Field<Dictionary<int, Rect2>>("_removedBounds");
            Require(removed.Count == 2, "Consolidation lost one of the removed-card piles");
            foreach (var home in Field<Dictionary<int, Rect2>>("_persistentBounds").Values)
                Require(removed.Values.All(pile => !pile.Intersects(home)), "Removed-card pile overlaps persistent Home");
            foreach (var seat in new[] { 0, 1 })
            {
                if (_session.Snapshot!.View.ViewerId != seat) { _session.SwitchPracticeSeat(); await Settle(.4); }
                var bounds = Field<Dictionary<int, Rect2>>("_persistentBounds")[seat];
                var zoom = (float)Call("FitRegion", bounds.Grow(50))!;
                Set("_cameraCenter", bounds.GetCenter()); Set("_cameraZoom", zoom); Set("_cameraFitted", false); Call("ApplyTableCamera"); await Settle(.3);
                var reminder = Reminders.First(r => r.OwnerId == seat);
                await Click(reminder); Require(reminder.Selected, "Zoomed reminder is not clickable");
                await Capture("crowded-zoom-seat-" + seat); Call("DeselectCard");
            }
            GD.Print("PERSISTENT_HOME_PASS: all real-command and native-input checks passed; capacity images are staged fixtures.");
            GetTree().Quit();
        }
        catch (Exception e) { await Capture("failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
