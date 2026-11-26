using Godot;

namespace L5R.Client;

/// <summary>
/// Issue #635: an always-visible list of the legal cards for a performer or target. It stands where a drop-down stood, with the same small
/// surface (AddItem, Selected, ItemSelected), but every choice is a row on screen, so the pointer can be over a row and over the table at once:
/// hovering a row draws its Personality as hovered, and hovering the Personality on the table lights its row. That is how a player tells apart
/// two Personalities that share a title and differ only in their attachments.
/// </summary>
public sealed partial class CardChoiceList : VBoxContainer
{
    private readonly Func<string, IReadOnlyList<CardTile>> _tilesFor;
    private readonly List<(Button Row, string? CardId)> _rows = [];
    private int _selected = -1;

    public bool Enabled { get; init; } = true;
    public event Action<int>? ItemSelected;
    public int Selected { get => _selected; set { _selected = value; Restyle(); } }

    public CardChoiceList(Func<string, IReadOnlyList<CardTile>> tilesFor)
    {
        _tilesFor = tilesFor;
        AddThemeConstantOverride("separation", 4);
    }

    /// <summary>Adds a row. <paramref name="cardId"/> is the table card the row stands for (null for a row with no card, such as "No performer").
    /// <paramref name="wrap"/> lets a long row (an ability's printed text) wrap onto as many lines as it needs instead of being clipped.</summary>
    public void AddItem(string text, string? cardId, bool wrap = false)
    {
        var index = _rows.Count;
        var row = SidebarSkin.Button(text, () => { if (Enabled) ItemSelected?.Invoke(index); });
        row.Alignment = HorizontalAlignment.Left; row.ClipText = !wrap; row.Disabled = !Enabled;
        if (wrap)
        { // as in the choice step's rows: a wrapping button does not grow by itself, so it is as tall as its lines (about 30 characters each in the sidebar)
            row.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.CustomMinimumSize = new Vector2(0, 26 + 24 * text.Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling((line.Length + 3) / 30.0))));
        }
        row.SetMeta("choice_card", cardId ?? "");
        _rows.Add((row, cardId));
        AddChild(row);
        if (cardId is not null) TableLink.Attach(row, _tilesFor, [cardId]);
        Restyle();
    }

    /// <summary>Selects a row and raises ItemSelected, as a click on it does (used by the capture probes).</summary>
    public void Choose(int index) { Selected = index; ItemSelected?.Invoke(index); }

    private void Restyle()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var (row, _) = _rows[i];
            var selected = i == _selected;
            if (selected) row.AddThemeStyleboxOverride("normal", SidebarSkin.Surface(true, tint: new Color(1.35f, 1.2f, .9f)));
            else row.RemoveThemeStyleboxOverride("normal");
            var label = row.Text.StartsWith("✓  ", StringComparison.Ordinal) || row.Text.StartsWith("○  ", StringComparison.Ordinal) ? row.Text[3..] : row.Text;
            row.Text = (selected ? "✓  " : "○  ") + label;
        }
    }
}
