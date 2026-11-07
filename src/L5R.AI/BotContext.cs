using L5R.Core;

namespace L5R.AI;

/// <summary>
/// Issue #642: what the bot remembers between decisions of one match, using only what a player at the table could see: a card shown face-up in a
/// public zone stays seen even after it is discarded, removed or returned to a deck, so the bot can reason that a non-Unique card has two more
/// copies somewhere (the usual three-copy limit) and a Unique card has none. It never reads a hidden zone, the seed, or the engine.
///
/// One context belongs to one seat of one match. Choose() works without one (a fresh context is made per call), but then it forgets, so a caller
/// that plays a whole match should keep and pass the same instance.
/// </summary>
public sealed class BotContext
{
    /// <summary>The copy limit assumed for a card that is not Unique.</summary>
    public const int AssumedCopies = 3;

    private readonly Dictionary<string, HashSet<string>> _seenIdsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _uniqueByName = new(StringComparer.Ordinal);
    private int _lobbyTurn = -1;
    private int _favorTakenTurn = -1;
    private int _borderRefreshTurn = -1;

    /// <summary>Issue #1185: the planning bot. True builds a turn plan (intent, Gold shape, purchase slate; TrainingOpponent.Planner.cs) and rebuilds it whenever the table changes. OFF by default: the champion plays as before.</summary>
    public bool UsePlanner { get; init; } = false;

    /// <summary>Issue #1185 (slice 3): ability triage. Targets are ranked by what they can still do (unspent printed abilities, unspent Tacticians, Force), printed abilities in play go before hand cards, and no Gold or card is spent on a battle already comfortably won. Works with or without <see cref="UsePlanner"/>. OFF by default.</summary>
    public bool UsePlannerTriage { get; init; } = false;

    // What each performer has used this turn, remembered from the battle's public action list (the view says nothing about a card's once-per-turn abilities). Keyed by performer NAME,
    // as the list names them: two same-named cards are treated as one, the cautious reading.
    private int _spentTurn = -1;
    private readonly HashSet<string> _spent = new(StringComparer.Ordinal);
    public void NoteBattleActions(MatchView view)
    {
        if (view.BattleActions is not { Length: > 0 } actions) return;
        if (view.TurnNumber != _spentTurn) { _spent.Clear(); _spentTurn = view.TurnNumber; }
        foreach (var a in actions) if (a.Performer is not null) _spent.Add(a.Performer + "|" + a.AbilityId);
    }
    public bool SpentThisTurn(int turn, string performerName, string abilityId) => turn == _spentTurn && _spent.Contains(performerName + "|" + abilityId);

    /// <summary>Issue #1185 ablation switches, meaningful only with <see cref="UsePlanner"/>. PlannerReserve: Gold is kept untapped for Battle tricks (before this turn's attack, and at the end of the turn when an attack on the bot is likely). PlannerDiscard: Mendon's discard rule instead of the champion's.</summary>
    public bool PlannerReserve { get; init; } = true;
    public bool PlannerDiscard { get; init; } = true;

    internal TurnPlan? Plan { get; private set; }
    internal string? PlanKey { get; private set; }
    internal string? PlanGroup { get; private set; }
    /// <summary>Every plan made this match, in order (the trigger that made it is in each): a tournament report or a test can see when the bot re-thought.</summary>
    internal List<TurnPlan> PlanHistory { get; } = [];
    /// <summary>One line per plan made this match: the turn, what made the bot re-think, its intent, the Gold and its shape, and the purchase slate. For tests and tournament reports.</summary>
    public List<string> PlanLog { get; } = [];
    internal void NotePlan(TurnPlan plan, string key, string group)
    {
        Plan = plan; PlanKey = key; PlanGroup = group; PlanHistory.Add(plan);
        PlanLog.Add($"T{plan.Turn} {plan.Trigger}: {plan.Intent}, Gold {plan.Gold} [{string.Join("+", plan.Sources)}], slate [{string.Join(",", plan.Slate)}] ({plan.Why}){(plan.Attack is { } a ? $", attack Province {a.Province + 1} {(a.Raze ? "raze" : "win")} net {a.Estimate.Net} reserve {a.Estimate.Reserve}" : "")}");
    }

    /// <summary>Issue #788: false (the default while the arena results are inconclusive; see docs/parked) plays the bot as it was before the six battle changes (no pass-when-won, target priorities, send-home rule, reaction rule or Border Keep refresh). The arena uses it to measure the change.</summary>
    public bool UseBattlePolicy { get; init; } = false;

