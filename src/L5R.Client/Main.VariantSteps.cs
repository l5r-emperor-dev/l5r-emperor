using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // Variant steps (issue #1202). Core lists one option for every combination of the things a single use of an ability asks for. The choosing flow (#1004) asks the performer, province, player and
    // target one at a time; this adds the question after the target: for Stolen Merchandise, "which of its abilities" (ChoiceLabel). The card's list shows one entry per play, and the step
    // appears only when Core offers more than one legal answer, has a Back button, and never picks for the player. Legality stays in Core: the client only lists options Core already enabled.
    private bool _choicePicked;

    private static bool IsAbilityDraft(ActionOption? option) =>
        option is { Type: "equip" or "playStrategy" or "useAbility", AlternativeCost: null, Command.AbilityId: not null };

    /// <summary>The enabled options Core offers for the same ability on the same card with the same performer: the variants of one play.</summary>
    private ActionOption[] VariantsOf(ActionOption option) => Actions.Where(a => a.Enabled && a.Type == option.Type && a.CardId == option.CardId && a.AlternativeCost is null
        && a.Command is { AbilityId: not null } c && c.AbilityId == option.Command!.AbilityId && c.PerformerId == option.Command.PerformerId).ToArray();

    /// <summary>The variants that differ only by Command.Choice for the drafted target: what the player is asked after the target. For a modal ability, its modes for the chosen performer.</summary>
    private ActionOption[] ChoiceVariants(ActionOption option) => option.ChoiceLabel is null || option.Command?.TargetId is not { } target ? ModeVariants(option)
        : VariantsOf(option).Where(a => a.ChoiceLabel is not null && a.Command!.CostOption == option.Command.CostOption && a.TargetIds?.Contains(target) == true)
            .GroupBy(a => (a.Command!.Choice, a.Command.ClanDiscount, a.Command.ClanAllegiance)).Select(g => g.First()).ToArray(); // issue #1230: a recruit's ways to pay are answers too

    /// <summary>Issue #1230: the answers are ways to pay (At Any Cost's Clan discount and allegiance), not things to use from the target.</summary>
    private static bool IsPaymentChoice(ActionOption option) => option.Command is { } c && AbilityCatalog.All.TryGetValue(c.AbilityId ?? "", out var ability) && ability.Cost?.Basis == GoldCostBasis.TargetRecruit && option.ChoiceLabel is not null;

    // Issue #1228: the modes of one printed ability (Wall of Honor's "increase or decrease", Inspired Devotion's two bonuses) are separate abilities in Core that share one use, have no target and
    // name a performer. They are one play: the card is clicked, the performer is clicked on the table, and the mode is asked last. Reactions keep their own flow.
    private static AbilityDefinition? ModalAbility(ActionOption option) => option.Command?.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability)
        && ability.UsageKey is not null && ability.Target == "none" && ability.Performer is not null && ability.Timing != "Reaction" ? ability : null;

    /// <summary>The modes Core enables for the drafted performer, one option each; empty unless there is more than one to choose between.</summary>
    private ActionOption[] ModeVariants(ActionOption option)
    {
        if (ModalAbility(option) is not { } ability || option.Command!.PerformerId is not { } performer) return [];
        var modes = Actions.Where(a => a.Enabled && a.Type == option.Type && a.CardId == option.CardId && a.AlternativeCost is null && a.Command?.PerformerId == performer
            && ModalAbility(a)?.UsageKey == ability.UsageKey).GroupBy(a => a.Command!.AbilityId).Select(g => g.First()).ToArray();
        return modes.Length > 1 ? modes : [];
    }

    /// <summary>What a mode does: its ability's text after the shared cost or performer ("Increase the strength of ...").</summary>
    private static string ModeLabel(ActionOption option)
    {
        var label = FlowLabel(option);
        var colon = label.IndexOf(": ", StringComparison.Ordinal);
        return colon >= 0 ? label[(colon + 2)..] : label;
    }

    private bool ChoiceStepActive => IsAbilityDraft(_chosen) && !ReactionFlowOwnsControls && !_choicePicked && ChoiceVariants(_chosen!).Length > 1;

    /// <summary>The drafted play with every target any of its variants allows, so the player can pick any Holding (or other target) from the one list and on the table; ChooseTarget then selects the variant.</summary>
    private ActionOption WithFlowTargets(ActionOption option) => option.ChoiceLabel is null || !IsAbilityDraft(option) ? option
        : option with { TargetIds = VariantsOf(option).Where(a => a.Command!.CostOption == option.Command!.CostOption).SelectMany(a => a.TargetIds ?? []).Distinct().ToArray() };

    /// <summary>A variant of the draft with what the player already answered carried over: the target when it is still legal, else none; the player when one was picked.</summary>
    private ActionOption Carry(ActionOption next)
    {
        if (_chosen?.Command is not { } draft || next.Command is not { } command) return next;
        var target = draft.TargetId is { } t && next.TargetIds?.Contains(t) == true ? t : NeedsTargetPick(next) ? null : command.TargetId;
        return next with { Command = command with { TargetId = target, TargetPlayerId = IsPlayerTarget(next) ? draft.TargetPlayerId : command.TargetPlayerId } };
    }

    /// <summary>The ability's own text without the " · performer · cost · player" Core appends to tell the variants apart.</summary>
    private static string FlowLabel(ActionOption option) =>
        option.Command?.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && option.Label.StartsWith(ability.Label, StringComparison.Ordinal) ? ability.Label : option.Label;

    private void PickChoice(ActionOption variant)
    {
        _choicePicked = true;
        Choose(Carry(variant), keepTarget: true);
    }

    /// <summary>The question asked after the target: which of the target's options to use (which ability a Holding copies), one row per option Core lists for it.</summary>
    private void RenderChoicePicker(ActionOption action)
    {
        _inspector.AddChild(Palette.Text("STEP · CHOOSE ONE", 11, Palette.Gold));
        var target = action.Command?.TargetId is { } id && FindCard(id) is { } card ? ShortName(card) : "the target";
        _inspector.AddChild(Palette.Wrapped(action.ChoiceLabel is null ? "Choose one. Nothing happens until you confirm." : IsPaymentChoice(action) ? $"Choose how to pay for {target}. Nothing happens until you confirm."
            : $"Choose what to use from {target}. Nothing happens until you confirm.", 13, Palette.PaperDim));
        var rows = new VBoxContainer(); rows.AddThemeConstantOverride("separation", 6); _inspector.AddChild(rows);
        foreach (var variant in ChoiceVariants(action))
        {
            var pick = variant;
            var price = IsPaymentChoice(variant) && action.Command?.TargetId is { } priced && variant.TargetGold?.TryGetValue(priced, out var quote) == true ? $" · {quote.Cost} Gold" : "";
            var text = (variant.ChoiceLabel ?? ModeLabel(variant)) + price;
            var row = SidebarSkin.Button(text, () => PickChoice(pick));
            row.SetMeta("choice_row", variant.Command!.Choice ?? (IsPaymentChoice(variant) ? $"discount={variant.Command.ClanDiscount},allegiance={variant.Command.ClanAllegiance}" : variant.Command.AbilityId) ?? ""); row.Disabled = !DecisionCanSubmit; row.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.CustomMinimumSize = new Vector2(0, 26 + 24 * (int)Math.Ceiling(text.Length / 30.0)); // an ability's whole printed text wraps: the row is as tall as its lines
            rows.AddChild(row);
        }
        _inspector.AddChild(Palette.Wrapped("Choose one to continue.", 13, Palette.Error));
        ScrollInspectorTo(rows); // the rows can sit below a long card text
    }

    /// <summary>A target click on a drafted play whose variants differ by target (Stolen Merchandise's Holdings): switch to the variant for that target, with the choice asked afresh.</summary>
    private ActionOption RetargetVariant(ActionOption draft, string targetId)
    {
        if (draft.ChoiceLabel is null || !IsAbilityDraft(draft)) return draft;
        var next = VariantsOf(draft).Where(a => a.Command!.CostOption == draft.Command!.CostOption && a.TargetIds?.Contains(targetId) == true).FirstOrDefault();
        _choicePicked = false;
        return next is null ? draft : WithFlowTargets(next) with { Command = next.Command! with { TargetId = targetId, TargetPlayerId = draft.Command!.TargetPlayerId ?? next.Command.TargetPlayerId } };
    }

    /// <summary>
    /// A card's actions as the player thinks of them: one entry for each distinct play (an ability), however many performers, cost options and players Core lists it for; those are asked for in the
    /// draft. Core's disabled variants of a play that is available (the "you" of "another player", a bowed performer) are not another action; a play that is not available at all is listed once with its reason.
    /// </summary>
    private void AddCardActions(VBoxContainer container, CardView card)
    {
        var offered = Actions.Where(a => a.CardId == card.Id && a.CardId is not null && a.Type is not "proclaim" && Listable(a)).ToArray();
        string? AbilityOf(ActionOption o) => o.Command?.AbilityId ?? card.Definition?.Abilities?.FirstOrDefault(id => AbilityCatalog.All.TryGetValue(id, out var d) && o.Label.StartsWith(d.Label, StringComparison.Ordinal));
        var available = offered.Where(o => o.Enabled && o.Type == "useAbility" && o.Command?.AbilityId is not null).Select(o => o.Command!.AbilityId).ToHashSet();
        var listed = new HashSet<string>();
        foreach (var option in offered)
        {
            if (option.Type != "useAbility" || option.AlternativeCost is not null || option.Enabled && option.Command is null || AbilityOf(option) is not { } ability) { AddAction(container, option); continue; }
            if (option.Enabled && option.Command is not null)
            {
                var choice = PlayChoices(card).FirstOrDefault(c => c.Key == PlayKey(option));
                if (choice is null || !listed.Add(choice.Key)) continue;
                if (choice.Variants.Length < 2) { AddAction(container, option); continue; }
                var representative = choice.Option;
                var button = SidebarSkin.Button(Placard(representative, FlowLabel(representative)), () => { SelectReactionSource(representative); StartPlay(choice); });
                button.AutowrapMode = TextServer.AutowrapMode.WordSmart; button.ClipText = true; button.Disabled = _session.Busy || _session.Online && !_session.Connected;
                button.SetMeta("play_group", choice.Key); LinkToTable(button, ActionCards(representative));
                container.AddChild(button);
                continue;
            }
            if (available.Contains(ability) || !listed.Add("off:" + ability + ":" + option.Reason)) continue; // a disabled variant of a play that is available, or of one already listed
            AddAction(container, option);
        }
    }
}
