using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    private void RenderLockedInspection(CardView card)
    {
        AddInspectionCaption("Card details");
        var title = new HBoxContainer(); title.AddThemeConstantOverride("separation", 14); _inspector.AddChild(title);
        var name = Palette.Wrapped(card.Definition!.Name, 27); name.AddThemeFontOverride("font", SidebarSkin.Bold);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; title.AddChild(name); title.AddChild(ActionLockStatus.Icon(56));
        _inspector.AddChild(Palette.Wrapped($"{card.Definition.Clan} · {card.Definition.Type}", 18, Palette.PaperDim));
        if (PrintedActionStrip.VisibleOn(card)) _inspector.AddChild(PrintedActionStrip.Explanation(card));
        if (CreatedCopyStatus.VisibleOn(card)) _inspector.AddChild(CreatedCopyStatus.Explanation(card));
        _inspector.AddChild(new HSeparator());
        if (RestrictionRail.HasEffects(card)) _inspector.AddChild(Palette.Wrapped(RestrictionRail.Describe(card), 17, Palette.Paper));
        else _inspector.AddChild(ActionLockStatus.Explanation(card));
        if (card.StraightenBlocked && !RestrictionRail.HasEffects(card)) _inspector.AddChild(Palette.Wrapped("Straightening is prevented for this turn.", 16, Palette.Gold));
        if (card.Dishonorable) _inspector.AddChild(Palette.Wrapped("Dishonorable", 20, new Color("efaa9a")));
        if (card.AttachedTo is { } parent) _inspector.AddChild(Palette.Wrapped("Attached to " + (FindCard(parent)?.Definition?.Name ?? "its Personality"), 16, Palette.PaperDim));
        if (CardChanges.Entries(card).Length > 0) _inspector.AddChild(CardChangePanel.Create(card, id => FindCard(id)?.Definition?.Name, compact: true));
        _inspector.AddChild(new HSeparator());
    }
    private void InspectLockedCard(CardView card)
    {
        var (_, body) = Dialog(card.Definition!.Name, 720, 820);
        body.AddChild(CardPreview.Face(card, 560)); body.AddChild(RestrictionRail.HasEffects(card) ? Palette.Wrapped(RestrictionRail.Describe(card), 17, Palette.Paper) : ActionLockStatus.Explanation(card));
    }
}
