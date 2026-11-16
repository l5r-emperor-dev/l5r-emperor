using Godot;
using L5R.Core;
using System.Reflection;

namespace L5R.Client;

public partial class Main
{
    private bool _lockCaptureStarted, _lockReviewBusy;
    private MatchEngine _lockFixture = null!;
    private string _lockHero = "", _lockItem = "";
    private Button[] _lockReviewButtons = [];
    private static string StabilizationText(Node root) => string.Join("\n", root.FindChildren("*", "Label", true, false).OfType<Label>().Select(label => label.Text));
    private void ProcessActionLockCapture(double delta)
    {
        _captureClock += delta; if (_lockCaptureStarted || _captureClock < 1.5) return;
        _lockCaptureStarted = true; _ = CaptureActionLock();
    }
    private async Task PrepareLockReview(bool crowded = false, bool bowed = false)
    {
        var state = MatchEngine.CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        var hotako = SupportFixtureCard(state, 1726, 1, Zone.Home);
        var hero = SupportFixtureCard(state, 10292, 0, Zone.Home, bowed: bowed); _lockHero = hero.Id;
        var item = SupportFixtureCard(state, 10118, 0, Zone.Attached); item.AttachedTo = hero.Id; _lockItem = item.Id;
        if (crowded)
        {
            hero.Dishonorable = !_captureMode.Contains("spent") || bowed;
            var follower = CardCatalog.Cards.Values.First(d => !d.ScenarioOnly && d.Type == CardType.Follower);
            SupportFixtureCard(state, follower.Id, 0, Zone.Attached, bowed: true).AttachedTo = hero.Id;
            for (var i = 0; i < 6; i++) SupportFixtureCard(state, _captureMode.Contains("spent") ? 8590 : 1645, i % 2, Zone.Home, bowed: i % 3 == 0);
        }
        if (_captureMode.Contains("restriction"))
        {
            foreach (var unit in state.Cards.Values.Where(c => c.Zone == Zone.Attached || c.Zone == Zone.Home && CardCatalog.Cards[c.DefinitionId].Type == CardType.Personality))
            {
                unit.Tokens = new[] { "Fire", "Poison", "Sake", "Training", "Blood", "Madness" }.Select((type, i) => new CardToken("layout-" + unit.Id + i, type, "layout-fixture")).ToList();
                unit.MovementLocks.Add(new("Layout fixture", state.TurnNumber, MovementKinds.ToBattlefield));
                unit.StraightenLockedTurn = state.TurnNumber;
                unit.ForcePenaltiesNegatedTurn = state.TurnNumber;
                unit.CreatedCopy = true;
                unit.GrantedKeywords.Add(new("Berserker", "layout-fixture", "Permanent"));
            }
        }
        if (_captureMode.Contains("spent"))
        {
            var region = CardCatalog.Cards.Values.First(d => d.Type == CardType.Region && d.Abilities is { Length: > 0 } ids && ids.Any(id => AbilityCatalog.All.ContainsKey(id) && !TraitReactions.Is(id)));
            SupportFixtureCard(state, region.Id, 0, Zone.Region, 0);
            foreach (var unit in state.Cards.Values.Where(c => c.Zone is Zone.Home or Zone.Attached))
                if (CardCatalog.Cards[unit.DefinitionId].Abilities is { Length: > 0 } ids && AbilityCatalog.All.TryGetValue(ids[0], out var used))
                    unit.AbilityUses[used.UsageKey ?? ids[0]] = state.TurnNumber;
            // Keep Hotako usable: his real action below establishes the restriction and spends his use.
            hotako.AbilityUses.Clear();
        }
        _lockFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_lockFixture, 0);
        if (!bowed && !_captureMode.Contains("spent")) SupportRequire(Actions.Any(a => a.CardId == _lockHero && a.Enabled && a.Command?.AbilityId == "shizuka-search"), "Published action missing before lock");
        await _session.Submit(Actions.Single(a => a.Enabled && a.Type == "pass").Command!); await WaitDecisionPresentation();
        var ability = _lockFixture.QueryActions(1).Single(a => a.Enabled && a.Command?.AbilityId == "hotako-fear-lock" && a.CardId == hotako.Id);
        await _session.Submit(ability.Command! with { TargetId = hero.Id }); await WaitDecisionPresentation();
        ClearCardPlays(); CloseCardShow(); SelectCard(FindCard(_lockHero)!); HideTableTooltip(); ReleaseTableFocus();
        if (_captureMode.Contains("review")) BuildLockReviewControls();
        await EventFrames(12);
    }
    private void BuildLockReviewControls()
    {
        if (_root.GetNodeOrNull<Control>("LockReviewControls") is { } old) { _root.RemoveChild(old); old.QueueFree(); }
        var row = new HBoxContainer { Name = "LockReviewControls" }; row.AddThemeConstantOverride("separation", 6);
        var inspect = SidebarSkin.Button("Inspect attachment", () =>
        {
            if (FindCard(_lockItem) is { } attachment) SelectCard(attachment);
            else SetNotice("That attachment has left the table. Restart demo to prepare it again.");
        });
        var end = SidebarSkin.Button("Advance turn", () => _ = RunLockReview(AdvanceLockTurn));
        var restart = SidebarSkin.Button("Restart demo", () => _ = RunLockReview(() => PrepareLockReview(crowded: _captureMode.Contains("crowded"), bowed: _captureMode.Contains("bowed"))));
        var restore = SidebarSkin.Button("Restore action use", () => _ = RunLockReview(RestorePrintedUse));
        _lockReviewButtons = _captureMode.Contains("spent") ? [inspect, restore, end, restart] : [inspect, end, restart];
        foreach (var button in _lockReviewButtons) { button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; button.CustomMinimumSize = new(0, 44); row.AddChild(button); }
        Place(_root, row, 350, 114, 830, 50);
    }
    private async Task RunLockReview(Func<Task> action)
    {
        if (_lockReviewBusy) return; _lockReviewBusy = true; foreach (var b in _lockReviewButtons) b.Disabled = true;
        try { await action(); }
        catch (Exception e) { SetNotice("The demo has changed. Use Restart demo to prepare it again."); GD.PushWarning(e.Message); }
        finally { _lockReviewBusy = false; foreach (var b in _lockReviewButtons) if (IsInstanceValid(b)) b.Disabled = false; }
    }
    private async Task AdvanceLockTurn()
    {
        if (!FindCard(_lockHero)!.ActionLocked) { SetNotice("The restriction has expired. Restart demo to see it again."); return; }
        var turn = _lockFixture.State.TurnNumber;
        for (var step = 0; step < 32 && _lockFixture.State.TurnNumber == turn; step++)
        {
            var actor = _lockFixture.State.PendingChoice?.PlayerId ?? _lockFixture.State.PriorityPlayerId;
            var next = _lockFixture.QueryActions(actor).First(a => a.Enabled && a.Command is not null && a.Type is "pass" or "endDynasty" or "declineAttack" or "declineReaction");
            await _session.Submit(next.Command!); await WaitDecisionPresentation();
        }
        SupportRequire(_lockFixture.State.TurnNumber > turn, "Demo did not advance the turn");
        SelectCard(FindCard(_lockHero)!); SetNotice("A new turn has begun. The action restriction has expired.");
    }
    private async Task RestorePrintedUse()
    {
        // Labelled review fixture: apply the existing engine additional-use mark; no client legality.
        var state = _lockFixture.State;
        state.Cards[_lockHero].AbilityUses["fires-of-war:second-use"] = state.TurnNumber;
        _lockFixture = (MatchEngine)typeof(MatchEngine).GetMethod("FromTestState", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state])!;
        await LoadSupportFixture(_lockFixture, View.ViewerId);
        SelectCard(FindCard(_lockHero)!);
        if (_captureMode.Contains("review")) BuildLockReviewControls();
        SetNotice("Fixture: an extra use is granted this turn. UNUSED does not bypass the action restriction.");
        await EventFrames(15);
    }
    private void AssertPrintedActionCrowding()
    {
        static float Overlap(Vector2[] a, Vector2[] b) => Geometry2D.IntersectPolygons(a, b).Sum(p => Math.Abs(Enumerable.Range(0, p.Length).Sum(i => p[i].Cross(p[(i + 1) % p.Length]))) / 2);
        var strips = _tiles.Where(t => !t.UprightPreview && t.ActionStrip is not null).Select(t => t.ActionStrip!).ToArray();
        SupportRequire(strips.Length > 0 && strips.Any(s => s.CardAnchor.Card.PrintedActions!.Any(a => a.Spent)), "Used action strips missing from native table");
        foreach (var strip in strips)
        {
            var polygon = strip.CornersOnViewport();
            // Expanded units are on an opaque, flat inspection panel. Do not compare
            // their screen polygons with the table visible behind that panel.
            var flat = strip.CardAnchor.ProjectToScreen is null;
            foreach (var tile in _tiles.Where(t => !t.UprightPreview && (t.ProjectToScreen is null) == flat && (!flat || t.GetParent() == strip.CardAnchor.GetParent())))
            {
                SupportRequire(Overlap(polygon, tile.FaceCornersOnViewport()) < .25f, $"Action strip {strip.CardAnchor.Card.Id} covers card face {tile.Card.Id}");
                if (tile.TokenRail is { } rail) SupportRequire(Overlap(polygon, rail.CornersOnViewport()) < .25f, "Action strip covers token/restriction rail");
            }
            foreach (var standard in Standards(_board).Where(s => s.IsVisibleInTree() && (s.CardAnchor.ProjectToScreen is null) == flat && (!flat || s.CardAnchor.GetParent() == strip.CardAnchor.GetParent())))
                SupportRequire(Overlap(polygon, standard.BodyCornersOnViewport()) < .25f, "Action strip covers Unit Force standard");
            foreach (var other in strips.Where(s => s != strip && (s.CardAnchor.ProjectToScreen is null) == flat && (!flat || s.CardAnchor.GetParent() == strip.CardAnchor.GetParent()))) SupportRequire(Overlap(polygon, other.CornersOnViewport()) < .25f, "Action strips overlap each other");
        }
    }
    private async Task CapturePrintedUnitCloseup(string suffix)
    {
        var peers = _tiles.Where(t => t.Card.Id == _lockHero || t.Card.AttachedTo == _lockHero).ToArray();
        var points = peers.SelectMany(t => t.FaceCornersOnViewport().Concat(t.TokenRail?.CornersOnViewport() ?? []).Concat(t.ActionStrip?.CornersOnViewport() ?? [])).Select(ScreenToWorld).ToArray();
        var bounds = new Rect2(new Vector2(points.Min(p => p.X), points.Min(p => p.Y)), new Vector2(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y))).Grow(65);
        _cameraFitted = false; _cameraCenter = bounds.GetCenter(); _cameraZoom = Math.Min(2, FitRegion(bounds) * .8f); ApplyTableCamera();
        await EventFrames(20); await SupportCapture(suffix); ResetTableCamera(); await EventFrames(12);
    }
    private async Task ClickLockSeal(CardTile tile)
    {
        var id = tile.Card.Id; DeselectCard(); await EventFrames(10);
        tile = _tiles.Single(t => t.Card.Id == id);
        var point = tile.TokenRail?.Restrictions is { } track ? (tile.ProjectToScreen?.Invoke(track.GetGlobalTransform() * track.Body.GetCenter()) ?? track.GetGlobalTransform() * track.Body.GetCenter()) : tile.RestrictionSeal!.CenterOnViewport(); PushCaptureMouse(new InputEventMouseMotion { Position = point, GlobalPosition = point }); await EventFrames(8);
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true });
        PushCaptureMouse(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(12);
    }
    private void AssertRestrictionCrowding()
    {
        static float Overlap(Vector2[] a, Vector2[] b) => Geometry2D.IntersectPolygons(a, b).Sum(p =>
            Math.Abs(Enumerable.Range(0, p.Length).Sum(i => p[i].Cross(p[(i + 1) % p.Length]))) / 2);
        var rails = _tiles.Where(t => !t.UprightPreview && t.TokenRail is not null).Select(t => t.TokenRail!).ToArray();
        foreach (var rail in rails)
        {
            var polygon = rail.CornersOnViewport();
            SupportRequire(polygon.All(p => GetViewport().GetVisibleRect().HasPoint(p)), "Crowded restriction rail leaves window");
            foreach (var other in rails.Where(other => other != rail))
                SupportRequire(Overlap(polygon, other.CornersOnViewport()) < .25f, $"Crowded rails overlap: {rail.CardAnchor.Card.Id}, {other.CardAnchor.Card.Id}");
            foreach (var face in _tiles.Where(t => !t.UprightPreview && t.Card.AttachedTo is null))
                SupportRequire(Overlap(polygon, face.FaceCornersOnViewport()) < .25f, $"Crowded rail covers card face: {rail.CardAnchor.Card.Id}, {face.Card.Id}");
        }
    }
    private async Task CaptureActionLock()
    {
        try
        {
            _preferences.ReducedMotion = _captureMode.Contains("reduced"); ApplyPreferences(false);
            await PrepareLockReview(crowded: _captureMode.Contains("crowded"), bowed: _captureMode.Contains("bowed"));
            if (_captureMode.Contains("review"))
            {
                if (_captureMode.Contains("opponent")) { _session.SwitchPracticeSeat(); await EventFrames(20); DeselectCard(); SelectCard(FindCard(_lockHero)!); }
                GD.Print("ACTION_LOCK_REVIEW_READY: Hotako has restricted Shizuka; inspect attachment or advance turn."); return;
            }
            var hero = _tiles.Single(t => t.Card.Id == _lockHero); var version = View.Version;
            SupportRequire((hero.TokenRail?.Restrictions is not null || hero.RestrictionSeal is not null) && FindCard(_lockItem)!.ActionLocked, "Public lock not presented on Personality or attachment");
            await ClickLockSeal(hero);
            if (_captureMode.Contains("spent"))
            {
                DeselectCard(); GetWindow().GrabFocus(); await EventFrames(10);
                var strip = _tiles.Single(t => t.Card.Id == _lockHero).ActionStrip!;
                var at = strip.CornersOnViewport().Aggregate(Vector2.Zero, (sum, p) => sum + p) / 4;
                PushCaptureMouse(new InputEventMouseMotion { Position = at, GlobalPosition = at }); await EventFrames(12);
                at = strip.CornersOnViewport().Aggregate(Vector2.Zero, (sum, p) => sum + p) / 4;
                GD.Print($"PRINTED_STRIP_INPUT: {at}, hover={TableHovered?.GetType().Name}, rect={strip.GetGlobalRect()}, size={strip.Size}, filter={strip.MouseFilter}, blocked={CameraBlocked}, focus={GetWindow().HasFocus()}, expanded={_expandedUnits.Count}, windows={GetChildren().OfType<Window>().Count(w => w.Visible)}");
                PushCaptureMouse(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true });
                PushCaptureMouse(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }); await EventFrames(12);
                SupportRequire(_selected?.Id == _lockHero && View.Version == version && StabilizationText(_inspector).Contains("PRINTED ACTIONS"), $"Action strip input must only inspect its owner: selected={_selected?.Id}, version={version}->{View.Version}, hover={TableHovered?.GetType().Name}");
            }
            SupportRequire(_selected?.Id == _lockHero && View.Version == version, $"Seal click must only inspect the locked card: selected={_selected?.Id}, expected={_lockHero}, version={version}->{View.Version}, hovered={TableHovered?.GetType().Name}");
            SupportRequire(StabilizationText(_inspector).Contains("perform actions"), "Inspector restriction missing");
            var blocked = Actions.Single(a => a.CardId == _lockHero && a.Type == "useAbility");
            SupportRequire(!blocked.Enabled && (blocked.Reason!.Contains("may not perform actions") || _captureMode.Contains("spent") && blocked.Reason.Contains("used within its limit")), "Engine must keep the action disabled");
            // Current main deliberately hides disabled ability buttons (Listable, #1202).
            // The engine refusal and the restriction explanation remain independently checked.
            if (_captureMode.Contains("spent")) AssertPrintedActionCrowding();
            await SupportCapture("-table");
            ClearCardPlays(); CloseCardShow(); DeselectCard(); hero = _tiles.Single(t => t.Card.Id == _lockHero); var pointer = _tableSurface.ProjectCanvas(hero.FaceCornersInCanvas().Aggregate(Vector2.Zero, (sum, p) => sum + p) / 4); PushCaptureMouse(new InputEventMouseMotion { Position = pointer, GlobalPosition = pointer });
            for (var frame = 0; frame < 240 && _tableTooltip is null; frame += 6) await EventFrames(6);
            SupportRequire(_tableTooltip is not null && StabilizationText(_tableTooltip).Contains("perform actions"), $"Full-card hover did not explain the restriction: hovered={TableHovered?.GetType().Name}, candidate={_tableHover?.GetType().Name}, blocked={CameraBlocked}, focus={GetWindow().HasFocus()}, pointer={pointer}, last={_lastTableMouse}, time={_tableHoverTime}, tooltip={(_tableTooltip is null ? "none" : StabilizationText(_tableTooltip))}");
            if (_captureMode.Contains("spent"))
            {
                var previews = _tableTooltip!.FindChildren("*", "", true, false).OfType<CardTile>().Where(t => t.ActionStrip is not null).ToArray();
                SupportRequire(previews.Length > 0 && previews.All(t => _tableTooltip.GetGlobalRect().Encloses(t.ActionStrip!.GetGlobalRect())), "Action strip escapes enlarged preview");
            }
            SupportRequire(GetViewport().GetVisibleRect().Encloses(_tableTooltip!.GetGlobalRect()), "Restricted-card hover leaves the window"); await SaveShownCapture("-hover"); HideTableTooltip();
            await ClickLockSeal(hero);
            _expandedUnits.Add(_lockHero); RenderBoard(); await EventFrames(15);
            hero = _tiles.Single(t => t.Card.Id == _lockHero); await ClickLockSeal(hero);
            await SupportCapture("-detail");
            await SupportClick(_tiles.Single(t => t.Card.Id == _lockItem));
            SupportRequire(StabilizationText(_inspector).Contains("perform actions"), "Attachment inspection lost the public restriction"); await SupportCapture("-attachment");
            _session.SwitchPracticeSeat(); await EventFrames(20);
            hero = _tiles.Single(t => t.Card.Id == _lockHero); SupportRequire((hero.TokenRail?.Restrictions is not null || hero.RestrictionSeal is not null) && hero.Card.ActionLocked, "Other seat lost public seal");
            await ClickLockSeal(hero); SupportRequire(_selected?.Id == _lockHero, "Other seat cannot inspect restriction"); await SupportCapture("-opponent");
            _session.SwitchPracticeSeat(); await EventFrames(12);
            if (_captureMode.Contains("spent"))
            {
                var sameTurn = _lockFixture.State.TurnNumber;
                await RestorePrintedUse();
                SupportRequire(_lockFixture.State.TurnNumber == sameTurn && FindCard(_lockHero)!.PrintedActions!.All(a => !a.Spent) && FindCard(_lockHero)!.ActionLocked, "Same-turn extra use must clear USED while retaining the action restriction");
                await SupportCapture("-restored-use");
            }
            _expandedUnits.Clear(); RenderBoard(); await AdvanceLockTurn(); await EventFrames(20);
            SupportRequire(!FindCard(_lockHero)!.ActionLocked && !FindCard(_lockItem)!.ActionLocked && _tiles.Single(t => t.Card.Id == _lockHero).RestrictionSeal is null && !StabilizationText(_inspector).Contains("perform actions"), "Expired restriction remained visible"); if (_captureMode.Contains("spent")) SupportRequire(FindCard(_lockHero)!.PrintedActions!.All(a => !a.Spent), "Turn expiry left the used strip behind");
            await SupportCapture("-expired");
            await PrepareLockReview(crowded: true); AssertRestrictionCrowding(); if (_captureMode.Contains("spent")) AssertPrintedActionCrowding(); await SupportCapture("-crowded-upright"); if (_captureMode.Contains("spent")) await CapturePrintedUnitCloseup("-upright-closeup");
            await PrepareLockReview(crowded: true, bowed: true); AssertRestrictionCrowding(); if (_captureMode.Contains("spent")) AssertPrintedActionCrowding(); await SupportCapture("-crowded-bowed"); if (_captureMode.Contains("spent")) await CapturePrintedUnitCloseup("-bowed-closeup");
            _expandedUnits.Add(_lockHero); RenderBoard(); await EventFrames(15);
            if (_captureMode.Contains("spent")) AssertPrintedActionCrowding();
            await SupportCapture("-bowed-detail");
            SupportRequire(_tiles.Where(t => ActionLockStatus.VisibleOn(t.Card) && !t.UprightPreview).All(t => t.TokenRail?.Restrictions is not null || t.RestrictionSeal is not null), "Bowed or crowded Personality lost its marker");
            GD.Print("ACTION_LOCK_PASS: real Hotako lock, native seal input, disabled action, both seats, attachments, expiry, bowed/dishonorable crowded rendering"); GetTree().Quit();
        }
        catch (Exception e) { await SupportCapture("-failure"); GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
