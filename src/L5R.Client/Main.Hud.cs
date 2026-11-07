using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private readonly Dictionary<int, ClanPlaque> _clanPlaques = [];
    private readonly Dictionary<int, string> _plaqueKeys = [];

    /// <summary>What one player's plaque shows, plus a key of everything it is built from. A plaque is about 40 nodes, mostly text, and every command rebuilt both, so a plaque whose key has not changed is kept.</summary>
    private (int Production, string Turn, string Action, bool Acting, bool TurnOwner, string Key) PlaqueFacts(PlayerView player)
    {
        var production = player.Home.Concat(player.Army).Append(player.Stronghold).Where(c => !c.Bowed).Sum(c => Production(c, player));
        var own = player.Id == View.ViewerId;
        var turn = View.WinnerId is int winner ? (winner == player.Id ? "VICTORY" : "MATCH ENDED") :
            EventDecisionActive && View.PendingChoice!.PlayerId == player.Id ? (own ? "YOUR DECISION" : "OPPONENT DECIDING") :
            (View.ActionPhaseTurnPlayerId ?? View.ActivePlayerId) == player.Id ? (own ? "YOUR TURN" : "OPPONENT'S TURN") : "";
        var action = own ? player.Name : player.Name == player.Clan ? "Opponent" : player.Name;
        var acting = View.WinnerId is null && View.PriorityPlayerId == player.Id;
        var turnOwner = View.WinnerId is null && (View.ActionPhaseTurnPlayerId ?? View.ActivePlayerId) == player.Id;
        var key = $"{player.Clan}|{player.Honor}|{production}|{View.FavorPlayerId == player.Id}|{action}|{turn}|{acting}|{turnOwner}|{ClanPlaque.DescribeHand(player.HandCount, player.HandLimit)}|{player.HandLimit?.Maximum}|{_reducedMotion}";
        return (production, turn, action, acting, turnOwner, key);
    }

    private void RenderClanHud()
    {
        var facts = View.Players.ToDictionary(p => p.Id, PlaqueFacts);
        var kept = new HashSet<int>();
        foreach (var child in _clanHud.GetChildren())
        {
            var owner = _clanPlaques.FirstOrDefault(pair => pair.Value == child);
            if (child is ClanPlaque && IsInstanceValid(child) && owner.Value is not null && facts.TryGetValue(owner.Key, out var now) && _plaqueKeys.GetValueOrDefault(owner.Key) == now.Key) kept.Add(owner.Key);
            else { _clanHud.RemoveChild(child); child.QueueFree(); }
        }
        var previous = new Dictionary<int, ClanPlaque>(_clanPlaques);
        _clanPlaques.Clear(); _plaqueKeys.Clear();
        foreach (var player in View.Players)
        {
            var (production, turn, action, acting, turnOwner, key) = facts[player.Id];
            var own = player.Id == View.ViewerId;
            var plaqueY = own ? 950 - FallenCounter.DesignHeight - 4 : 14;
            if (kept.Contains(player.Id)) _clanPlaques[player.Id] = previous[player.Id];
            else
            {
                var plaque = new ClanPlaque(player.Clan, player.Honor, production, View.FavorPlayerId == player.Id, () => { if (!ChoosePlayerTarget(player.Id)) ShowFallenGallery(player.Id); }, // issue #960: choosing a player
                    action, turn, acting, turnOwner, player.HandCount, player.HandLimit)
                { ReducedMotion = _reducedMotion };
                Place(_clanHud, plaque, 16, plaqueY, ClanPlaque.DesignWidth, ClanPlaque.DesignHeight); _clanPlaques[player.Id] = plaque;
            }
            _plaqueKeys[player.Id] = key;
            var fallen = new FallenCounter { Player = player };
            fallen.Pressed += () => ShowFallenGallery(player.Id);
            Place(_clanHud, fallen, 16, plaqueY + ClanPlaque.DesignHeight + 4, ClanPlaque.DesignWidth, FallenCounter.DesignHeight);
        }
        AddBattleStack();
        if (IsInstanceValid(_fallenGallery)) _fallenGallery!.Refresh(View.Players);
    }
    private readonly Dictionary<(int, Zone), CardView> _discardPreview = [];
    private void RenderPiles()
    {
        Palette.Clear(_pileHud);
        foreach (var player in View.Players)
        {
            var y = ProvinceRowY(player);
            Zone[] zones = [Zone.DynastyDiscard, Zone.DynastyDeck, Zone.FateDeck, Zone.FateDiscard];
            int[] slots = [0, 1, 6, 7];
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                var key = (player.Id, zone);
                if (zone is Zone.DynastyDiscard or Zone.FateDiscard)
                {
                    var cards = player.Discard.Where(c => c.Zone == zone).ToArray();
                    var prior = _motionView?.Players.FirstOrDefault(p => p.Id == player.Id)?.Discard.Select(c => c.Id).ToHashSet() ?? [];
                    var latest = cards.LastOrDefault(c => !prior.Contains(c.Id));
                    if (latest is not null) _discardPreview[key] = latest;
                    if (_discardPreview.TryGetValue(key, out var preview) && !cards.Any(c => c.Id == preview.Id)) _discardPreview.Remove(key);
                }
                Place(_pileHud, new TablePile(player, zone, () => ShowPile(player.Id, zone), _discardPreview.GetValueOrDefault(key)?.Definition), SlotX(player, slots[i]) + 31, y + 2, 142, PlaneHeight(CardHeight) - 4);
            }
        }
        RenderRemovedPiles();
    }
    private void ShowTableMenu()
    {
        var (dialog, body) = Dialog("The table", 560, 720);
        dialog.GetOkButton().Hide();
        body.AddThemeConstantOverride("separation", 9);
        body.AddChild(Palette.Text("Emperor", 32, Palette.Gold, true));
        body.AddChild(Palette.Wrapped(_status.Text, 16, Palette.PaperDim));
        foreach (var (text, action) in new (string, Action)[] { ("Practice", ShowPractice), ("Private match", ShowOnline), ("Card library", () => LibraryDialog.Show(this)), ("Match history", ShowHistory), ("Settings", ShowSettings), ("Main menu", ShowMainMenu), ("Export practice replay", ExportReplay), ("Rules coverage", ShowCoverage), (TutorialActive ? "Leave tutorial…" : "Concede…", ShowConcede) })
        {
            var button = SidebarSkin.Button(text, () => { dialog.QueueFree(); action(); });
            if (text == "Export practice replay" && !_session.CanExportPracticeReplay)
            {
                button.Disabled = true;
                button.TooltipText = _session.Online ? "Replay export is available for local practice matches."
                    : "Prepared review scenes cannot be exported as replayable matches.";
            }
            button.CustomMinimumSize = new Vector2(0, 48);
            foreach (var state in new[] { "normal", "hover", "pressed" }) { var skin = SidebarSkin.Surface(true); skin.ContentMarginTop = skin.ContentMarginBottom = 8; button.AddThemeStyleboxOverride(state, skin); }
            body.AddChild(button);
        }
        body.AddChild(SidebarSkin.Button("Return to game", dialog.QueueFree, true));
    }
    private void ShowPile(int owner, Zone zone)
    {
        if (zone == Zone.Dead) { ShowFallenGallery(owner); return; }
        var player = View.Players.Single(p => p.Id == owner);
        var deck = zone is Zone.FateDeck or Zone.DynastyDeck;
        var name = zone switch { Zone.FateDeck => "Fate deck", Zone.DynastyDeck => "Dynasty deck", Zone.FateDiscard => "Fate discard", Zone.DynastyDiscard => "Dynasty discard", _ => "Dead cards" };
        var (pileDialog, body) = Dialog(player.Clan + " · " + name, 880, 720);
        body.AddChild(Palette.Text(player.Clan + " · " + name, 27, Palette.Gold, true));
        if (deck)
        {
            body.AddChild(Palette.Wrapped("Deck size, contents and order are hidden.", 19));
            if (zone == Zone.FateDeck) body.AddChild(Palette.Text($"{player.HandCount} cards in hand", 18));
            return;
        }
        var cards = player.Discard.Where(c => c.Zone == zone).ToArray();
        var actionable = PileActionableIds(); // issue #965: cards here that may be clicked now
        body.AddChild(Palette.Text(cards.Length == 0 ? "No cards in this pile." : cards.Any(c => actionable.Contains(c.Id)) ? $"{cards.Length} cards · click a highlighted card to choose it" : $"{cards.Length} cards · hover to read", 16, Palette.PaperDim));
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        var grid = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 12); grid.AddThemeConstantOverride("v_separation", 15); scroll.AddChild(grid);
        foreach (var card in cards)
        {
            var legal = actionable.Contains(card.Id);
            var tile = new CardTile(card, true, picked => { if (!legal) return; pileDialog.QueueFree(); SelectCard(picked); }, _ => { }) { CustomMinimumSize = new Vector2(185, 257), FaceHeight = 245, ReducedMotion = true, ContextHelp = player.Clan + " · " + name, LegalTarget = legal };
            grid.AddChild(tile);
        }
    }
}