    /// <summary>Issue #870 (Mendon's playtest note): Honor the player loses for using an ability is weighed by the deck's Honor tier (a high-Honor deck hardly ever pays it), Honor gain is worth more to a high-Honor deck, and an
    /// Attack that cannot destroy its target is valued only for the rest of its action. An Attack that swings the battle from losing to winning is exempt from the Honor penalty. False plays the bot as it was before.</summary>
    public bool UseHonorAware { get; init; } = true;

    /// <summary>Issue #902 (Mendon's bot notes): general prudence. An effect that hits both sides is played only when the opponent loses clearly more; a bow is aimed at an unbowed card or not played; a claim on the first action of a battle is made only by an Attacker with units on both sides; and in the first turns, while it is still building its economy, Gold is kept for the Dynasty phase instead of spent on cards from the hand. False plays the bot as it was before.</summary>
    public bool UsePrudence { get; init; } = true;

    /// <summary>Issues #1008 and #1015 (Mendon's playtest note): when the attacker's Cavalry could destroy every Province the bot has left, it defends in the Infantry Maneuvers Segment with everything it is offered. Needs <see cref="UseCavalryDefense"/>. False plays the bot as it was before.</summary>
    public bool UseCavalryLastStand { get; init; } = true;

    /// <summary>Issue #998 (Mendon's playtest note): a Battle/Open card is never used when it would do nothing, and is held for the Combat Segment when its effect matters only in a combat (a Combat-Segment-only bow, a Force change). False plays the bot as it was before.</summary>
    public bool UsePhaseChoice { get; init; } = true;

    /// <summary>Issue #997 (Mendon's playtest note): an unopposed battle wastes actions. The defender passes; the attacker acts only to add the Force its Province-destroying total still lacks, and stops once it has it (unless Honor really resolves). False plays the bot as it was before.</summary>
    public bool UseUnopposedGate { get; init; } = true;

    /// <summary>Issue #788 (Mendon's equip changes): attachments valued by their Battle abilities, the additional action an attached Spell grants always used, equipping down to the hand limit once a battle is won, and equipping up to three cards to change a battle's outcome. ON by default, independent of <see cref="UseBattlePolicy"/>.</summary>
    public bool UseEquipPolicy { get; init; } = true;

    /// <summary>Issue #788 (Mendon): true lets the bot attack when it cannot raze a Province but can destroy the defending army (or trade favourably in a tie). OFF by default: the arena found it lost more than it won (docs/parked/2026-10-09-what-makes-a-good-l5r-bot.md).</summary>
    public bool UseTradeAttacks { get; init; } = false;

    /// <summary>Issue #818 (Mendon's eleven bot changes, 2026-10-10): buy-everything and discard-by-affordability in the Dynasty Phase, the catch-up recruit, the overlay limit, lobbying and defending with the Imperial Favor, the attack and attacker-pass rules, Honor attacks, equips that count Force removed, Shinden / optional bows, and never declining an optional destroy or bow. ON by default; the arena switches it off to measure it.</summary>
    public bool UseSpec818 { get; init; } = true;

    /// <summary>Issue #946 (Mendon's defense guidance): when the attacker commits no infantry but has Cavalry at home, the bot weighs the attacker's possible Cavalry army against its own Force (see <see cref="DefensePlanner"/>) and commits its infantry to the most valuable provinces it can hold. ON by default; the arena switches it off to measure it.</summary>
    public bool UseCavalryDefense { get; init; } = true;

    /// <summary>Issue #946 (Mendon): attacking, the bot holds its Cavalry units back in the Infantry Maneuvers Segment and assigns them in the Cavalry Maneuvers Segment, planning the whole army against the defense it can then see. ON by default; the arena switches it off to measure it.</summary>
    public bool UseCavalryAttack { get; init; } = true;

    /// <summary>Issue #963 (bot loop, round 1): order Seppuku only for a Dishonorable Personality, only when the family Honor is low enough for its dishonorable death to threaten the game, and only when it cannot be rehonored by winning a battle this turn. Promoted to ON by default (Mendon, 2026-10-15) after dev 58.5% and held-out 53.8% (docs/bots/experiments). Off: the bot orders it whenever it is offered.</summary>
    public bool UseSeppukuPolicy { get; init; } = true;

    /// <summary>Issue #963 (bot loop, round 2): assign the defense to the battlefield where it wins or saves a Province, and do not spend units where the Province holds on its own. Promoted to ON by default (Mendon, 2026-10-15) after dev 51.4% and held-out 51.4% (docs/bots/experiments). Off: all unbowed Personalities defend the largest visible attacking army.</summary>
    public bool UseInformedDefense { get; init; } = true;

    /// <summary>Issue #963 (bot loop, round 3): while the bot has fewer Personalities than it needs and one it could recruit is face-up in a Province, the Action phase does not spend Gold on cards from the hand (the early-economy rule, kept for the whole game).</summary>
    public bool UseBodiesFirst { get; init; } = false;

