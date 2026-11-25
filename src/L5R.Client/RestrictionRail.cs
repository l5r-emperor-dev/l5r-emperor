using Godot;
using L5R.Core;
namespace L5R.Client;

/// <summary>Public restriction/status rows supplied by Core, presented as separate icons outside the card (#1314). Every sentence is supplied by Core.</summary>
public static class RestrictionRail
{
    private static Texture2D? _sprites;
    public static bool HasEffects(CardView card) => card.FaceUp && card.Definition is not null && card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Stronghold or Zone.Region && card.Restrictions is { Length: > 0 };
    public static string Describe(CardView card) => Describe(card.Restrictions ?? []);
    public static string Describe(IEnumerable<CardRestrictionView> rows) => string.Join("\n\n", rows.Select(r => r.Label + "\n" + r.Duration + (r.SourceName is null ? "" : "\nSource: " + r.SourceName)));
    // One icon per kind of effect; protective effects share the protection mark.
    public static IGrouping<(string Kind, bool Protective), CardRestrictionView>[] Groups(CardView card) =>
        !HasEffects(card) ? [] : card.Restrictions!.GroupBy(r => (r.Kind, r.Protective)).ToArray();
    // Alfredo's approved art: move/straighten/protection sprites, the dishonor fan for a lasting Honor loss; every other restriction uses the action-lock seal.
    public static void DrawIcon(CanvasItem canvas, (string Kind, bool Protective) key, Rect2 rect)
    {
        // Issue #1341: a game-long Honor loss (Scars of War) wears the red broken fan of a dishonorable Personality.
        if (key.Kind == "honorLoss") { FallenArt.Medallion(canvas, rect.GetCenter(), rect.Size.X, true); return; }
        _sprites ??= GD.Load<Texture2D>("res://Art/Restrictions/restriction-sprites.png");
        if (!key.Protective && key.Kind is not ("move" or "straighten")) { canvas.DrawTextureRect(ActionLockStatus.Seal, rect, false); return; }
        var source = key.Protective ? new Rect2(1550, 118, 468, 476) : key.Kind == "straighten" ? new Rect2(842, 117, 472, 478) : new Rect2(143, 115, 468, 480);
        canvas.DrawTextureRectRegion(_sprites, rect, source);
    }
}
