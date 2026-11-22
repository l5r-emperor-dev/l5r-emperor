using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private const float MatPadding = 22, MatHeader = InkMatSections.Header, MatGap = 24;
    private InkMatSections _matSections = null!;
    private float _playLeft, _playWidth;
    private int _matMatchEpoch;
    private float _enemyProvinceY, _reservedBattleHeight, _reservedHoldingWidth, _regionLayoutExtent;
    private readonly Dictionary<int, float> _reservedHomeBands = [];
    private readonly float[] _reservedLaneWidths = new float[4];
    // Additional battlefields (Emperor Edition rulings pass R1, issue #492: a battle without a province, or a second one at a province) get a lane of their own beyond the piles while they exist; the set
    // seen during a battle is kept until it is over so the layout stays still while its resolution is presented.
    private readonly SortedSet<int> _reservedExtraLanes = [];
    private int[] _extraLanes = [];

    /// <summary>What a battlefield is called: a province by its number, an additional one by the province it is at (if any).</summary>
    private string BattlefieldName(int battlefield)
    {
        var name = BattlefieldPresentation.Name(View, battlefield);
        return BattlefieldProvince(battlefield) is null && _openBattlefieldOrdinals.Count > 1 &&
            _openBattlefieldOrdinals.TryGetValue(battlefield, out var ordinal)
            ? name + " " + BattlefieldPresentation.Letter(ordinal) : name;
    }

    private static bool IsAdditionalBattlefield(int battlefield) => battlefield >= MatchEngine.FirstAdditionalBattlefield;

    // The slot a battlefield stands in: a province's own lane, or the lane of an additional battlefield.
    private int LaneSlot(int battlefield) => IsAdditionalBattlefield(battlefield) ? 8 + Math.Max(0, Array.IndexOf(_extraLanes, battlefield)) : 2 + battlefield;

    // Every additional battlefield the table has to show: one the engine lists, the current one, or one with units at it.
    private IEnumerable<int> ExtraBattlefieldIndices() => (View.Battlefields ?? []).Select(b => b.Index)
        .Concat(View.UnresolvedBattlefields)
        .Concat(View.CurrentBattleProvince is int current ? [current] : [])
        .Concat(View.Players.SelectMany(p => p.Army).Where(c => c.AttachedTo is null && c.ProvinceIndex is not null).Select(c => c.ProvinceIndex!.Value))
        .Where(IsAdditionalBattlefield).Distinct();

    // A resolved primary can still hold survivors when another front destroys its province.
    // Keep every public or occupied primary court visible; older views can omit battlefield metadata.
    private IEnumerable<int> CourtIndices(PlayerView defender) => defender.Provinces.Where(p => !p.Destroyed ||
        View.CurrentBattleProvince == p.Index || View.UnresolvedBattlefields.Contains(p.Index) ||
        (View.Battlefields ?? []).Any(b => b.Index == p.Index) ||
        View.Players.Any(player => player.Army.Any(c => c.AttachedTo is null && c.ProvinceIndex == p.Index)))
        .Select(p => p.Index).Concat(_extraLanes);
    private readonly Dictionary<int, Rect2> _battleCourts = [];
    private readonly Dictionary<string, Vector2> _battlePlacements = [];
    private IEnumerable<CardView> CourtUnits(PlayerView player, int province) => player.Army
        .Where(c => c.AttachedTo is null && c.ProvinceIndex == province)
        .Concat(player.Home.Where(c => c.AttachedTo is null && _assignments.GetValueOrDefault(c.Id, -1) == province))
        .Concat(MovementCourtUnits(player, province));
    private readonly Dictionary<int, CardView[][]> _holdingRows = [];
    private readonly Dictionary<int, Rect2> _holdingBounds = [], _homeBounds = [];
    private readonly List<InkMatSection> _matTargets = [];

    private CardView[][] PackMatRows(CardView[] cards, PlayerView player, float available, int maxColumns = int.MaxValue)
    {
        var rows = new List<CardView[]>(); var row = new List<CardView>(); var width = 0f;
        foreach (var card in cards)
        {
            var next = UnitWidth(card, player);
            if (row.Count > 0 && (width + next > available || row.Count >= maxColumns))
            { rows.Add(row.ToArray()); row.Clear(); width = 0; }
            row.Add(card); width += next + CardGap;
        }
        if (row.Count > 0) rows.Add(row.ToArray());
        return rows.ToArray();
    }

    private float MatRowsWidth(CardView[][] rows, PlayerView player) => rows
        .Select(r => r.Sum(c => UnitWidth(c, player)) + Math.Max(0, r.Length - 1) * CardGap).DefaultIfEmpty(220).Max();
    private float MatRowsHeight(CardView[][] rows, PlayerView player) => rows
        .Sum(r => r.Max(c => StackHeight(c, player)) + RowGap) + MatHeader + MatPadding;

    private void MeasureTableLayout()
    {
        ObserveBattlefieldLocations();
        var oldSize = new Vector2(WorldWidth, _worldHeight);
        var oldRegionExtent = _regionLayoutExtent;
        _regionLayoutExtent = View.Players.SelectMany(p => p.Provinces).Sum(ProvinceCardWidth);
        var oldPersistentExtent = new Vector3(_persistentWidth, _persistentHeights.GetValueOrDefault(0), _persistentHeights.GetValueOrDefault(1));
        if (!_tableLayoutSeen)
        {
            _matMatchEpoch++; _reservedBattleHeight = 0; _reservedHoldingWidth = 0; _reservedHomeBands.Clear(); Array.Clear(_reservedLaneWidths); _reservedExtraLanes.Clear();
            _fallTimes.Clear(); _framedBattle = null;
        }
        // Battle-only reservations keep a resolution stable, but must not
        // permanently inflate the next Action/Dynasty board.
        if (!BattleActive && !PresentationBusy)
        { _reservedBattleHeight = 0; _reservedHoldingWidth = 0; _reservedHomeBands.Clear(); Array.Clear(_reservedLaneWidths); _reservedExtraLanes.Clear(); }
        _matTargets.Clear(); _battleCourts.Clear(); _battlePlacements.Clear(); _movementPlacements.Clear();
        MeasurePersistentHome();
        foreach (var extra in ExtraBattlefieldIndices()) _reservedExtraLanes.Add(extra);
        _extraLanes = _reservedExtraLanes.ToArray();
        if (_slotCenters.Length < 8 + _extraLanes.Length) Array.Resize(ref _slotCenters, 8 + _extraLanes.Length);
        for (var i = 0; i < 4; i++)
        {
            var unitWidth = View.Players.SelectMany(p => CourtUnits(p, i)
                .Select(c => UnitWidth(c, p))).DefaultIfEmpty(154).Max();
            _laneWidths[i] = _reservedLaneWidths[i] = Math.Max(_reservedLaneWidths[i],
                Math.Max(View.Players.Max(p => ProvinceContentWidth(p.Provinces.Single(v => v.Index == i))) + 8,
                    ArmyColumns(i) * (unitWidth + CardGap) + MatPadding * 2 + 10));
        }
        // This inner play area mirrors by seat. Holdings deliberately remain on
        // screen-left for both owners and never enter this mirrored slot system.
        var x = 0f;
        _slotCenters[0] = x + 80; x += 160;
        _slotCenters[1] = x + 80; x += 160;
        for (var i = 0; i < 4; i++) { _slotCenters[2 + i] = x + _laneWidths[i] / 2; x += _laneWidths[i]; }
        _slotCenters[6] = x + 80; x += 160;
        _slotCenters[7] = x + 80; x += 160;
        // The lanes of additional battlefields lie beyond the piles, so nothing that has a fixed slot moves for them.
        for (var k = 0; k < _extraLanes.Length; k++)
        {
            var extra = _extraLanes[k];
            var extraUnitWidth = View.Players.SelectMany(p => CourtUnits(p, extra).Select(c => UnitWidth(c, p))).DefaultIfEmpty(154).Max();
            var extraWidth = Math.Max(300, ArmyColumns(extra) * (extraUnitWidth + CardGap) + MatPadding * 2 + 10);
            _slotCenters[8 + k] = x + extraWidth / 2; x += extraWidth;
        }
        _playWidth = x;

        var holdingWidth = 0f;
        foreach (var player in View.Players)
        {
            _holdingRows[player.Id] = PackMatRows(HoldingCards(player), player, 680, HoldingCards(player).Length > 4 ? 3 : 2);
            holdingWidth = Math.Max(holdingWidth, MatRowsWidth(_holdingRows[player.Id], player) + 2 * MatPadding);
            _homeRows[player.Id] = PackMatRows(HomeCards(player), player, _playWidth - 2 * MatPadding);
            _homeHeights[player.Id] = Math.Max(118, MatRowsHeight(_homeRows[player.Id], player));
            if (BattleActive || PresentationBusy)
            {
                var returning = player.Home.Concat(player.Army).Where(c => c.AttachedTo is null && c.Definition?.Type != CardType.Holding && !IsPersistentHomeCard(c))
                    .Select(c => c with { Bowed = true }).ToArray();
                _homeHeights[player.Id] = _reservedHomeBands[player.Id] = Math.Max(_reservedHomeBands.GetValueOrDefault(player.Id),
                    Math.Max(_homeHeights[player.Id], MatRowsHeight(PackMatRows(returning, player, _playWidth - 2 * MatPadding), player)));
            }
            _armyHeights[player.Id] = Math.Max(0, Enumerable.Range(0, 4).Concat(_extraLanes).Select(province => CourtUnits(player, province).Chunk(ArmyColumns(province)).Sum(r => r.Max(c => StackHeight(c, player)) + RowGap)).DefaultIfEmpty(0).Max());
        }
        holdingWidth = _reservedHoldingWidth = Math.Max(holdingWidth, _reservedHoldingWidth);
        _playLeft = MatPadding + holdingWidth + MatGap;
        WorldWidth = _playLeft + _playWidth + MatPadding + (_persistentWidth > 0 ? MatGap + _persistentWidth : 0);
        var enemy = View.Players.Single(p => p.Id != View.ViewerId);
        var provinceActionHeight = View.Players.SelectMany(p => p.Provinces).Select(ProvinceActionsHeight).DefaultIfEmpty(0).Max();
        var provinceHeight = MatHeader + PlaneHeight(CardHeight) + ProvinceThresholdArt.Footer + MatPadding + provinceActionHeight;
        // Restore the original table-facing Home rows and compact province
        // spacing. The defending court touches its province through B's neck;
        // Home remains outside that court, on the table-facing side as well.
        _enemyProvinceY = MatPadding + MatHeader;
        var connectedCourt = BattleActive && (View.CurrentBattleProvince is not null || View.UnresolvedBattlefields.Length > 0 || View.Players.Any(p => Enumerable.Range(0, 4).Concat(_extraLanes).Any(i => CourtUnits(p, i).Any())));
        var occupied = Enumerable.Range(0, 4).Concat(_extraLanes).Select(i => View.Players.Sum(p => CourtUnits(p, i).Chunk(ArmyColumns(i))
            .Sum(r => r.Max(c => StackHeight(c, p)) + RowGap))).DefaultIfEmpty(0).Max();
        _battleHeight = connectedCourt ? Math.Max(_reservedBattleHeight, Math.Max(270, occupied + MatHeader + MatPadding + 38)) : _reservedBattleHeight;
        if (BattleActive) _reservedBattleHeight = _battleHeight;
        var enemyDefends = View.ActivePlayerId == Self.Id;
        // C's top stroke sits inside its bounds. Close that inset so the neck
        // meets pigment, not merely the invisible layout rectangle.
        var neckGap = enemyDefends ? BattleInkArt.NeckSpan : BattleInkArt.NeckSpan - 12;
        var courtBand = _battleHeight > 0 ? _battleHeight + neckGap : 0;
        _battleY = MatPadding + provinceHeight + neckGap;
        _enemyHomeY = MatPadding + provinceHeight + MatGap + (enemyDefends ? courtBand : 0);
        var enemyBandBottom = Math.Max(Math.Max(_enemyHomeY + _homeHeights[enemy.Id], MatPadding + MatRowsHeight(_holdingRows[enemy.Id], enemy)), MatPadding + _persistentHeights[enemy.Id]);
        _selfHomeY = enemyBandBottom + MatGap;
        if (!enemyDefends) _battleY = _selfHomeY + _homeHeights[Self.Id] + MatGap;
        _selfProvinceY = _selfHomeY + _homeHeights[Self.Id] + MatGap + MatHeader + (!enemyDefends ? courtBand : 0);
        _worldHeight = Math.Max(_selfProvinceY + PlaneHeight(CardHeight) + ProvinceThresholdArt.Footer + MatPadding + provinceActionHeight,
            _selfHomeY + MatRowsHeight(_holdingRows[Self.Id], Self)) + MatPadding;
        _worldHeight = Math.Max(_worldHeight, _selfHomeY + _persistentHeights[Self.Id] + MatPadding);

        foreach (var player in View.Players)
        {
            var homeY = player.Id == Self.Id ? _selfHomeY : _enemyHomeY;
            var width = Math.Min(_playWidth, MatRowsWidth(_homeRows[player.Id], player) + 2 * MatPadding);
            _homeBounds[player.Id] = new Rect2(_playLeft + (_playWidth - width) / 2, homeY, width, _homeHeights[player.Id]);
            _holdingBounds[player.Id] = new Rect2(MatPadding, player.Id == Self.Id ? _selfHomeY : MatPadding,
                holdingWidth, MatRowsHeight(_holdingRows[player.Id], player));
            _matTargets.Add(new($"holdings:{player.Id}", "Holdings", _holdingBounds[player.Id]));
            _matTargets.Add(new($"home:{player.Id}", "Home", _homeBounds[player.Id]));
            if (HasPersistentHome(player))
            {
                var persistent = new Rect2(_playLeft + _playWidth + MatGap, player.Id == Self.Id ? _selfHomeY : MatPadding,
                    _persistentWidth, _persistentHeights[player.Id]);
                _persistentBounds[player.Id] = persistent;
                _matTargets.Add(new($"persistent:{player.Id}", "Persistent Home", persistent));
            }
            // Each surviving province has its own fixed identity and C threshold.
            // Destroying one never shifts or renumbers its surviving neighbours.
            foreach (var province in player.Provinces.Where(p => !p.Destroyed))
            {
                var bayWidth = Math.Min(ProvinceContentWidth(province) + 8, _laneWidths[province.Index] - 12);
                _matTargets.Add(new($"province:{player.Id}:{province.Index}", $"Province {province.Index + 1}",
                    new Rect2(SlotX(player, province.Index + 2) + (CardHeight - bayWidth) / 2,
                        ProvinceRowY(player) - MatHeader, bayWidth, provinceHeight),
                    ProvinceNumber: province.Index + 1, RegionPair: false, ConnectionAbove: player.Id == Self.Id,
                    Connected: BattleActive && player.Id != View.ActivePlayerId &&
                        CourtConnectsToProvince(province.Index) &&
                        (View.CurrentBattleProvince == province.Index || View.Players.Any(p => CourtUnits(p, province.Index).Any()) || MovementDestination(player.Id, province.Index))));
            }
        }
        if (BattleActive)
        {
            var defender = View.Players.Single(p => p.Id != View.ActivePlayerId);
            foreach (var index in CourtIndices(defender).Distinct())
            {
                if (!View.Players.Any(p => CourtUnits(p, index).Any()) && View.CurrentBattleProvince != index &&
                    !MovementDestination(defender.Id, index) && !View.UnresolvedBattlefields.Contains(index)) continue;
                var widest = View.Players.SelectMany(p => CourtUnits(p, index).Select(c => UnitWidth(c, p))).DefaultIfEmpty(154).Max();
                var width = Math.Max(IsAdditionalBattlefield(index) ? 300 : 210,
                    ArmyColumns(index) * (widest + CardGap) + MatPadding * 2 + 10) - 16;
                var occupiedHeight = View.Players.Sum(p => CourtUnits(p, index).Chunk(ArmyColumns(index))
                    .Sum(r => r.Max(c => StackHeight(c, p)) + RowGap));
                var courtHeight = Math.Max(270, occupiedHeight + MatHeader + MatPadding + 38);
                var yCourt = defender.Id == Self.Id ? _battleY + _battleHeight - courtHeight : _battleY;
                var bounds = new Rect2(SlotX(defender, LaneSlot(index)) + CardHeight / 2 - width / 2, yCourt, width, courtHeight);
                _battleCourts[index] = bounds;
                var closed = !CourtConnectsToProvince(index);
                _matTargets.Add(new($"battle:{defender.Id}:{index}", BattlefieldName(index), bounds,
                    View.CurrentBattleProvince == index || MovementDestination(defender.Id, index), ConnectionAbove: closed || defender.Id != Self.Id, ClosedCourt: closed));
                // Top army belongs to the top seat, so the defending seat is
                // always closest to its own province, from either player's view.
                foreach (var player in new[] { enemy, Self })
                {
                    var rows = CourtUnits(player, index).Chunk(ArmyColumns(index)).ToArray();
                    var height = rows.Sum(r => r.Max(c => StackHeight(c, player)) + RowGap);
                    var y = player.Id == enemy.Id ? bounds.Position.Y + MatHeader : bounds.End.Y - MatPadding - height;
                    foreach (var row in rows)
                    {
                        var rowWidth = row.Sum(c => UnitWidth(c, player)) + (row.Length - 1) * CardGap;
                        var cx = bounds.GetCenter().X - rowWidth / 2;
                        foreach (var card in row)
                        {
                            if (MovementGhost(card, index)) _movementPlacements[card.Id] = new Vector2(cx, y);
                            else _battlePlacements[card.Id] = new Vector2(cx, y);
                            cx += UnitWidth(card, player) + CardGap;
                        }
                        y += row.Max(c => StackHeight(c, player)) + RowGap;
                    }
                }
            }
        }

        MeasureRemovedPiles();

        // Keep a player's camera steady while ink and cards change positions.
        // An overview stays fitted when the contextual Home area appears or grows,
        // so a newly gained Favor cannot appear behind the sidebar. A manually
        // positioned camera remains under player control. The tutorial never lets go of the fitted view: its table grows as the lesson brings cards in, and nobody has panned it.
        var persistentChanged = oldPersistentExtent.DistanceTo(new Vector3(_persistentWidth, _persistentHeights.GetValueOrDefault(0), _persistentHeights.GetValueOrDefault(1))) > 1;
        if (_tableLayoutSeen && _cameraFitted && !TutorialActive && !persistentChanged && Math.Abs(oldRegionExtent - _regionLayoutExtent) < 1 && oldSize.DistanceTo(new Vector2(WorldWidth, _worldHeight)) > 1) _cameraFitted = false;
        _tableLayoutSeen = true;
        _board.Size = new Vector2(WorldWidth, _worldHeight); _world.Size = _board.Size; _pileHud.Size = _board.Size; _battleDecor.Size = _board.Size;
    }

    private void SyncMatSections()
    {
        _matSections.Sync(_matTargets, _reducedMotion, $"{_matMatchEpoch}:{View.Scenario}:{View.ViewerId}:{_session.Snapshot?.RoomCode}");
        _battleDecor?.QueueRedraw();
    }
}