    /// <summary>Issue #963 (bot loop, round 4): a Reaction that helps, penalizes, bows or destroys is always played when it is worth anything, instead of being declined by a deterministic roll (30% to 90% chance to play). Off: the roll.</summary>
    public bool UseSureReactions { get; init; } = false;

    /// <summary>The route(s) to victory the bot's own decklist supports (<see cref="DeckArchetype"/>); the caller supplies it, as with <see cref="DeckAveragePersonalHonor"/>. Null: no archetype rule applies.</summary>
    public DeckArchetype? Archetype { get; set; }

    /// <summary>Issue #963 (archetype S1): a deck whose route is dishonor declares an attack only when its army beats the defenders by a clear margin; its engine, not its army, wins the game.</summary>
    public bool UseArchetypeAttack { get; init; } = false;

    /// <summary>Issue #963 (archetype S2): abilities that are the deck's engine (Honor for itself in an honor deck, Honor taken from the opponent in a dishonor deck) are worth more.</summary>
    public bool UseArchetypeAbilities { get; init; } = false;

    /// <summary>Issue #963 (archetype S3): a deck with no honor or dishonor engine does not chase Honor gain; it wins on the battlefield.</summary>
    public bool UseArchetypeMilitary { get; init; } = false;

    /// <summary>Issue #963 (archetype S4): a deck with an honor route sprints when its own Honor is high, a dishonor deck presses when the opponent's is low, instead of the fixed Honor-tier preference.</summary>
    public bool UseArchetypeSwitch { get; init; } = false;

    /// <summary>Issue #963 (hypothesis A): when the attackers are at several of the bot's Provinces, the defenders are split among them where each group wins or saves a Province, instead of all going to one battlefield. Needs <see cref="UseInformedDefense"/>.</summary>
    public bool UseSplitDefense { get; init; } = false;

    /// <summary>Issue #963 (hypothesis B): a deck whose only route is the battlefield (archetype military, no honor secondary) takes the trade attacks <see cref="UseTradeAttacks"/> describes, which the pooled test found to lose; its units exist to fight.</summary>
    public bool UseMilitaryTrades { get; init; } = false;

    /// <summary>The average Personal Honor of the bot's own decklist, when the caller knows it (its own deck is its own information). Null: estimated from cards seen.</summary>
    public double? DeckAveragePersonalHonor { get; init; }

    private int _recruitTurn = -1;
    /// <summary>Remembers that a Personality was recruited this turn (issue #818: the catch-up priority applies to the first one only).</summary>
    public void NoteRecruit(int turn) => _recruitTurn = turn;
    public bool RecruitedThisTurn(int turn) => _recruitTurn == turn;

    /// <summary>Remembers that the Border Keep's refresh was used this turn, so the bot recruits Personalities with the fresh cards (issue #788).</summary>
    public void NoteBorderRefresh(int turn) => _borderRefreshTurn = turn;
    public bool BorderRefreshedRecently(int turn) => _borderRefreshTurn >= 0 && turn - _borderRefreshTurn <= 1;

    /// <summary>Records every face-up card in a public zone of either player. Safe to call on every decision.</summary>
    public void Observe(MatchView view)
    {
        foreach (var player in view.Players)
            foreach (var card in PublicCards(player))
                Note(card);
        foreach (var entry in view.ResolutionArea ?? []) Note(entry.Card); // issue #917: the card being resolved is public from its announcement
    }

