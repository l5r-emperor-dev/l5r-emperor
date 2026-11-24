using Godot;
using L5R.Core;

namespace L5R.Client;

// Issue #1333 (Mendon's approved wording, 2026-11-24): when the player holds an action that a named or chosen card must take -- the first Battle action
// (Naval Invasion, Scouting the Pass, Shosuro Toson, Jade's Roar, Rebuilding Kyuden Hida) or an additional action (Spinning Heel Kick, Two Hands, Equip,
// Learning, ...) -- a gold box above the battle controls says so in plain words. Clicking it selects that card, so its usual action buttons appear; the card
// is outlined on the table. The opponent sees a matching line. Everything here reads MatchView.HeldActions (Core); nothing is decided by the client.
public partial class Main
{
    /// <summary>What to say about one held action: the box's headline, its smaller line, and the card a click selects (null: nothing to select).</summary>
    private sealed record HeldActionText(string Headline, string Detail, string? FocusCardId);

    private const float PhaseActionsHeight = 170;
    private SidebarScroll _phaseScroll = null!;

    private HeldActionView[] HeldActionsOf(int playerId) => (View.HeldActions ?? []).Where(h => h.PlayerId == playerId).ToArray();

    private string Title(string? cardId) => cardId is not null && FindCard(cardId)?.Definition?.Name is { } name ? name : "that card";

    // Whether this card has a Battle action the viewer could take now: when the named performer has none, the only choice is to pass.
    private bool PerformerCanAct(string performerId) =>
        Actions.Any(a => a.Enabled && a.Command is { } c && c.Type is "useAbility" or "playStrategy" or "equip" && (c.PerformerId == performerId || c.CardId == performerId));

    private HeldActionText DescribeHeldAction(HeldActionView held)
    {
        var source = held.SourceName ?? "This effect";
        var decline = held.MayDecline ? " You may decline." : "";
        if (held.Kind == "first")
        {
            if (held.MustPass) return new("Pass Your First Opportunity", $"{source}: you may not announce a Battle action before the Defender's first opportunity.", null);
            if (held.ActionKeyword is { } keyword) return new($"Take the First Action: a {keyword} Action", $"{source}: your first Battle action must be a {keyword} action.", null);
            var name = Title(held.PerformerId);
            var who = source == name ? "he" : name;
            var detail = held.PerformerId is { } performer && !PerformerCanAct(performer)
                ? $"{name} has no Battle action available: you may only pass."
                : $"{source}: {who} must perform your first Battle action.";
            return new($"Take the First Action Using \"{name}\"", detail, held.PerformerId);
        }
        if (held.PerformerId is { } performerId)
        {
            var name = Title(performerId);
            if (held.FromDiscard) return new($"Play a Kiho from Your Discard Pile Using \"{name}\"", $"{name} must perform it; the Strategy is then removed from the game.", performerId);
            if (held.BaseAbilityOnly) return new($"Use One of \"{name}\"'s Printed Abilities", $"{source}: one additional Battle action from his own printed abilities.{decline}", performerId);
            if (held.ActionKeyword is { } keyword)
                return new($"Take an Additional {keyword} Action Using \"{name}\"", $"It must be {keyword}.{(source == name ? $" If you decline, {name.Split(' ')[^1]} bows." : decline)}", performerId);
            return new($"Take an Additional Action Using \"{name}\"", source == name ? $"{name} must perform it.{decline}" : $"{source}: {name} must perform it.{decline}", performerId);
        }
        if (held.ScopeCardIds is { Length: > 0 } scope)
        {
            var cards = scope.Select(FindCard).Where(c => c is not null).ToArray();
            var personality = cards.FirstOrDefault(c => c!.Definition?.Type == CardType.Personality && c.Zone is Zone.Battlefield or Zone.Home);
            if (held.ScopeAbilityIds is { Length: > 0 } && personality is not null)
                return new($"Use \"{personality.Definition!.Name}\"'s Second Ability", $"An additional action from his first ability.{decline}", personality.Id);
            if (personality is not null && cards.Length > 1)
            {
                var left = held.Count >= 2 ? "Up to Two Actions" : "One More Action";
                return new($"Take {left} from \"{personality.Definition!.Name}\"'s Unit",
                    $"{source}: any cards in his unit may act ({held.Count} left). Other actions give no new additional actions this battle.", personality.Id);
            }
            if (personality is not null) return new($"Take an Additional Action Using \"{personality.Definition!.Name}\"", source == personality.Definition.Name ? $"{personality.Definition.Name} must perform it.{decline}" : $"{source}: {personality.Definition.Name} must perform it.{decline}", personality.Id);
            if (cards.Length == 1 && cards[0]!.Zone == Zone.Attached && cards[0]!.Definition is { } attached)
            {
                if (attached.Type == CardType.Follower) return new($"Use a Base Ability of \"{attached.Name}\"", $"{source}: even if it is bowed or has used the ability this turn.{decline}", cards[0]!.Id);
                return new($"Use an Ability on \"{attached.Name}\"", $"{source}: an additional action from the card just attached to {Title(cards[0]!.AttachedTo)}.{decline}", cards[0]!.Id);
            }
            var named = cards.Length == 1 ? Title(cards[0]!.Id) : cards.Length > 1 ? $"one of {cards.Length} cards" : "the chosen card";
            return new($"Play \"{named}\" as an Additional Action", $"{source}: an additional action to play it.{decline}", cards.Length == 1 ? cards[0]!.Id : null);
        }
        if (held.ActionKeyword is { } only) return new($"Take an Additional {only} Action", $"{source}: it must be a {only} action.{decline}", null);
        return new("Take an Additional Action", $"{source}.{decline}", null);
    }

