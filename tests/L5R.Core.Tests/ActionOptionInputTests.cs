using System.Reflection;
using L5R.Core;

// Issue #1028: ActionOption.RequiresInput() is the one place that says which of an option's fields mean the player must supply more than a confirm.
// A client used to keep its own list of those fields, so a field added to ActionOption was silently treated as "no input needed". This suite makes the
// decision explicit: every ActionOption property must be listed below as input-implying or not, so adding a field fails here until someone decides.
internal static class ActionOptionInputTests
{
    private static readonly string[] ImpliesInput = ["TargetIds", "Slots", "Movement", "Attack", "AlternativeCost", "Gold", "TargetGold"]; // read by RequiresInput (Gold only when it costs or is variable; Command only through CardIds)
    private static readonly string[] DoesNotImplyInput = [
        "Type", "Label", "Enabled", "Reason", "Command", "CardId", "Quote", // identity, display, the command itself, and the purchase quote (purchases have their own panel)
        "Provinces", "NoTarget", // a province or attack-target pick depends on the action's shape; the client reads those shapes (IsProvinceTarget, HasAttackTargets)
        "DecisionCandidateIds", "DecisionLabel", "MoveTo", "ChoiceLabel"]; // presentation of a complete pending answer; where a move goes; the text of a choice made after the target

    public static void Run(TestRunner runner) => runner.Run("action-option-input", [
        ("Every ActionOption field is classified as implying player input or not, so a new field cannot be silently ignored by a client", EveryFieldIsClassified),
        ("RequiresInput is true for each field it reads and false for a plain option", RequiresInputReadsItsFields)
    ]);

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static void EveryFieldIsClassified()
    {
        var properties = typeof(ActionOption).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).Where(n => n != "EqualityContract").ToHashSet();
        var classified = ImpliesInput.Concat(DoesNotImplyInput).ToHashSet();
        Check(properties.SetEquals(classified), "unclassified: " + string.Join(", ", properties.Except(classified)) + "; no longer exists: " + string.Join(", ", classified.Except(properties)));
        Check(!ImpliesInput.Intersect(DoesNotImplyInput).Any(), "a field is listed on both sides");
    }

    private static void RequiresInputReadsItsFields()
    {
        var plain = new ActionOption("pass", "Pass", true, null, new GameCommand("pass", 0, 1));
        Check(!plain.RequiresInput(), "a plain option needs no input");
        GoldQuote Quote(int cost, string kind) => new(cost, kind, "", null, []);
        Check((plain with { TargetIds = ["a"] }).RequiresInput(), "targets need a pick");
        Check((plain with { TargetIds = [] }).RequiresInput() == false, "an empty target list does not");
        Check((plain with { Slots = [] }).RequiresInput(), "slots need a pick");
        Check((plain with { Movement = new(0, [], false) }).RequiresInput(), "a movement needs a destination");
        Check((plain with { Attack = new("attack", "a") }).RequiresInput(), "an attack cue needs input");
        Check((plain with { TargetGold = [] }).RequiresInput(), "a target-derived price needs a target");
        Check((plain with { Gold = Quote(2, GoldCostKind.Fixed) }).RequiresInput(), "a Gold cost needs a payment");
        Check((plain with { Gold = Quote(0, GoldCostKind.Variable) }).RequiresInput(), "a variable cost needs an amount");
        Check(!(plain with { Gold = Quote(0, GoldCostKind.Fixed) }).RequiresInput(), "a free fixed cost needs nothing");
        Check((plain with { Command = new GameCommand("pass", 0, 1) with { CardIds = ["c"] } }).RequiresInput(), "a command with CardIds needs the player's pick");
    }
}
