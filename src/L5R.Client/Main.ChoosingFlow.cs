using Godot;
using L5R.Core;

namespace L5R.Client;

public partial class Main
{
    // The choosing flow (issue #1004): a drafted action asks for one thing at a time -- performer, province, player, target -- each a single click that moves on to the next, with a Back button and a
    // bullet summary once everything is chosen. Only what the engine already offers is listed (performers, players and targets are the legal ones); the client never decides legality.
    private bool _performerPicked;

    private bool PerformerStepActive => _chosen is { Command.AbilityId: not null } && !ReactionFlowOwnsControls && !_performerPicked && PerformerClicks().Count > 0; // an alternative-cost draft too (issue #1202)

    private void PickPerformer(string id)
    {
        _performerPicked = true;
        if (PerformerClicks().TryGetValue(id, out var choose) && _chosen?.Command?.PerformerId != id) choose(); else { RenderInspector(); Highlight(); }
    }

    private void RenderPerformerStep()
    {
        _inspector.AddChild(Palette.Text("STEP · CHOOSE A PERFORMER", 11, Palette.Gold));
        _inspector.AddChild(Palette.Wrapped("Choose the performing Personality: click a highlighted Personality on the table, or choose below.", 13, Palette.PaperDim));
        foreach (var id in PerformerClicks().Keys)
        {
            if (FindCard(id) is not { } card) continue;
            var performerId = id;
            var row = SidebarSkin.Button($"{ShortName(card)} · {card.Force} F{(CardTile.UnitLabel(card) is { Length: > 0 } unit ? " · " + unit : "")}", () => PickPerformer(performerId));
            row.SetMeta("performer_row", id); row.Disabled = !DecisionCanSubmit; row.AutowrapMode = TextServer.AutowrapMode.WordSmart; LinkToTable(row, id);
            _inspector.AddChild(row);
        }
        _decisionActions.AddChild(SidebarSkin.Button("Cancel action", CancelChoice));
    }

    /// <summary>Undo the most recent choice: the target, else the player, else the province, else the performer.</summary>
    private void FlowBack()
    {
        if (_chosen?.Command is not { } command) return;
        if (_choicePicked) { _choicePicked = false; RenderInspector(); Highlight(); return; } // issue #1202: the most recent answer first
        if (command.TargetId is not null && (_chosen.TargetIds?.Length ?? 0) > 0) { ClearTarget(); return; }
        if (command.TargetPlayerId is not null && PlayerTargetActive && PlayerSiblings(_chosen).FirstOrDefault() is { } sibling)
        { Choose(sibling with { Command = sibling.Command! with { TargetPlayerId = null } }, keepTarget: true); return; }
        if (command.Province is not null && IsProvinceTarget(_chosen)) { _chosen = _chosen with { Command = command with { Province = null } }; RenderInspector(); Highlight(); return; }
        if (_performerPicked) { _performerPicked = false; RenderInspector(); Highlight(); }
    }

    private bool FlowCanGoBack(ActionOption action) => action.Command is { } c
        && (c.TargetId is not null && (action.TargetIds?.Length ?? 0) > 0 || c.TargetPlayerId is not null && PlayerTargetActive || c.Province is not null && IsProvinceTarget(action) || _performerPicked && PerformerClicks().Count > 0 || _choicePicked);

    // Short effect lines for the summary, "{P}" the performer and "{T}" the target. A card not listed falls back to the engine's own action text.
    private static readonly Dictionary<string, string[]> FlowEffects = new()
    {
        ["sound-of-thunder-move"] = ["Move and Straighten ({P})", "Give ({T}) -3F"],
        ["paragon-strength-move"] = ["Send home ({T})", "Gain 2 Honor"],
        ["paragon-strength-attack"] = ["Ranged Attack, strength = Force ({P})", "Lose 4 Honor"],
        ["captured-convict-move"] = ["Send home ({T})", "If dishonorable, Kolat, Ninja or Shadowlands: a player gains or loses 2 Honor"],
    };

    private string[] FlowSummaryLines(ActionOption action)
    {
        var command = action.Command!;
        string Name(string? id) => id is not null && FindCard(id) is { } c ? ShortName(c) : "?";
        string Fill(string line) => line.Replace("{P}", Name(command.PerformerId)).Replace("{T}", Name(command.TargetId));
        var known = command.AbilityId is { } id && FlowEffects.TryGetValue(id, out var effects) ? effects : null;
        var lines = known is not null ? known.Select(Fill).ToList() : [Placard(action)];
        if (known is not null && !known.Any(l => l.Contains("{T}")))
        {
            if (command.TargetId is not null) lines.Add("Target: " + Name(command.TargetId));
            else if (command.Province is { } province && _attackChoice == AttackChoice.Province) lines.Add($"Target: Province {province + 1}");
            else if (_attackChoice == AttackChoice.NoTarget) lines.Add("No target");
        }
        if (known is null)
        {
            if (command.PerformerId is not null) lines.Add("Performer: " + Name(command.PerformerId));
            if (command.TargetId is not null) lines.Add("Target: " + Name(command.TargetId));
        }
        if (command.TargetPlayerId is { } player) lines.Add("Player: " + (player == View.ViewerId ? "you" : "your opponent"));
        if (action.ChoiceLabel is { } chosenText) lines.Add("Use: " + chosenText);
        return lines.ToArray();
    }

    private void RenderFlowSummary(ActionOption action)
    {
        _inspector.AddChild(new HSeparator());
        _inspector.AddChild(Palette.Text("SUMMARY", 11, Palette.Gold));
        foreach (var line in FlowSummaryLines(action)) _inspector.AddChild(Palette.Wrapped("• " + line, 16, Palette.Paper));
    }

    private void AddFlowBack(ActionOption action)
    {
        if (!FlowCanGoBack(action)) return;
        var back = SidebarSkin.Button("← Back", FlowBack); back.Name = "FlowBack"; back.Disabled = !DecisionCanSubmit; _decisionActions.AddChild(back);
    }
}
