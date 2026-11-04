namespace L5R.Core;

// Issue #1169: the battle's action record. Every action announced and every pass made while a battle is being fought (Reactions are actions too) is appended to
// MatchState.BattleActions and shown, oldest first, in the battle sidebar. The record is a presentation fact: it is taken from the command as it is announced (who,
// which card, which ability, performer, targets, province/player/Gold chosen) and never decides anything. It is emptied when the next battle starts, so the battle that
// just ended can still be reviewed. Not recorded: a declined Reaction (a decline is not an action), and what the effect did (the engine does not track every bow), so
// the effect line is the ability's own terse text, never a claim about the outcome.

/// <summary>One face-up or face-down card named by an action. Secret is true when the card was face down as the action was announced: only its owner is told its name.</summary>
public sealed record BattleActionTarget(string Name, int OwnerId, bool Secret);

/// <summary>Engine-private record of one action or pass in the battle in progress. Kind is "Action", "Reaction" or "Pass".</summary>
public sealed record BattleActionRecord(int Sequence, string Kind, int PlayerId, string AbilityId, string? SourceCardId, string? Performer,
    BattleActionTarget[] Targets, string[] Chosen);

/// <summary>PUBLIC presentation of a BattleActionRecord. Source is the card the ability is printed on (for an innate action such as Tactical Advantage, the card discarded for it);
/// null for a pass or an action with no card (the Imperial Favor). Brief is the terse effect text, at most BattleActionLog.BriefLimit characters.</summary>
public sealed record BattleActionView(int Sequence, string Kind, int PlayerId, string PlayerName, CardView? Source, string AbilityId, string AbilityLabel, string Brief,
    string? Performer, string[] Targets, string[] Chosen);

public sealed partial class MatchEngine
{
    private bool InBattleRecord => _state.Phase == Phase.Battle && _state.CurrentBattleProvince is not null;

    // Called as an ability is announced, after it validated and before the card leaves its zone (EnterResolutionArea). A rejected command restores the state, record included.
    private void NoteBattleAction(GameCommand command)
    {
        if (!InBattleRecord || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return;
        CardInstance? card = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var c) ? c : null;
        CardInstance? target = command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var t) ? t : null;
        // An innate action (Tactical Advantage) has no card of its own: the card it discards is what lends it.
        var innate = card is null && ability.PlayerAbility && target is not null && (ability.Costs?.Any(x => x.Verb == CostVerbs.Discard && x.Subject == CostSubject.Target) ?? false);
        var source = innate ? target : card;
        CardInstance? performer = command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var p) ? p
            : ability.Performer is null && card is not null && Definition(card).Type == CardType.Personality ? card : null;
        var targets = target is null || innate ? [] : new[] { new BattleActionTarget(Definition(target).Name, target.OwnerId, !target.FaceUp) };
        var chosen = new List<string>();
        if (command.Province is int province) chosen.Add(province < 100 ? $"Province {province + 1}" : "Extra battlefield");
        if (command.TargetPlayerId is int chosenPlayer) chosen.Add(Player(chosenPlayer).Name);
        if (command.GoldAmount is int gold && gold > 0) chosen.Add($"{gold} Gold");
        _state.BattleActions.Add(new(_state.BattleActions.Count + 1, ability.Timing == "Reaction" ? "Reaction" : "Action", command.PlayerId, command.AbilityId, source?.Id,
            performer is null ? null : Definition(performer).Name, targets, [.. chosen]));
    }

    private void NoteBattlePass(int playerId)
    {
        if (!InBattleRecord) return;
        _state.BattleActions.Add(new(_state.BattleActions.Count + 1, "Pass", playerId, "pass", null, null, [], []));
    }

    private BattleActionView[]? BuildBattleActionViews(int viewerId)
    {
        if (_state.BattleActions.Count == 0) return null;
        return _state.BattleActions.Select(r =>
        {
            CardView? source = null;
            if (r.SourceCardId is not null && _state.Cards.TryGetValue(r.SourceCardId, out var card))
            {
                var def = Definition(card);
                source = new(card.Id, Copy(def), card.OwnerId, card.Zone, true, false, null, null, def.Force); // an action's card is public once played
            }
            var known = AbilityCatalog.All.TryGetValue(r.AbilityId, out var ability);
            return new BattleActionView(r.Sequence, r.Kind, r.PlayerId, Player(r.PlayerId).Name, source, r.AbilityId, known ? ability!.Label : "Pass", known ? BattleActionLog.Brief(r.AbilityId, ability!) : "",
                r.Performer, r.Targets.Select(x => x.Secret && x.OwnerId != viewerId ? "a face-down card" : x.Name).ToArray(), r.Chosen);
        }).ToArray();
    }
}

public static class BattleActionLog
{
    /// <summary>The longest effect line the sidebar is built for.</summary>
    public const int BriefLimit = 64;

    // Hand-written terse effect lines, for the abilities whose printed text is long. Anything not listed falls back to the effect half of its printed text (after the last ": ").
    private static readonly Dictionary<string, string> Written = new()
    {
        ["tactical-advantage"] = "Performing Tactician +Force = discarded card's Focus",
        ["ring-of-air-straighten"] = "Straighten his unit",
        ["ring-of-air-recruit"] = "Put into play",
        ["ring-of-earth-recruit"] = "Put into play",
        ["ring-of-fire-recruit"] = "Put into play",
        ["ring-of-void-recruit"] = "Put into play",
        ["ring-of-water-recruit"] = "Put into play",
        ["ring-of-water-move-home"] = "Move home, straighten unit if moved",
        ["ring-of-water-move-battlefield"] = "Move to enemy battlefield, straighten if moved",
        ["ring-of-earth-raise"] = "Province +3 strength",
        ["ring-of-earth-lower"] = "Province -3 strength",
        ["ring-of-fire-destroy"] = "Destroy performer; destroy enemy with ≤ Chi",
        ["ring-of-void-draw"] = "Draw if opponent holds more cards (or in play)",
    };

    /// <summary>The terse effect line for an ability: its hand-written one, else the effect half of its printed text, shortened at a word to BriefLimit characters.</summary>
    public static string Brief(string abilityId, AbilityDefinition ability)
    {
        if (Written.TryGetValue(abilityId, out var written)) return written;
        var label = ability.Label.Trim();
        var colon = label.LastIndexOf(": ", StringComparison.Ordinal);
        var text = colon >= 0 && colon + 2 < label.Length ? label[(colon + 2)..] : label;
        text = text.Replace(" · ", "; ");
        if (text.Length <= BriefLimit) return text;
        var cut = text.LastIndexOf(' ', BriefLimit - 1);
        return text[..(cut > BriefLimit / 2 ? cut : BriefLimit - 1)].TrimEnd(',', ';', ' ', '.') + "…";
    }
}
