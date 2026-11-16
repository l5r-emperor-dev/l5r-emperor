using Godot;
using L5R.Core;

namespace L5R.Client;

/// <summary>Hover reads the projected CardView only; it cannot reveal hidden cards or change a draft.</summary>
public static class CardPreview
{
    public static Control Face(CardView card, float height, float width = 0)
    {
        var face = new CardTile(card with { Bowed = false }, true, _ => { }, _ => { })
        {
            CustomMinimumSize = new Vector2(width, height),
            FaceHeight = height,
            ReducedMotion = true,
            ShowTokenRail = false,
            UprightPreview = true,
            Material = CardPaper.Reading,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None
        };
        face.Resized += () => { face.FaceHeight = Math.Min(height, Math.Max(1, face.Size.X - (CreatedCopyStatus.VisibleOn(card) ? 72 : 0)) * 1.4f); face.QueueRedraw(); };
        return face;
    }
    public static Control Create(CardView card, string context, int? gold, float scale = 1, Func<string, string?>? sourceName = null, string? interactionHint = null, bool printedOnly = false)
    {
        // Fix the popup's measure independently of wrapped text. Godot measures a tooltip
        // before its first layout; a container alone can otherwise grow to thousands of pixels.
        var design = new Vector2(card.Definition is null ? 380 : 750, card.Definition is null ? 118 : 558);
        var shell = new Control { CustomMinimumSize = design * scale, MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        shell.SetMeta("card_preview", card.Id);
        var panel = new PanelContainer { Theme = SidebarSkin.Theme(), MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", SidebarSkin.Inset());
        shell.AddChild(panel); panel.Size = design; panel.Scale = Vector2.One * scale;
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 18); panel.AddChild(row);
        if (card.Definition is not { } def)
        {
            var hidden = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(hidden);
            hidden.AddChild(Palette.Wrapped("Face-down Dynasty · identity hidden", 16));
            hidden.AddChild(Palette.Wrapped(context, 15, Palette.PaperDim));
            return shell;
        }
        if (CardTile.Artwork(def) is { } art)
        {
            row.AddChild(Face(card, 510, 360));
        }
        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(body);
        body.AddChild(Palette.Wrapped(def.Name, 22, Palette.Paper));
        if (CreatedCopyStatus.VisibleOn(card)) body.AddChild(CreatedCopyStatus.Explanation(card));
        if (RestrictionRail.HasEffects(card)) body.AddChild(Palette.Wrapped(RestrictionRail.Describe(card), 16, Palette.Paper));
        if (card.ActionLocked && !RestrictionRail.HasEffects(card))
        {
            var status = new HBoxContainer(); status.AddThemeConstantOverride("separation", 10); body.AddChild(status);
            status.AddChild(ActionLockStatus.Icon(38));
            var words = ActionLockStatus.Explanation(card); words.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; status.AddChild(words);
        }
        body.AddChild(Palette.Text(printedOnly ? "PRINTED CARD · " + def.Type : def.Type + " · " + (card.Bowed ? "BOWED" : "UNBOWED"), 12, Palette.Gold));
        if (def.Type == CardType.Personality && !printedOnly)
            body.AddChild(Palette.Wrapped($"Current: {card.Force} Force · {card.Chi} Chi\n{def.PersonalHonor} Personal Honor", 14));
        if (gold > 0) body.AddChild(Palette.Text($"{gold} Gold" + (card.Bowed ? " · bowed" : " · ready"), 14, Palette.Gold));
        var rules = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; rules.AddChild(details);
        if (def.Type == CardType.Personality)
        {
            details.AddChild(Palette.Wrapped($"Printed: {def.Force} Force · {def.Chi} Chi", 16, Palette.PaperDim));
        }
        foreach (var token in CardTokenRail.Groups(card))
            details.AddChild(Palette.Wrapped($"{token.Type} ×{token.Instances.Length}" + (CardTokenRail.Delta(token.Stat, token.Amount) is { Length: > 0 } delta ? $" · {delta} each" : ""), 16, Palette.Gold));
        if (card.Dishonorable) details.AddChild(Palette.Wrapped("Dishonorable", 20, new Color("efaa9a")));
        if (CardChanges.Entries(card).Length > 0) details.AddChild(CardChangePanel.Create(card, sourceName, compact: true));
        details.AddChild(Palette.Wrapped(string.IsNullOrWhiteSpace(def.Text) ? "No activated ability is printed on this card. Its statistics and traits can still be used by the game and other card effects." : def.Text, 16, Palette.PaperDim));
        details.AddChild(Palette.Wrapped(context, 15, Palette.Crane)); body.AddChild(rules);
        body.AddChild(Palette.Wrapped(interactionHint ?? "Click to select. Read longer rules in the inspector.\n? Rules explains the table and turn sequence.", 12, Palette.Muted));
        return shell;
    }
}
