using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _supportCaptureStarted;
    private void ProcessSupportCapture(double delta)
    {
        _captureClock += delta;
        if (_supportCaptureStarted || _captureClock < 1.5) return;
        _supportCaptureStarted = true; CaptureSupports();
    }
    private static CardInstance SupportFixtureCard(MatchState state, int number, int owner, Zone zone, int? province = null, bool bowed = false)
        => SupportFixtureCard(state, "oracle-l5r-" + number, owner, zone, province, bowed);
    private static CardInstance SupportFixtureCard(MatchState state, string definition, int owner, Zone zone, int? province = null, bool bowed = false)
    {
        var card = new CardInstance
        {
            Id = "support-native-" + state.Cards.Count,
            DefinitionId = definition,
            OwnerId = owner,
            Zone = zone,
            FaceUp = zone != Zone.Province,
            ProvinceIndex = province,
            Bowed = bowed
        };
        state.Cards.Add(card.Id, card);
        if (zone == Zone.Hand) state.Players[owner].Hand.Add(card.Id);
        if (zone == Zone.Region) state.Players[owner].Provinces[province!.Value].RegionId = card.Id;
        if (zone == Zone.Province) state.Players[owner].Provinces[province!.Value].CardId = card.Id;
        return card;
    }
    private static MatchEngine SupportFixture(bool crowded = false, bool battle = false)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = battle ? Phase.Battle : Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0;
        state.FavorPlayerId = 0; state.PendingChoice = null; state.CurrentBattleProvince = battle ? 0 : null;
        state.UnresolvedBattlefields = battle ? [0] : [];
        foreach (var p in state.Players)
        {
            p.TurnsTaken = 2;
            foreach (var i in crowded ? new[] { 0, 1, 2, 3 } : new[] { 1, 2 })
                SupportFixtureCard(state, i % 2 == 1 ? 4777 : 9865, p.Id, Zone.Region, i, crowded && i == 1);
            SupportFixtureCard(state, 4658, p.Id, Zone.Celestial);
            for (var i = 0; i < (crowded ? 6 : 3); i++) SupportFixtureCard(state, p.Id == 0 ? 2060 : 3636, p.Id, battle ? Zone.Battlefield : Zone.Home, battle ? 0 : null);
            SupportFixtureCard(state, 6333, p.Id, Zone.Home); SupportFixtureCard(state, 6339, p.Id, Zone.Home);
            SupportFixtureCard(state, 7675, p.Id, Zone.EventInPlay);
        }
        SupportFixtureCard(state, 6481, 0, Zone.Hand);
        return (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
    }
    private async Task LoadSupportFixture(MatchEngine engine, int seat)
    {
        _autoOpponent = false; _session.StartEventProbe(engine, seat); _uiQueue.Clear();
        _presentationSnapshot = null; _battlePresentationStage = 0; _motionView = null; _selected = null; _chosen = null; _selectedHomeReminder = null; _tableLayoutSeen = false;
        _expandedUnits.Clear(); _turnBloom.Cancel(); Render(); ResetTableCamera(); GetWindow().GrabFocus();
        await EventFrames(24); _turnBloom.Cancel(); await EventFrames(6);
    }
    private async Task SupportCapture(string suffix)
    {
        HideTableTooltip(); ReleaseTableFocus();
        var p = new Vector2(12, 220); var motion = new InputEventMouseMotion { Position = p, GlobalPosition = p }; motion.SetMeta(CaptureMouseTag, true); GetViewport().PushInput(motion, true);
        await EventFrames(18);
        var path = Path.ChangeExtension(_capturePath, null) + suffix + ".png";
        if (GetViewport().GetTexture().GetImage().SavePng(path) != Error.Ok) throw new Exception("Could not save " + path);
    }
    // A card another card partly covers (an attached Region: a strip above its Dynasty card, or the sides in a crowded province) is clicked where the pointer really reaches it: try points around the tile's
    // edge and return the first one whose hover lands on this card.
    private async Task<Vector2> ReachablePoint(CardTile tile)
    {
        var w = tile.Size.X; var h = tile.Size.Y;
        foreach (var local in new[] { new Vector2(w / 2, RegionStrip / 2), new Vector2(w / 2, 4), new Vector2(w * .25f, RegionStrip / 2), new Vector2(w * .75f, RegionStrip / 2), new Vector2(4, h * .3f), new Vector2(w - 4, h * .3f), new Vector2(4, h * .6f), new Vector2(w - 4, h * .6f), new Vector2(4, h * .85f), new Vector2(w - 4, h * .85f) })
        {
            var p = _tableSurface.ProjectCanvas(tile.GetGlobalTransform() * local);
            var motion = new InputEventMouseMotion { Position = p, GlobalPosition = p }; motion.SetMeta(CaptureMouseTag, true); GetViewport().PushInput(motion, true); await EventFrames(3);
            if ((TableHovered as CardTile)?.Card.Id == tile.Card.Id) return p;
        }
        throw new Exception($"No visible, reachable part of {tile.Card.Id} to click");
    }
    private async Task SupportClick(Control control, bool reachable = false)
    {
        if (control is not CardTile)
        {
            for (Node? parent = control.GetParent(); parent is not null; parent = parent.GetParent())
                if (parent is ScrollContainer scroll) scroll.EnsureControlVisible(control);
            await EventFrames(5);
        }
        var p = control is CardTile tile ? reachable ? await ReachablePoint(tile) : ScreenCenter(tile) : control.GetGlobalRect().GetCenter();
        void Push(InputEventMouse input) { input.SetMeta(CaptureMouseTag, true); GetViewport().PushInput(input, true); }
        Push(new InputEventMouseMotion { Position = p, GlobalPosition = p }); await EventFrames(3);
        if (control is CardTile clicked) GD.Print($"SUPPORT_CLICK: {clicked.Card.Id} at {p}; focus={GetWindow().HasFocus()}, blocked={CameraBlocked}, hovered={(TableHovered as CardTile)?.Card.Id}, busy={PresentationBusy}, viewer={View.ViewerId}");
        Push(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, ButtonMask = MouseButtonMask.Left, Pressed = true });
        Push(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(10);
    }
    private static void SupportRequire(bool condition, string message) { if (!condition) throw new Exception(message); }
    private void CheckSupportLayout()
    {
        var expected = View.Players.SelectMany(p => p.Provinces).Count(p => p.Region is not null && !p.Destroyed);
        SupportRequire(_tiles.Count(t => t.Card.Zone == Zone.Region) == expected, "Missing or duplicate Region tiles");
        SupportRequire(_tiles.Count(t => t.Card.Zone == Zone.Celestial) == View.Players.Sum(p => (p.Celestials ?? []).Length), "Missing or duplicate Celestial tiles");
        foreach (var owner in View.Players)
            foreach (var province in owner.Provinces.Where(p => p.Region is not null && !p.Destroyed))
            {
                var r = _tiles.Single(t => t.Card.Id == province.Region!.Id); var d = _tiles.Single(t => t.Card.Id == province.Card!.Id);
                SupportRequire(r.GetGlobalRect().Position.Y < d.GetGlobalRect().Position.Y, "Region is not attached under its Dynasty card");
                SupportRequire(r.Card.OwnerId == owner.Id && r.Card.ProvinceIndex == province.Index, "Region ownership/position drift");
            }
        foreach (var tile in _tiles.Where(t => t.Card.Zone is Zone.Region or Zone.Celestial))
            SupportRequire(tile.FaceCornersOnViewport().All(p => _tableViewport.GetGlobalRect().Grow(2).HasPoint(p)), "Support card clipped outside fitted table");
    }
    private async void CaptureSupports()
    {
        try
        {
            var seat = _captureMode.Contains("opponent") ? 1 : 0;
            var crowded = _captureMode.Contains("crowded"); _reducedMotion = !_captureMode.Contains("motion");
            if (_captureMode.Contains("dynasty-flow"))
            {
                await CheckDynastyFlow(); GD.Print("REGIONS_CELESTIALS_PASS: " + _captureMode); GetTree().Quit(); return;
            }
            if (_captureMode.Contains("doji"))
            {
                await CheckDojisGuidance(); GD.Print("REGIONS_CELESTIALS_PASS: " + _captureMode); GetTree().Quit(); return;
            }
            if (_captureMode.Contains("reaction"))
            {
                await CheckSupportReaction(); GD.Print("REGIONS_CELESTIALS_PASS: " + _captureMode); GetTree().Quit(); return;
            }
            var engine = SupportFixture(crowded); await LoadSupportFixture(engine, seat); CheckSupportLayout();
            if (_captureMode.Contains("review"))
            {
                SetNotice("Regions and Celestials · prepared practice table · real engine actions");
                await SupportCapture(""); GD.Print("SUPPORT_REVIEW_READY: native prepared table loaded; both owners, Regions, Celestials and Favor verified"); return;
            }
            await SupportCapture(""); var version = View.Version;
            var region = Self.Provinces.First(p => p.Region?.Definition?.Id == "oracle-l5r-4777").Region!;
            await SupportClick(_tiles.Single(t => t.Card.Id == region.Id), reachable: true);
            SupportRequire(_selected?.Id == region.Id && View.Version == version, $"Projected Region inspection failed or mutated game: expected {region.Id}, selected {_selected?.Id}, version {version}->{View.Version}");
            await SupportCapture("-region-inspector");
            var regionBounds = _matTargets.Single(t => t.Id == $"province:{seat}:{region.ProvinceIndex}").Bounds.Grow(100);
            _cameraFitted = false; _cameraCenter = regionBounds.GetCenter(); _cameraZoom = FitRegion(regionBounds) * .65f; ApplyTableCamera(); await SupportCapture("-region-detail");
            if (_chosen is not null) CancelChoice(); else DeselectCard(); ResetTableCamera(); await EventFrames(6); // selecting a card can open its action choice, which DeselectCard leaves alone
            var celestial = Self.Celestials!.Single(); await SupportClick(_tiles.Single(t => t.Card.Id == celestial.Id));
            SupportRequire(_selected?.Id == celestial.Id, $"Projected Celestial inspection failed: expected {celestial.Id}, selected {_selected?.Id}");
            SupportRequire(StabilizationButtons(_inspector).Any(b => b.Text == "Inspect card"), "Missing full-card inspection");
            await SupportCapture("-celestial-inspector");
            var home = _persistentBounds[seat].Grow(55); _cameraFitted = false; _cameraCenter = home.GetCenter(); _cameraZoom = FitRegion(home) * .8f; ApplyTableCamera(); await SupportCapture("-celestial-detail");
            await SupportClick(StabilizationButton("Inspect card")); await SupportCapture("-full-inspection");
            var dialog = GetChildren().OfType<AcceptDialog>().Last(); await StabilizationClick(dialog.GetOkButton()); await EventFrames(5);
            SupportRequire(View.Version == version, "Full-card inspection submitted a command");
            if (_chosen is not null) CancelChoice(); else DeselectCard(); ResetTableCamera(); await EventFrames(6); // selecting a card can open its action choice, which DeselectCard leaves alone
            GD.Print("SUPPORT_PASS: both-owner public layout, projected inspection, independent Region/Dynasty slots, full original scan, no inspection command");
            if (!crowded && seat == 0)
            {
                await SupportClick(_tiles.Single(t => t.Card.Id == region.Id), reachable: true);
                var option = Actions.First(a => a.Enabled && a.CardId == region.Id && a.Command?.AbilityId == "low-market-equip" && QuoteFor(a)?.Cost > 0);
                var beforeProvince = Self.Provinces[region.ProvinceIndex!.Value].Card!.Id;
                // The choosing flow (#1004) opens the drafted ability at once and asks for the performer first; click the row for the performer the engine offered.
                var performerRow = StabilizationButtons(_inspector).FirstOrDefault(b => b.GetMeta("performer_row", "").AsString() == option.Command!.PerformerId);
                await SupportClick(performerRow ?? StabilizationButton("Use Open ability"));
                SupportRequire(_chosen?.Command?.AbilityId == "low-market-equip", "Region ability button did not choose authoritative option");
                // Pick the engine's offered payment through actual checkboxes, then confirm.
                var payment = QuoteFor(option)!.Payments.First();
                foreach (var id in payment.SourceIds)
                {
                    // The row names the card; the check box carries the payment method's own label, so find it by the source it stands for.
                    CheckBox SourceCheck() => StabilizationButtons(_inspector).OfType<CheckBox>().First(b => b.GetMeta("payment_source_id", "").AsString() == id);
                    var check = SourceCheck();
                    if (check.ButtonPressed) await SupportClick(check);
                    check = SourceCheck();
                    await SupportClick(check);
                }
                await SupportCapture("-region-confirm"); await SupportClick(StabilizationButton("Confirm action")); await EventFrames(30);
                SupportRequire(engine.State.Cards[region.Id].Bowed, "Low Market confirmation did not bow source");
                SupportRequire(Self.Provinces[region.ProvinceIndex.Value].Card!.Id == beforeProvince, "Using Region altered province's Dynasty card");
                SupportRequire(engine.State.Cards[option.TargetIds!.First()].Zone == Zone.Attached, "Low Market did not attach the engine-selected card");
                ResetTableCamera(); await SupportCapture("-region-used");
                GD.Print("SUPPORT_PASS: Low Market native action, offered payment, source bow, independent province card");
                engine = SupportFixture(battle: true); await LoadSupportFixture(engine, 0);
                celestial = Self.Celestials!.Single(); await SupportClick(_tiles.Single(t => t.Card.Id == celestial.Id));
                option = Actions.First(a => a.Enabled && a.CardId == celestial.Id && a.Command?.AbilityId == "lady-matsus-rage-bow-destroy");
                performerRow = StabilizationButtons(_inspector).FirstOrDefault(b => b.GetMeta("performer_row", "").AsString() == option.Command!.PerformerId);
                await SupportClick(performerRow ?? StabilizationButton("Use Battle ability"));
                SupportRequire(_chosen?.Command?.AbilityId == "lady-matsus-rage-bow-destroy", "Native Celestial ability button did not open a draft");
                await SupportCapture("-celestial-confirm"); await SupportClick(StabilizationButton("Confirm action")); await EventFrames(40);
                SupportRequire(engine.State.Cards[celestial.Id].Zone == Zone.DynastyDiscard, "Celestial ability did not pay discard cost");
                SupportRequire(_tiles.All(t => t.Card.Id != celestial.Id), "Spent Celestial left a stale table frame");
                SupportRequire(View.Players.Single(p => p.Id == 1).Celestials!.Length == 1 && View.FavorPlayerId == 0, "Spending Celestial changed opponent's Celestial or Favor");
                await SupportCapture("-celestial-spent"); GD.Print("SUPPORT_PASS: Celestial native action and discard, opponent/Favor independence, no stale mount");
            }
            if (!crowded && seat == 0) { await CheckSupportLifecycle(); await CheckSupportReaction(); }
            GD.Print("REGIONS_CELESTIALS_PASS: " + _captureMode); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task CheckSupportReaction()
    {
        var state = SupportFixture(battle: true).State;
        state.Cards[state.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218";
        var castle = SupportFixtureCard(state, "training-castle", 0, Zone.Home);
        var enemy = state.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var region = SupportFixtureCard(state, 9865, 1, Zone.Region, 0);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        var result = engine.Execute(new GameCommand("useAbility", 0, engine.State.Version, CardId: state.Players[0].StrongholdId, PerformerId: castle.Id, TargetId: enemy.Id, AbilityId: "carpenter-castle-siege"));
        SupportRequire(result.Accepted && engine.State.PendingAttack?.Strength == 5, "Reaction fixture did not open the real attack window");
        if (engine.State.PriorityPlayerId != 1)
        {
            var pass = engine.QueryActions(engine.State.PriorityPlayerId).Single(a => a.Type == "declineReaction");
            SupportRequire(engine.Execute(pass.Command!).Accepted, "Attacker reaction pass failed");
        }
        await LoadSupportFixture(engine, 1);
        await SupportClick(_tiles.Single(t => t.Card.Id == region.Id));
        var option = Actions.First(a => a.Enabled && a.CardId == region.Id && a.Command?.AbilityId == "fallow-lands-reaction");
        await SupportClick(StabilizationButton(ReactionOptionLabel(option))); // a reaction window labels its options "Use <card>" (ReactionOptionLabel), not the engine label
        SupportRequire(_chosen?.Command?.AbilityId == "fallow-lands-reaction", "Fallow Lands reaction did not open confirmation");
        await SupportCapture("-reaction-confirm"); await SupportClick(StabilizationButton("Confirm action")); await EventFrames(25);
        // The reaction window closes itself once nobody has a reaction left (with motion on, after its animation), so the probe proves the native click used the Region's reaction by the engine's own event.
        // The numeric -2 is not observable here any more: PendingAttack is gone once the window closes.
        bool FallowUsed() => View.Events.Any(e => e.Type == "abilityUsed" && e.Message.StartsWith("Lion: Fallow Lands"));
        for (var wait = 0; wait < 12 && !FallowUsed(); wait++) await EventFrames(10);
        SupportRequire(FallowUsed(), "Native Region reaction was not used through the reaction window");
        await SupportCapture("-reaction-used");
        GD.Print("SUPPORT_PASS: Fallow Lands native reaction used during a real attack window; the window closes and the attack resolves");
    }
    private async Task CheckSupportLifecycle()
    {
        var state = SupportFixture().State; state.Phase = Phase.Dynasty; state.Players[0].DynastyActionTaken = true; // the probe clicks End Dynasty directly; the confirmation has its own probe (dynasty-flow)
        var oldCelestial = state.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Celestial).Id;
        var incomingRegion = SupportFixtureCard(state, 4777, 1, Zone.Province, 0);
        var incomingCelestial = SupportFixtureCard(state, 4658, 1, Zone.Province, 3);
        var engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        var end = Actions.Single(a => a.Type == "endDynasty" && a.Enabled);
        await SupportClick(StabilizationButtons(_root).First(b => b.IsVisibleInTree() && b.Text == end.Label)); await EventFrames(100);
        SupportRequire(engine.State.Cards[incomingRegion.Id].Zone == Zone.Region, "Revealed Region did not attach");
        SupportRequire(engine.State.Cards[incomingCelestial.Id].Zone == Zone.Celestial && engine.State.Cards[oldCelestial].Zone == Zone.DynastyDiscard, "Celestial replacement incorrect");
        SupportRequire(_tiles.Count(t => t.Card.Zone == Zone.Celestial && t.Card.OwnerId == 1) == 1 && _tiles.All(t => t.Card.Id != oldCelestial), "Replacement left stale Celestial frame");
        ResetTableCamera(); CheckSupportLayout(); await SupportCapture("-revealed");
        GD.Print("SUPPORT_PASS: actual Events entry/refill, one Celestial per owner, replacement removes old mount");

        state = SupportFixture(battle: true).State;
        foreach (var c in state.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield)) c.Zone = Zone.Home;
        // Prepared unopposed army with enough force to destroy Fallow Lands' province.
        foreach (var c in state.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield)) c.Modifiers.Add(new("native-fixture", "native-fixture", "Force", 5));
        var doomed = SupportFixtureCard(state, 9865, 1, Zone.Region, 0);
        engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        for (var i = 0; i < 8 && !engine.State.Players[1].Provinces[0].Destroyed; i++)
        {
            var actor = engine.State.PriorityPlayerId; var action = engine.QueryActions(actor).First(a => a.Enabled && a.Type == "pass");
            await _session.Submit(action.Command!); await EventFrames(70);
        }
        SupportRequire(engine.State.Players[1].Provinces[0].Destroyed && engine.State.Cards[doomed.Id].Zone == Zone.DynastyDiscard, "Province destruction did not discard its Region");
        for (var wait = 0; wait < 40 && _tiles.Any(t => t.Card.Id == doomed.Id); wait++) await EventFrames(20); // the removal animation is time-based, so the frames it needs depend on the frame rate
        SupportRequire(_tiles.All(t => t.Card.Id != doomed.Id), "Destroyed province retained its Region tile");
        ResetTableCamera(); await SupportCapture("-province-fallen");
        GD.Print("SUPPORT_PASS: real battle resolution removes destroyed province and its Region");

        state = SupportFixture().State;
        foreach (var c in state.Cards.Values.Where(c => c.Zone is Zone.Region or Zone.Celestial)) c.Zone = Zone.DynastyDiscard;
        foreach (var player in state.Players) foreach (var province in player.Provinces) province.RegionId = null;
        engine = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(engine, 0);
        SupportRequire(_tiles.All(t => t.Card.Zone is not (Zone.Region or Zone.Celestial)) && _matTargets.All(t => !t.RegionPair), "Empty support state retains placeholder or mount");
        await SupportCapture("-empty"); GD.Print("SUPPORT_PASS: empty support zones have no permanent placeholder");
    }

}
