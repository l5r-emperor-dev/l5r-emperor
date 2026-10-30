using Godot;
using System.Text.RegularExpressions;
namespace L5R.Client;

/// <summary>One catalogue filter/interaction model for Library and Deck Builder; no game legality lives here.</summary>
public partial class CatalogueBrowserBar : Control
{
    public event Action? Changed;
    public event Action<float>? CardSizeChanged;
    public LineEdit Search { get; }
    public HSlider CardSize { get; }
    public FilterChecklist Types { get; }
    public FilterChecklist Keywords { get; }
    /// <summary>Printing set (unrecognised sources are Other).</summary>
    public FilterChecklist Sets { get; }
    /// <summary>Printed Gold cost, 0 to 10+, or None for a card without one.</summary>
    public FilterChecklist Gold { get; }
    /// <summary>Whether the engine can play the card yet.</summary>
    public FilterChecklist Support { get; }
    /// <summary>Whether the card is in My collection.</summary>
    public FilterChecklist Collection { get; }
    /// <summary>Focus Value, offered for Fate cards only.</summary>
    public FilterChecklist Focus { get; }
    /// <summary>Printed Personal Honor of a Personality, 0 to 5+, or None for a card without one; offered for Dynasty cards only (issue #792).</summary>
    public FilterChecklist PersonalHonor { get; }
    /// <summary>Gold a Holding produces, 0 to 5+, or None; a Holding that produces different amounts is listed under each. Offered for Dynasty cards only (issue #792).</summary>
    public FilterChecklist GoldProduction { get; }
    /// <summary>Printed action type, offered for Dynasty and Fate cards.</summary>
    public FilterChecklist Actions { get; }
    public bool ExtraRow => Family is "Dynasty" or "Fate";
    /// <summary>The clan boxes that are unchecked; every clan starts checked.</summary>
    public HashSet<string> ExcludedClans { get; } = [];
    public string Family { get; private set; }
    public OptionButton SortControl => _sort;
    public string Sort => _sort.GetItemText(_sort.Selected);
    public CatalogueData.Card[] Results { get; private set; } = [];
    public CatalogueData.Card[] CardsWithoutOwnership { get; private set; } = [];
    public Button EditCollectionButton { get; }
    public event Action? EditCollection;
    public string ScopeNotice => _notice.Text;
    public Func<Rect2>? PopupBounds { get => Types.PopupBounds; set { foreach (var menu in Menus) menu.PopupBounds = value; } }
    private FilterChecklist[] Menus => [Types, Keywords, Sets, Gold, Support, Collection, Focus, PersonalHonor, GoldProduction, Actions];
    public float BarHeight => _families.Count == 2 ? 206 : 254;
    private const string Owned = "Owned", Unowned = "Unowned", Playable = "Playable now", NotPlayable = "Not yet playable", OtherSet = "Other", NoneChoice = "None", NoKeywords = "No keywords";
    private static readonly string[] ClanNames = ["Crab", "Crane", "Dragon", "Lion", "Mantis", "Phoenix", "Scorpion", "Spider", "Unicorn", "Imperial", "Unaligned"];
    private readonly CatalogueData _data;
    private readonly Func<CatalogueData.Card, bool> _eligible;
    private readonly string _initialFamily;
    private readonly Dictionary<string, Button> _clans = [], _families = [];
    private readonly Button _selectAll, _deselectAll, _reset, _resetChoices, _alphabetical, _common;
    private readonly PopupPanel _resetPopup;
    private readonly VBoxContainer _resetRows;
    private readonly Dictionary<string, CheckBox> _resetChecks = [];
    /// <summary>Filters the player unchecked in the Clear All menu; Clear All leaves these as they are.</summary>
    public HashSet<string> KeptOnClear { get; } = [];
    private readonly OptionButton _sort;
    private readonly Label _count, _sizeLabel, _notice;
    private readonly ScrollContainer _tagScroll;
    private readonly HBoxContainer _tags;
    private bool _refreshing, _mostCommon;
    private static float? _preferredCardWidth;
    public CatalogueBrowserBar(CatalogueData data, string[] families, string initialFamily = "All", Func<CatalogueData.Card, bool>? eligible = null)
    {
        Name = "CatalogueBrowserBar"; Theme = CatalogueArt.Theme(); _data = data; _eligible = eligible ?? (_ => true); Family = _initialFamily = initialFamily;
        foreach (var clan in ClanNames)
        {
            var button = CatalogueArt.Button("", () => { if (!ExcludedClans.Remove(clan)) ExcludedClans.Add(clan); Refresh(); });
            button.ToggleMode = true; button.AccessibilityName = clan + " cards"; button.TooltipText = clan + " cards";
            AddChild(button); _clans[clan] = button; button.AddChild(CatalogueArt.Clan(clan));
            var label = CatalogueArt.Label(clan, 17); label.HorizontalAlignment = HorizontalAlignment.Center; button.AddChild(label);
            var mark = new TextureRect { Name = "SelectedMark", Texture = DeckSkin.Icon("tick"), MouseFilter = MouseFilterEnum.Ignore, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered }; button.AddChild(mark);
        }
        _selectAll = AddButton("Select all", () => { ExcludedClans.Clear(); Refresh(); });
        _deselectAll = AddButton("Deselect all", () => { ExcludedClans.UnionWith(ClanNames); Refresh(); });
        Collection = new FilterChecklist(this, Theme, "Collection", new Vector2I(300, 300)); AddChild(Collection.Trigger); Collection.Changed += () => Refresh();
        Collection.SetOptions([Owned, Unowned]); Collection.Trigger.TooltipText = "Cards in My collection (Owned) and cards that are not (Unowned).";
        EditCollectionButton = AddButton("Edit my collection", () => EditCollection?.Invoke());
        MyCollection.Store.Changed += CollectionChanged; TreeExiting += () => MyCollection.Store.Changed -= CollectionChanged;
        Search = new LineEdit { PlaceholderText = "Search card names, keywords or rules…", ClearButtonEnabled = true }; AddChild(Search); Search.TextChanged += _ => Refresh();
        foreach (var family in families) _families[family] = AddButton(family, () => SetFamily(family));
        Types = new FilterChecklist(this, Theme, "Types"); AddChild(Types.Trigger); Types.Changed += () => Refresh();
        var order = new HBoxContainer(); order.AddThemeConstantOverride("separation", 10);
        _alphabetical = CatalogueArt.Button("A–Z", () => { _mostCommon = false; Refresh(); }); _common = CatalogueArt.Button("Most common", () => { _mostCommon = true; Refresh(); });
        order.AddChild(_alphabetical); order.AddChild(_common);
        Keywords = new FilterChecklist(this, Theme, "Keywords", extraHeader: order); AddChild(Keywords.Trigger); Keywords.Changed += () => Refresh();
        Focus = new FilterChecklist(this, Theme, "Focus Value", new Vector2I(300, 440)); AddChild(Focus.Trigger); Focus.Changed += () => Refresh();
        Focus.SetOptions(["0", "1", "2", "3", "4", "5", NoneChoice]);
        PersonalHonor = new FilterChecklist(this, Theme, "Personal Honor", new Vector2I(300, 400)); AddChild(PersonalHonor.Trigger); PersonalHonor.Changed += () => Refresh();
        PersonalHonor.SetOptions(["0", "1", "2", "3", "4", "5+", NoneChoice]);
        GoldProduction = new FilterChecklist(this, Theme, "Gold Production", new Vector2I(300, 400)); AddChild(GoldProduction.Trigger); GoldProduction.Changed += () => Refresh();
        GoldProduction.SetOptions(["0", "1", "2", "3", "4", "5+", NoneChoice]);
        Actions = new FilterChecklist(this, Theme, "Action Type", new Vector2I(320, 440)); AddChild(Actions.Trigger); Actions.Changed += () => Refresh();
        Actions.SetOptions(CatalogueActions.Choices);
        Actions.Trigger.TooltipText = "Bold actions printed on the card. Battle and Open include Battle/Open cards; Battle/Open matches only cards printing it; None has no printed action.";
        Sets = new FilterChecklist(this, Theme, "Printing set", new Vector2I(340, 480)); AddChild(Sets.Trigger); Sets.Changed += () => Refresh();
        Sets.SetOptions([.. CatalogueData.SetOrder, OtherSet]); Sets.Trigger.TooltipText = "A card with several printings stays while any of them is checked. Unknown source labels are grouped as Other.";
        Gold = new FilterChecklist(this, Theme, "Gold cost", new Vector2I(300, 480)); AddChild(Gold.Trigger); Gold.Changed += () => Refresh();
        Gold.SetOptions(["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10+", NoneChoice]);
        Support = new FilterChecklist(this, Theme, "Support", new Vector2I(320, 300)); AddChild(Support.Trigger); Support.Changed += () => Refresh();
        Support.SetOptions([Playable, NotPlayable]); Support.Trigger.TooltipText = "Playable now cards are supported by the rules engine; the rest are reference-only.";
        var menus = Menus;
        foreach (var menu in menus) { var self = menu; self.Opening += () => { foreach (var other in menus) if (other != self && other.IsOpen) other.Close(); }; }
        _sort = AddOption("Sort cards", ["Name A–Z", "Name Z–A", "Gold cost", "Force", "Chi", "Set"]);
        _sort.TooltipText = "Set uses Mendon's presentation order; reprints sort by their earliest recognized set.";
        _tagScroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Auto, VerticalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tags = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; _tags.AddThemeConstantOverride("separation", 8); _tagScroll.AddChild(_tags); AddChild(_tagScroll);
        _reset = AddButton("Clear All", ClearSelected); _reset.AccessibilityName = "Clear all filters"; _reset.TooltipText = "Resets every filter that is checked in the menu beside this button.";
        _resetChoices = AddButton("▾", ToggleResetMenu); _resetChoices.AccessibilityName = "Choose filters to clear"; _resetChoices.TooltipText = "Uncheck a filter to keep it when you press Clear All.";
        _resetPopup = new PopupPanel { Theme = Theme, Name = "ClearAllChecklist" }; AddChild(_resetPopup);
        _resetPopup.AddThemeStyleboxOverride("panel", Palette.Box(new Color("0e171d"), CatalogueArt.Gold, 18, 5));
        var resetBox = new VBoxContainer(); resetBox.AddThemeConstantOverride("separation", 8); _resetPopup.AddChild(resetBox);
        resetBox.AddChild(CatalogueArt.Label("Clear All resets", 23, true)); resetBox.AddChild(CatalogueArt.Label("Uncheck a filter to keep it.", 15));
        _resetRows = new VBoxContainer(); _resetRows.AddThemeConstantOverride("separation", 4); resetBox.AddChild(_resetRows);
        _count = CatalogueArt.Label("", 17); AddChild(_count);
        _sizeLabel = CatalogueArt.Label("Card size", 16); AddChild(_sizeLabel);
        CardSize = new HSlider { Name = "CardSize", MinValue = 120, MaxValue = 300, Step = 2, Value = _preferredCardWidth ?? (families.Length == 2 ? 196 : 250), AccessibilityName = "Card size", TooltipText = "Drag to resize full card faces. Arrow keys also adjust size." };
        var track = Palette.Box(new Color("10171a"), new Color("665332"), 2, 1); track.ContentMarginTop = track.ContentMarginBottom = 2;
        CardSize.AddThemeStyleboxOverride("slider", track);
        var fill = Palette.Box(new Color("8d7040"), CatalogueArt.Gold, 2, 1); fill.ContentMarginTop = fill.ContentMarginBottom = 2;
        CardSize.AddThemeStyleboxOverride("grabber_area", fill); CardSize.AddThemeStyleboxOverride("grabber_area_highlight", fill);
        CardSize.AddThemeIconOverride("grabber", DeckSkin.Icon("radio")); CardSize.AddThemeIconOverride("grabber_highlight", DeckSkin.Icon("selected"));
        AddChild(CardSize); CardSize.ValueChanged += value => { _preferredCardWidth = (float)value; _sizeLabel.TooltipText = $"{value:N0} logical pixels wide"; CardSizeChanged?.Invoke((float)value); };
        _notice = CatalogueArt.Label("", 15); _notice.AddThemeColorOverride("font_color", CatalogueArt.Gold); AddChild(_notice);
        Resized += Fit;
        Refresh(false);
    }
    private Button AddButton(string text, Action action) { var button = CatalogueArt.Button(text, action); AddChild(button); return button; }
    private OptionButton AddOption(string name, string[] options)
    {
        var option = new OptionButton { Name = name.Replace(" ", ""), AccessibilityName = name, FitToLongestItem = false, ClipText = true };
        foreach (var item in options) option.AddItem(item); AddChild(option); option.ItemSelected += _ => Refresh();
        option.GetPopup().AllowSearch = true; option.GetPopup().MaxSize = new Vector2I(520, 460); return option;
    }
    public void SetFamily(string family)
    {
        if (!_families.ContainsKey(family)) return;
        Family = family; Refresh();
    }
    public void Reset()
    {
        _refreshing = true; ExcludedClans.Clear(); foreach (var menu in Menus) menu.Clear(); Search.Text = ""; Family = _initialFamily;
        _refreshing = false; _notice.Text = ""; Refresh();
    }
    private IEnumerable<(string Name, bool Shown)> ClearChoices()
    {
        yield return ("Search", true); yield return ("Deck type", _families.Count > 1); yield return ("Clans", true);
        foreach (var menu in Menus) yield return (menu.Label, menu.Trigger.Visible);
    }
    private void ToggleResetMenu()
    {
        if (_resetPopup.Visible) { _resetPopup.Hide(); return; }
        foreach (var menu in Menus) if (menu.IsOpen) menu.Close();
        Palette.Clear(_resetRows); _resetChecks.Clear();
        foreach (var (name, shown) in ClearChoices().Where(c => c.Shown))
        {
            var check = new CheckBox { Text = name, ButtonPressed = !KeptOnClear.Contains(name), CustomMinimumSize = new Vector2(280, 32), AccessibilityName = "Clear " + name };
            check.AddThemeColorOverride("font_color", CatalogueArt.Ivory); check.AddThemeFontSizeOverride("font_size", 18);
            check.AddThemeIconOverride("checked", DeckSkin.Icon("checked")); check.AddThemeIconOverride("unchecked", DeckSkin.Icon("unchecked"));
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" }) check.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            var key = name; check.Toggled += on => { if (on) KeptOnClear.Remove(key); else KeptOnClear.Add(key); };
            _resetRows.AddChild(check); _resetChecks[name] = check;
        }
        var rect = _resetChoices.GetGlobalRect(); var viewport = _resetChoices.GetViewport().GetVisibleRect();
        var size = new Vector2I(320, 70 + 36 * _resetChecks.Count);
        _resetPopup.Popup(new Rect2I(Math.Clamp((int)rect.End.X - size.X, 4, Math.Max(4, (int)viewport.End.X - size.X - 4)), Math.Min((int)rect.End.Y + 6, Math.Max(4, (int)viewport.End.Y - size.Y - 4)), size.X, size.Y));
    }
    /// <summary>Clear All: resets every filter except the ones unchecked in its menu.</summary>
    public void ClearSelected()
    {
        bool Clears(string name) => !KeptOnClear.Contains(name);
        _refreshing = true;
        if (Clears("Clans")) ExcludedClans.Clear();
        foreach (var menu in Menus) if (Clears(menu.Label)) menu.Clear();
        if (Clears("Search")) Search.Text = "";
        if (Clears("Deck type")) Family = _initialFamily;
        _refreshing = false; _notice.Text = ""; Refresh();
    }
    public void Refresh(bool notify = true)
    {
        if (_refreshing) return; _refreshing = true;
        try
        {
            var familyCards = _data.Filter(family: Family).Where(_eligible);
            var removed = new List<string>();
            if (Family != "Fate" && Focus.Active) { removed.Add("Focus Value"); Focus.Clear(); }
            if (Family != "Dynasty" && PersonalHonor.Active) { removed.Add("Personal Honor"); PersonalHonor.Clear(); }
            if (Family != "Dynasty" && GoldProduction.Active) { removed.Add("Gold Production"); GoldProduction.Clear(); }
            if (!ExtraRow && Actions.Active) { removed.Add("Action Type"); Actions.Clear(); }
            Types.SetOptions(familyCards.SelectMany(c => c.Types).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray());
            CardsWithoutOwnership = FilterWithoutKeywords(false, true).Where(KeywordPasses).ToArray();
            var scoped = FilterWithoutKeywords().ToArray();
            var counts = scoped.SelectMany(KeywordFeatures).GroupBy(k => k).ToDictionary(g => g.Key, g => g.Count());
            foreach (var unchecked_ in Keywords.Excluded) counts.TryAdd(unchecked_, 0);
            var ordered = _mostCommon ? counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key) : counts.Keys.Order(StringComparer.OrdinalIgnoreCase);
            Keywords.SetOptions(ordered.ToArray(), counts);
            Results = scoped.Where(KeywordPasses).ToArray();
            if (removed.Count > 0) _notice.Text = string.Join(", ", removed.Distinct()) + " cleared · unavailable with these filters.";
            else if (MyCollection.Store.LoadError.Length > 0) _notice.Text = "Collection unavailable; open Edit my collection for details. Your original file is preserved.";
            else if (notify) _notice.Text = "";
            _count.Text = $"{Results.Length:N0} matching cards";
            foreach (var (clan, button) in _clans)
            {
                var active = !ExcludedClans.Contains(clan); button.SetPressedNoSignal(active); CatalogueArt.Selected(button, active);
                button.GetChild<TextureRect>(0).Modulate = active ? Colors.White : new Color(.75f, .75f, .75f);
                button.GetChild<Label>(1).AddThemeColorOverride("font_color", active ? new Color("efd48d") : CatalogueArt.Ivory);
                button.GetNode<TextureRect>("SelectedMark").Visible = active;
            }
            foreach (var (family, button) in _families) SelectedStyle(button, Family == family);
            SelectedStyle(_alphabetical, !_mostCommon); SelectedStyle(_common, _mostCommon);
            Focus.Trigger.Visible = Family == "Fate"; Actions.Trigger.Visible = ExtraRow; PersonalHonor.Trigger.Visible = GoldProduction.Trigger.Visible = Family == "Dynasty";
            BuildTags(); Fit();
        }
        finally { _refreshing = false; }
        if (notify) Changed?.Invoke();
    }
    private void CollectionChanged() => Refresh();
    /// <summary>Checks only these clan boxes.</summary>
    public void OnlyClans(params string[] clans) { ExcludedClans.Clear(); ExcludedClans.UnionWith(ClanNames.Except(clans)); Refresh(); }
    /// <summary>Checks Owned, Unowned or both.</summary>
    public void SetOwnership(OwnershipFilter filter)
    {
        Collection.SetExcluded(filter == OwnershipFilter.Owned ? [Unowned] : filter == OwnershipFilter.Unowned ? [Owned] : []); Refresh();
    }
    private static string[] KeywordFeatures(CatalogueData.Card c) => c.Keywords.Length == 0 ? [NoKeywords] : c.Keywords;
    private bool KeywordPasses(CatalogueData.Card c) => Keywords.Passes(KeywordFeatures(c));
    /// <summary>The clan boxes a card belongs to; a card with no clan, or only clans without a box of their own, is Unaligned. An Imperial Personality is also in the Imperial box.</summary>
    private static string[] ClanFeatures(CatalogueData.Card c)
    {
        var boxes = c.Clans.Where(ClanNames.Contains).ToList(); if (boxes.Count == 0) boxes.Add("Unaligned");
        // Imperial is a keyword on Personalities rather than a Clan: it adds a box to a Personality without removing the Clan it also has.
        if (c.Types.Contains("Personality") && c.Keywords.Contains("Imperial") && !boxes.Contains("Imperial")) boxes.Add("Imperial");
        // Journey's End Keep (either printing) makes its player Imperial, though the catalogue lists the Experienced one with no Clan.
        if (c.Types.Contains("Stronghold") && c.Name.StartsWith("Journey's End Keep", StringComparison.Ordinal) && !boxes.Contains("Imperial")) { boxes.Remove("Unaligned"); boxes.Add("Imperial"); }
        return [.. boxes];
    }
    private static string[] GoldFeature(CatalogueData.Card c) => [c.Numeric("cost") is var cost && cost != double.MaxValue ? (cost >= 10 ? "10+" : ((int)cost).ToString()) : NoneChoice];
    private static string[] PersonalHonorFeature(CatalogueData.Card c) =>
        c.Types.Contains("Personality") && c.Stats.TryGetValue("ph", out var values) && int.TryParse(values.FirstOrDefault(), out var honor) ? [honor >= 5 ? "5+" : Math.Max(0, honor).ToString()] : [NoneChoice];
    private static readonly Regex ProduceClause = new(@"\bproduce[sd]?\b([^.;\n]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GoldAmount = new(@"\b(\d+)\s+Gold\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <summary>Every Gold amount a Holding's "Produce N Gold, or M Gold when..." clauses name, so a Holding with two amounts is found under either. Other cards, and Holdings with no production, are None.</summary>
    internal static string[] GoldProductionFeatures(CatalogueData.Card c)
    {
        if (!c.Types.Contains("Holding")) return [NoneChoice];
        var amounts = new SortedSet<int>();
        foreach (Match clause in ProduceClause.Matches(c.CurrentOracleText)) foreach (Match amount in GoldAmount.Matches(clause.Groups[1].Value)) amounts.Add(int.Parse(amount.Groups[1].Value));
        if (amounts.Count == 0 && c.Definition is { GoldProduction: > 0 } definition) amounts.Add(definition.GoldProduction);
        return amounts.Count == 0 ? [NoneChoice] : amounts.Select(a => a >= 5 ? "5+" : a.ToString()).Distinct().ToArray();
    }
    private static string[] SetFeatures(CatalogueData.Card c)
    {
        var sets = c.Printings.Where(p => p.Set != "Game printing").Select(p => CatalogueData.SetOrder.Contains(p.Set) ? p.Set : OtherSet).Distinct().ToArray();
        return sets.Length == 0 ? [OtherSet] : sets;
    }
    private IEnumerable<CatalogueData.Card> FilterWithoutKeywords(bool ownership = true, bool ignoreSearch = false)
    {
        var cards = _data.Filter(ignoreSearch ? "" : Search.Text, null, Family, null, null, false, Sort).Where(_eligible)
            .Where(c => FilterChecklist.Passes(ExcludedClans, ClanFeatures(c))
                && Types.Passes(c.Types) && Sets.Passes(SetFeatures(c)) && Gold.Passes(GoldFeature(c)) && Support.Passes(c.Playable ? Playable : NotPlayable));
        if (Family == "Fate" && Focus.Active) cards = cards.Where(c => Focus.Passes(c.Focus is { } focus ? focus.ToString() : NoneChoice));
        if (Family == "Dynasty" && PersonalHonor.Active) cards = cards.Where(c => PersonalHonor.Passes(PersonalHonorFeature(c)));
        if (Family == "Dynasty" && GoldProduction.Active) cards = cards.Where(c => GoldProduction.Passes(GoldProductionFeatures(c)));
        if (ExtraRow && Actions.Active) cards = cards.Where(c => CatalogueActions.Choices.Any(choice => Actions.Checked(choice) && CatalogueActions.Matches(c.Actions, choice)));
        return ownership && Collection.Active ? cards.Where(c => Collection.Passes(MyCollection.Store.Count(c.Id) > 0 ? Owned : Unowned)) : cards;
    }
    private void BuildTags()
    {
        Palette.Clear(_tags);
        void Tag(string text, Action remove, string name)
        {
            var b = CatalogueArt.Button(text + "  ×", remove);
            foreach (var state in new[] { "normal", "hover", "pressed" }) b.AddThemeStyleboxOverride(state, CatalogueArt.ControlStyle(new Color("e1d6c2"), new Color("8c7447"), 8, 6));
            b.AddThemeColorOverride("font_color", new Color("161d24")); b.AddThemeColorOverride("font_hover_color", new Color("161d24")); b.AccessibilityName = "Remove filter " + name; b.AddThemeFontSizeOverride("font_size", 16); _tags.AddChild(b);
        }
        // One tag per filter that is hiding something; removing it checks that filter's options again.
        if (ExcludedClans.Count > 0) Tag("Clans: " + (ExcludedClans.Count >= ClanNames.Length ? "None" : $"{ClanNames.Length - ExcludedClans.Count} of {ClanNames.Length}"), () => { ExcludedClans.Clear(); Refresh(); }, "Clans");
        foreach (var menu in Menus.Where(m => m.Active && m.Trigger.Visible)) Tag($"{menu.Label}: {menu.Summary}", () => { menu.Clear(); Refresh(); }, menu.Label);
        if (_tags.GetChildCount() == 0) { var hint = CatalogueArt.Label("All clans · All types · All keywords", 16); hint.CustomMinimumSize = new Vector2(360, 30); _tags.AddChild(hint); }
    }
    private static void SelectedStyle(Button button, bool selected) => CatalogueArt.Selected(button, selected);
    private static void Place(Control child, float x, float y, float w, float h) { child.Position = new Vector2(x, y); child.Size = new Vector2(w, h); }
    private void Fit()
    {
        if (Size.X < 400 || _notice is null) return;
        var w = Size.X; var cell = (w - 224) / ClanNames.Length; var i = 0;
        foreach (var button in _clans.Values)
        {
            Place(button, i++ * cell, 0, cell - 6, 72); Place(button.GetChild<Control>(0), (cell - 42) / 2, 4, 36, 36);
            Place(button.GetChild<Control>(1), 0, 44, cell - 6, 24); Place(button.GetChild<Control>(2), cell - 29, 7, 16, 16);
        }
        Place(_selectAll, w - 215, 14, 100, 38); Place(_deselectAll, w - 109, 14, 109, 38);
        var compact = _families.Count == 2; var searchY = compact ? 80 : 84; var filterY = compact ? 122 : 150; var tagsY = compact ? 166 : 204;
        // Focus Value and Action Type lead the tag row, so offering them never changes the bar's height.
        var tagX = 0; if (ExtraRow) { tagX = 190 + 10; if (Focus.Trigger.Visible) tagX += 176 + 10; if (GoldProduction.Trigger.Visible) tagX += 176 + 10 + 190 + 10; }
        var familyWidth = _families.Count == 4 ? 400 : 230;
        Place(Search, 0, searchY, w - familyWidth - 435, compact ? 40 : 52);
        Place(Collection.Trigger, w - familyWidth - 425, searchY, 220, compact ? 40 : 52); Place(EditCollectionButton, w - familyWidth - 195, searchY, 183, compact ? 40 : 52);
        var x = w - familyWidth;
        foreach (var (key, button) in _families) { var width = key == "Strongholds" ? 130 : (familyWidth - (_families.ContainsKey("Strongholds") ? 130 : 0)) / (_families.Count - (_families.ContainsKey("Strongholds") ? 1 : 0)); Place(button, x, searchY, width - 2, compact ? 40 : 52); x += width; }
        Place(Types.Trigger, 0, filterY, 174, compact ? 38 : 44); Place(Keywords.Trigger, 184, filterY, 192, compact ? 38 : 44); Place(Sets.Trigger, 386, filterY, 245, compact ? 38 : 44); Place(Gold.Trigger, 641, filterY, 140, compact ? 38 : 44);
        Place(Support.Trigger, 799, filterY, 184, compact ? 38 : 44);
        Place(_count, compact ? 1173 : w - 320, compact ? filterY + 3 : tagsY + 5, compact ? Math.Max(130, w - 1173) : 320, 32);
        Place(_tagScroll, tagX, tagsY, Math.Max(80, w - 695 - tagX), compact ? 38 : 44); Place(_reset, w - 684, tagsY, 112, compact ? 38 : 44); Place(_resetChoices, w - 568, tagsY, 44, compact ? 38 : 44);
        Place(_sort, w - 506, tagsY, 170, compact ? 38 : 44); Place(_sizeLabel, w - 320, compact ? tagsY + 5 : filterY + 12, 78, 25); Place(CardSize, w - 238, compact ? tagsY + 6 : filterY + 13, 230, 24);
        if (ExtraRow) Place(Actions.Trigger, 0, tagsY, 190, compact ? 38 : 44);
        if (Focus.Trigger.Visible) Place(Focus.Trigger, 200, tagsY, 176, compact ? 38 : 44);
        if (PersonalHonor.Trigger.Visible) Place(PersonalHonor.Trigger, 200, tagsY, 176, compact ? 38 : 44);
        if (GoldProduction.Trigger.Visible) Place(GoldProduction.Trigger, 386, tagsY, 190, compact ? 38 : 44);
        Place(_notice, 0, compact ? 204 : 248, w, compact ? 18 : 20);
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawLine(new Vector2(0, 77), new Vector2(Size.X, 77), new Color(.56f, .43f, .22f, .55f), 1);
    }
    public void CloseMenus() { foreach (var menu in Menus) if (menu.IsOpen) menu.Close(); _resetPopup.Hide(); }
}
