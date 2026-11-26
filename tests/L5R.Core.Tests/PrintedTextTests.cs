using L5R.Core;

// Issue #1358: the sidebar repeats a selected card's text only when the engine's corrected text differs from its pinned printing. These cases pin the comparison: a card the engine
// words as printed (once Oracle's symbols read as words) matches; an erratum does not; a card with no known printing never matches, so its text is always shown.
internal static class PrintedTextTests
{
    public static void Run(TestRunner runner) => runner.Run("printed-text", [
        ("A card worded as printed matches; a corrected card and a card with no printing do not", MatchesOnlyThePrinting),
    ]);

    private static void MatchesOnlyThePrinting()
    {
        // Rice Farmer: the engine spells out Oracle's ":g2:" as "2 Gold"; the reading is the same.
        Batch336Support.Check(PrintedText.Matches(CardCatalog.Get("oracle-l5r-10587")), "Rice Farmer reads as printed: " + PrintedText.For("oracle-l5r-10587"));
        // Isawa Nairuko: the printing's reminder text "(Shugenja may attach and cast Spells.)" is not a rule, so the engine's text without it still reads as printed.
        Batch336Support.Check(PrintedText.Matches(CardCatalog.Get("oracle-l5r-10242")), "reminder text is ignored: " + PrintedText.For("oracle-l5r-10242"));
        // Kitsuki Yoyugi: the engine's text is a correction of the printing ("printed abilities" and the bow cost differ).
        Batch336Support.Check(!PrintedText.Matches(CardCatalog.Get("oracle-l5r-9817")), "Kitsuki Yoyugi's corrected text differs from the printing");
        Batch336Support.Check(PrintedText.For("training-marketplace") is null && !PrintedText.Matches(CardCatalog.Get("training-marketplace")), "a training card has no printing, so its text is shown");
        Batch336Support.Check(PrintedText.Normalize("Limited, :bow:: Produce :g2:.") == PrintedText.Normalize("Limited, Bow this card: Produce 2 Gold"), "Oracle's symbols read as their words");
    }
}