    private static IEnumerable<CardView> PublicCards(PlayerView p) =>
        p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame).Concat(p.EventsInPlay ?? []).Concat(p.Celestials ?? []).Concat(p.Underneath ?? [])
            .Concat(p.Provinces.Select(x => x.Card).OfType<CardView>()).Append(p.Stronghold).Concat(p.Hand.Where(c => c.Definition is not null));

    private void Note(CardView card)
    {
        if (card.Definition is not { } def) return;
        if (!_seenIdsByName.TryGetValue(def.Name, out var ids)) _seenIdsByName[def.Name] = ids = new(StringComparer.Ordinal);
        ids.Add(card.Id);
        _uniqueByName[def.Name] = def.Keywords?.Contains("Unique") == true;
    }

    /// <summary>How many different copies of a card (by name) the bot has seen.</summary>
    public int Seen(string cardName) => _seenIdsByName.TryGetValue(cardName, out var ids) ? ids.Count : 0;

    /// <summary>Copies the bot expects could still be hidden somewhere: three minus those seen for a card that is not Unique, one minus those seen for a Unique card, never below zero.</summary>
    public int ExpectedHiddenCopies(string cardName) =>
        Math.Max(0, (_uniqueByName.GetValueOrDefault(cardName) ? 1 : AssumedCopies) - Seen(cardName));

    /// <summary>Records that the bot lobbied for the Imperial Favor on a turn (Lobby is once per turn).</summary>
    public void NoteLobby(int turn) => _lobbyTurn = turn;
    public bool LobbiedThisTurn(int turn) => _lobbyTurn == turn;
    public void NoteFavorTaken(int turn) => _favorTakenTurn = turn;
    public bool TookFavorThisTurn(int turn) => _favorTakenTurn == turn;

    // How often the bot has used the same ability from the same card this turn. Two bots can otherwise trade a free, repeatable action back and forth for ever; each use
    // lowers the next one's score (down to the value of passing, never below), so a loop always ends.
    private int _usesTurn = -1;
    private readonly Dictionary<string, int> _uses = new(StringComparer.Ordinal);

    public void NoteUse(int turn, string? abilityId, string? cardId)
    {
        if (turn != _usesTurn) { _uses.Clear(); _usesTurn = turn; }
        var key = abilityId + "|" + cardId;
        _uses[key] = _uses.GetValueOrDefault(key) + 1;
    }

    public int UsesThisTurn(int turn, string? abilityId, string? cardId) => turn == _usesTurn ? _uses.GetValueOrDefault(abilityId + "|" + cardId) : 0;

    // What the engine refused at the current decision. The engine lists every action a seat could try, and a few it then rejects (a restriction only checked when the
    // command arrives). A player whose action is refused chooses again, so the caller reports the refusal here and calls Choose again at the same version: the bot then skips
    // that exact answer, keeps the other targets of the same action, and drops any unit the refusal names.
    private long _rejectionVersion = -1;
    private readonly List<(GameCommand Command, string Error)> _rejections = [];

    /// <summary>Tells the bot the engine refused a command it chose at <paramref name="version"/>. The memory lasts until the version changes.</summary>
    public void NoteRejection(long version, GameCommand command, string? error)
    {
        if (version != _rejectionVersion) { _rejections.Clear(); _rejectionVersion = version; }
        _rejections.Add((command, error ?? ""));
    }

    internal bool IsRejected(long version, GameCommand command) => version == _rejectionVersion && _rejections.Any(r => SameAnswer(r.Command, command));

    /// <summary>Targets already refused for this ability on this source at this version, so the next try picks another.</summary>
    internal HashSet<string> RejectedTargets(long version, string? abilityId, string? cardId, string type) =>
        version != _rejectionVersion ? [] : _rejections.Where(r => r.Command.Type == type && r.Command.AbilityId == abilityId && r.Command.CardId == cardId && r.Command.TargetId is not null)
            .Select(r => r.Command.TargetId!).ToHashSet(StringComparer.Ordinal);

    internal IEnumerable<string> RejectionMessages(long version, string type) => version != _rejectionVersion ? [] : _rejections.Where(r => r.Command.Type == type).Select(r => r.Error);

    private static bool SameAnswer(GameCommand a, GameCommand b) =>
        a.Type == b.Type && a.AbilityId == b.AbilityId && a.CardId == b.CardId && a.TargetId == b.TargetId && a.Province == b.Province && a.Choice == b.Choice
        && a.TargetPlayerId == b.TargetPlayerId && a.PerformerId == b.PerformerId && a.CostOption == b.CostOption
        && string.Join(",", a.CardIds ?? []) == string.Join(",", b.CardIds ?? [])
        && string.Join(",", (a.Assignments ?? []).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ">" + p.Value)) == string.Join(",", (b.Assignments ?? []).OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + ">" + p.Value))
        && string.Join(",", (a.Selections ?? []).Select(x => x.Slot + ":" + string.Join("+", x.CardIds ?? []) + x.PlayerId + "@" + x.ProvinceOwner + "." + x.Province)) == string.Join(",", (b.Selections ?? []).Select(x => x.Slot + ":" + string.Join("+", x.CardIds ?? []) + x.PlayerId + "@" + x.ProvinceOwner + "." + x.Province));

    /// <summary>A reproducible weight roll in [0, 1): the same match state and the same ability always roll the same, so replays and tests are deterministic.</summary>
    public static double Roll(long version, string? abilityId, string? cardId)
    {
        unchecked
        {
            var hash = 17L;
            hash = hash * 31 + version;
            foreach (var c in abilityId ?? "") hash = hash * 31 + c;
            foreach (var c in cardId ?? "") hash = hash * 31 + c;
            hash ^= hash >> 33; hash *= unchecked((long)0xff51afd7ed558ccdUL); hash ^= hash >> 33;
            return (hash & 0xFFFFFF) / (double)0x1000000;
        }
    }
}
