using Godot;
using L5R.Core;
using System.Text.RegularExpressions;

namespace L5R.Client;

public partial class Main
{
    private static bool IsSupportCard(CardView card) => card.Definition?.Type is CardType.Region or CardType.Celestial;
    // A Region sits under its province's Dynasty card (Board.cs), so it no longer widens the province.
    private float ProvinceCardWidth(ProvinceView province) => CardHeight;

    private string SupportContext(CardView card)
    {
        var owner = View.Players.Single(p => p.Id == card.OwnerId);
        if (card.Zone == Zone.Region && card.ProvinceIndex is int index)
        {
            var province = owner.Provinces.Single(p => p.Index == index);
            return $"{owner.Clan} · Region at Province {index + 1} · {province.Strength} province strength.\nAttached to this province; stays here when its Dynasty card changes. " + (card.Bowed ? "Bowed." : "Unbowed.");
        }
        return $"{owner.Clan} · Celestial at Home.\nIndependent of Imperial Favor. A new Celestial replaces this player's previous Celestial. " + (card.Bowed ? "Bowed." : "Unbowed.");
    }

    private bool RenderSupportInspection()
    {
        if (_selected is not { Definition: { } definition } card || !IsSupportCard(card) || card.Zone is not (Zone.Region or Zone.Celestial)) return false;
        var owner = View.Players.Single(p => p.Id == card.OwnerId);
        AddInspectionCaption(card.Zone == Zone.Region ? $"REGION · PROVINCE {card.ProvinceIndex + 1}" : "CELESTIAL · HOME");
        var name = Palette.Wrapped(definition.Name, 24); name.AddThemeFontOverride("font", SidebarSkin.Bold); _inspector.AddChild(name);
        _inspector.AddChild(Palette.Wrapped(owner.Clan + " · " + (card.Bowed ? "Bowed" : "Unbowed"), 15, Palette.Gold));
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 14); _inspector.AddChild(row);
        var face = CardPreview.Face(card, _chosen is null ? 184 : 126, _chosen is null ? 128 : 90); face.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin; row.AddChild(face);
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; text.AddThemeConstantOverride("separation", 7); row.AddChild(text);
        foreach (var part in _chosen is null ? SupportTextParts(definition.Text) : [])
        {
            var heading = Palette.Wrapped(part.Heading, 18); heading.AddThemeFontOverride("font", SidebarSkin.Bold); text.AddChild(heading);
            text.AddChild(new HSeparator());
            text.AddChild(Palette.Wrapped(part.Text, 16, Palette.Paper));
        }
        if (_chosen?.Command?.AbilityId is { } chosenAbility && AbilityCatalog.All.TryGetValue(chosenAbility, out var selectedAbility))
            text.AddChild(Palette.Wrapped(selectedAbility.Label, 16, Palette.Paper));
        _inspector.AddChild(SidebarSkin.Button("Inspect card", () => ShowSupportCard(card)));
        if (card.Zone == Zone.Region && card.ProvinceIndex is int index)
            _inspector.AddChild(Palette.Wrapped($"Province {index + 1} · {owner.Provinces.Single(p => p.Index == index).Strength} strength\nThe Region stays with this province.", 15, Palette.PaperDim));
        foreach (var abilityId in definition.Abilities ?? [])
            if (_chosen is null) AddSupportCost(card, abilityId, _inspector);
        return true;
    }

    private void RenderSupportActions(CardView card)
    {
        foreach (var abilityId in card.Definition!.Abilities ?? [])
        {
            if (!AbilityCatalog.All.TryGetValue(abilityId, out var ability)) continue;
            var choices = Actions.Where(a => a.CardId == card.Id && a.Type == "useAbility" &&
                (a.Command?.AbilityId == abilityId || a.Command is null && a.Label.StartsWith(ability.Label, StringComparison.Ordinal))).ToArray();
            if (choices.Length == 0) continue;
            var option = choices.FirstOrDefault(a => a.Enabled) ?? choices[0];
            var button = SidebarSkin.Button("Use " + ability.Timing + " ability…", () => Choose(option));
            button.Disabled = !option.Enabled || _session.Busy || _session.Online && !_session.Connected;
            button.TooltipText = option.Reason ?? ability.Label;
            if (!option.Enabled && option.Reason is { Length: > 0 } reason) _inspector.AddChild(Palette.Wrapped(reason, 15, Palette.Gold)); // why it cannot be used now, above its button and without hovering (issue #1114)
            _inspector.AddChild(button);
        }
    }

    private ActionOption[] SupportDraftChoices() => _chosen is { CardId: { } id, Command: { } command } && FindCard(id) is { } card && IsSupportCard(card)
        ? Actions.Where(a => a.Enabled && a.CardId == id && a.Command?.AbilityId == command.AbilityId && a.Command?.PerformerId == command.PerformerId).ToArray() : [];

    private bool ChooseSupportTarget(CardView card)
    {
        var option = SupportDraftChoices().FirstOrDefault(a => a.TargetIds?.Contains(card.Id) == true);
        if (option is null) return false;
        Choose(option with { Command = option.Command! with { TargetId = card.Id } }); return true;
    }

    private void RenderSupportDraft(CardView card, ActionOption action)
    {
        var command = action.Command!;
        if (PerformerStepActive) { RenderPerformerStep(); return; } // choosing flow (issue #1004): with several performers, that is the first and only question
        var choices = Actions.Where(a => a.Enabled && a.CardId == card.Id && a.Command?.AbilityId == command.AbilityId).ToArray();
        var performers = choices.Select(a => a.Command!.PerformerId).Distinct().Where(id => id is not null).ToArray();
        if (performers.Length > 1 && _performerPicked && FindCard(command.PerformerId ?? "") is { } chosenPerformer)
            _inspector.AddChild(Palette.Wrapped("Performer: " + ShortName(chosenPerformer), 16, Palette.Crane));
        else if (performers.Length > 0)
        {
            _inspector.AddChild(Palette.Text("PERFORMER", 12, Palette.Gold));
            if (performers.Length > 1) _inspector.AddChild(Palette.Wrapped("Click a highlighted card on the table, or choose below.", 13, Palette.PaperDim));
            var picker = NewChoiceList(); // issue #635
            foreach (var id in performers) picker.AddItem(FindCard(id!)!.Definition!.Name, id);
            picker.Selected = Math.Max(0, Array.IndexOf(performers, command.PerformerId));
            picker.ItemSelected += index =>
            {
                var next = choices.FirstOrDefault(a => a.Command!.PerformerId == performers[(int)index] && a.TargetIds?.Contains(command.TargetId!) == true)
                    ?? choices.First(a => a.Command!.PerformerId == performers[(int)index]);
                Choose(next with { Command = next.Command! with { TargetId = next.TargetIds?.Contains(command.TargetId!) == true ? command.TargetId : next.Command.TargetId } });
            };
            _inspector.AddChild(picker);
        }
        var targetChoices = choices.Where(a => a.Command!.PerformerId == command.PerformerId).ToArray();
        var targets = targetChoices.SelectMany(a => a.TargetIds ?? []).Distinct().ToArray();
        if (targets.Length > 0)
        {
            _inspector.AddChild(Palette.Text("TARGET", 12, Palette.Gold));
            var picker = NewChoiceList(); // issue #635
            foreach (var id in targets)
            {
                var target = FindCard(id)!; var option = targetChoices.First(a => a.TargetIds!.Contains(id));
                picker.AddItem(target.Definition!.Name + (QuoteFor(option with { Command = option.Command! with { TargetId = id } })?.Cost is int gold ? $" · {gold} Gold" : ""), id);
            }
            picker.Selected = Math.Max(0, Array.IndexOf(targets, command.TargetId));
            picker.ItemSelected += index =>
            {
                var target = targets[(int)index]; var next = targetChoices.First(a => a.TargetIds!.Contains(target));
                Choose(next with { Command = next.Command! with { TargetId = target } });
            };
            _inspector.AddChild(picker);
        }
        AddSupportCost(card, command.AbilityId, _inspector);
        if (targets.Length > 0 || performers.Length > 1) RenderFlowSummary(action);
        AddFlowBack(action);
        var (cost, ready) = RenderGoldPanel(action);
        var confirm = SidebarSkin.Button(cost > 0 ? $"Confirm action · {cost} Gold" : "Confirm action", () => _ = Submit(command with { SourceIds = _sources.ToArray() }), true);
        confirm.Disabled = !action.Enabled || _session.Busy || !ready;
        _decisionActions.AddChild(confirm); _decisionActions.AddChild(SidebarSkin.Button("Cancel action", CancelChoice));
    }

    // This only separates printed text for reading. It does not infer ability availability or effects.
    private static IEnumerable<(string Heading, string Text)> SupportTextParts(string source)
    {
        var marks = Regex.Matches(source, @"\b(Battle/Open|Battle|Open|Limited|Reaction):", RegexOptions.CultureInvariant);
        var first = marks.Count > 0 ? marks[0].Index : source.Length;
        var trait = source[..first].Trim();
        if (trait.Length > 0) yield return ("In play", trait);
        for (var i = 0; i < marks.Count; i++)
        {
            var start = marks[i].Index + marks[i].Length;
            var end = i + 1 < marks.Count ? marks[i + 1].Index : source.Length;
            var timing = marks[i].Groups[1].Value;
            yield return (timing == "Reaction" ? "Reaction" : timing + " action", source[start..end].Trim());
        }
    }

    private static void AddSupportCost(CardView card, string? abilityId, VBoxContainer target)
    {
        if (abilityId is null || !AbilityCatalog.All.TryGetValue(abilityId, out var ability)) return;
        var label = Regex.Replace(ability.Label, @"^(Battle/Open|Battle|Open|Limited|Reaction):\s*", "");
        var colon = label.IndexOf(':');
        if (colon < 0) return;
        var requirements = label[..colon];
        string? cost = requirements.Contains("discard this card", StringComparison.OrdinalIgnoreCase) ? "Discard this " + card.Definition!.Type
            : requirements.Contains("bow this card", StringComparison.OrdinalIgnoreCase) ? "Bow this " + card.Definition!.Type : null;
        if (cost is null) return;
        var note = Palette.Wrapped("Cost: " + cost, 17, Palette.Gold); note.SetMeta("support_cost", abilityId); target.AddChild(note);
    }

    private void ShowSupportCard(CardView card)
    {
        var (_, body) = Dialog(card.Definition!.Name, 760, 660);
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 22); body.AddChild(row);
        row.AddChild(CardPreview.Face(card, 560, 380));
        var details = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; row.AddChild(details);
        details.AddChild(Palette.Wrapped(card.Definition.Name, 25));
        details.AddChild(Palette.Wrapped(SupportContext(card), 17, Palette.Gold));
        foreach (var part in SupportTextParts(card.Definition.Text))
        {
            details.AddChild(new HSeparator()); details.AddChild(Palette.Wrapped(part.Heading, 21));
            details.AddChild(Palette.Wrapped(part.Text, 18, Palette.Paper));
        }
    }
}