    private string DescribeOpponentHeldAction(HeldActionView held) => held.Kind == "first"
        ? held.MustPass ? "Your opponent must pass their first opportunity." : held.ActionKeyword is { } keyword ? $"Your opponent's first action must be a {keyword} action."
            : $"Your opponent's first action must be taken using \"{Title(held.PerformerId)}\"."
        : $"Your opponent may {SentenceCase(DescribeHeldAction(held).Headline)}.";

    // "Take an Additional Action Using \"Takasho\"" -> "take an additional action using \"Takasho\"": lower-case every word outside the quoted card names.
    private static string SentenceCase(string headline)
    {
        var parts = headline.Split('"');
        for (var i = 0; i < parts.Length; i += 2) parts[i] = parts[i].ToLowerInvariant();
        return string.Join('"', parts);
    }

    /// <summary>The cards named by the actions the viewer holds now, outlined on the table.</summary>
    private HashSet<string> HeldActionCardIds() =>
        _session.Snapshot is null || View.PriorityPlayerId != View.ViewerId ? [] : HeldActionsOf(View.ViewerId).Select(h => DescribeHeldAction(h).FocusCardId).OfType<string>().ToHashSet();

    /// <summary>Above the battle controls: the viewer's boxes when it is their decision, otherwise a line about what the opponent holds.</summary>
    private void RenderHeldActions(Container into)
    {
        _phaseScroll.MaxHeight = PhaseActionsHeight;
        if (_session.Snapshot is null || View.PendingChoice is not null) return;
        if (View.PriorityPlayerId == View.ViewerId)
        {
            // Room for each box and its line, so the usual Pass / Decline buttons stay in view beneath them.
            _phaseScroll.MaxHeight = PhaseActionsHeight + 110 * HeldActionsOf(View.ViewerId).Length;
            foreach (var held in HeldActionsOf(View.ViewerId))
            {
                var text = DescribeHeldAction(held);
                var focus = text.FocusCardId is { } id ? FindCard(id) : null;
                var box = SidebarSkin.Button(text.Headline, () => { if (focus is not null && FindCard(focus.Id) is { } card) SelectCard(card); }, true);
                box.Name = "HeldActionBox"; box.AutowrapMode = TextServer.AutowrapMode.WordSmart; box.CustomMinimumSize = new Vector2(0, 52);
                box.TooltipText = text.Detail; box.AccessibilityName = text.Headline + ". " + text.Detail;
                if (focus is null) box.FocusMode = Control.FocusModeEnum.None;
                into.AddChild(box);
                into.AddChild(Palette.Wrapped(text.Detail, 15, Palette.PaperDim));
            }
            return;
        }
        foreach (var held in HeldActionsOf(1 - View.ViewerId))
            into.AddChild(Palette.Wrapped(DescribeOpponentHeldAction(held), 15, Palette.Gold));
    }
}
