namespace L5R.Core;

// Battle Phase timing (issues #309/#310). The rules lead's formal Battle Phase, and where each step lives:
//   4.1 the Attacker chooses the battle        SelectBattle (MatchEngine.cs)
//   4.2 Engage actions / "after engaging"      OpenEngageWindow ... PassReaction -> CloseEngageWindow (this file)
//   4.3 the Attacker may play the first action ResolveFirstOpportunity: an attacker-side claim (a Reaction, or Naval
//       Invasion) that no defender-side claim overrode
//   4.4 otherwise the Defender does            the default SelectBattle seeds (priority to 1 - ActivePlayerId)
//   4.5 alternating Battle actions             Pass() (two consecutive passes end the segment)
//   5   before-resolution effects, then Force  ResolveBattle / AdvanceBeforeBattleResolve (MatchEngine.cs)
// The full segment-by-segment conformance table is in docs/decisions/2026-09-18-battle-timing-primitives.md.
public sealed partial class MatchEngine
{
    // ---- 4.2: the Engage window --------------------------------------------------------------------------

    // Opens the window when either player has at least one legal "After engaging" Reaction or "Engage:" action
    // (the same "only open a window worth opening" convention every other Reaction window here uses -- a
    // battle nobody can react to never asks anyone to pass). The turn player (the Attacker) has the first
    // opportunity and the round alternates until both players pass in succession, the core rules' own
    // multiple-Reactions procedure. Returns false, leaving no trace, when nothing could be played.
    private bool OpenEngageWindow(int province)
    {
        var attacker = _state.ActivePlayerId;
        var restorePriority = _state.PriorityPlayerId;
        _state.PendingEngage = new PendingEngage { ProvinceIndex = province, Attacker = attacker, Defender = 1 - attacker };
        _state.PendingChoice = new("engageReaction", attacker, null, $"After engaging at province {province + 1}: play an Engage action or Reaction, or pass.");
        _state.PriorityPlayerId = attacker; _state.ConsecutivePasses = 0;
        if (!Enumerable.Range(0, 2).Any(p => AbilityActions(p).Any(a => a.Enabled)))
        {
            _state.PendingChoice = null; _state.PendingEngage = null; _state.PriorityPlayerId = restorePriority;
            return false;
        }
        Emit("reactionWindow", "Engage reaction window opened.", attacker);
        return true;
    }

    // "Take the first action in battle" claims (spec 4.3/4.4 and Mendon's rulings, issue #310) are recorded as
    // they resolve and decided only when the window closes: claims never stack, and the Defender's claim wins
    // whatever order the claims were made in. beneficiary is who the card GRANTS the opportunity to.
    private void ClaimFirstOpportunity(int beneficiary, string kind, CardInstance? source)
    {
        _state.PendingEngage!.Claims.Add(new(beneficiary, kind, source?.Id));
        Emit("firstOpportunityClaimed", $"{Player(beneficiary).Name} claims the first opportunity to act in this battle{(source is null ? "" : $" ({Definition(source).Name})")}.", beneficiary, source?.Id);
    }

    private static (int Holder, string Reason) ResolveFirstOpportunity(PendingEngage engage)
    {
        var attackerClaims = engage.Claims.Where(c => c.PlayerId == engage.Attacker).ToArray();
        // The Defender using ANY effect that grants them the first opportunity gets it "no matter what": it
        // cancels the Attacker's claim(s) and is itself just the default made explicit when there were none.
        if (engage.Claims.Any(c => c.PlayerId == engage.Defender))
            return (engage.Defender, attackerClaims.Length > 0 ? BattleSteps.ReasonDefenderOverride : BattleSteps.ReasonDefault);
        if (attackerClaims.Length > 0)
            return (engage.Attacker, attackerClaims.All(c => c.Kind == "Naval") ? BattleSteps.ReasonNaval : BattleSteps.ReasonEngageGrant);
        return (engage.Defender, BattleSteps.ReasonDefault);
    }

    // Called by PassReaction once both players have passed in the Engage window: decides who holds the first
    // opportunity, records why (public, MatchView.BattleStep), sets any limit on that first action, and returns
    // the player who now has priority.
    private int CloseEngageWindow()
    {
        var engage = _state.PendingEngage!;
        _state.PendingEngage = null;
        var (holder, reason) = ResolveFirstOpportunity(engage);
        _state.FirstActionPlayerId = holder; _state.FirstActionReason = reason;
        _state.FirstActionRestriction = null;
        _state.AttackerLeadBattle = holder == engage.Attacker ? _state.BattleNumber : -1;
        MarkCombatSegmentBegan(); // Issue #503 (EmperorP5.cs): the Engage window has closed and the Combat Segment begins (Headbutt). // Entrenched Position (Forgotten Legacy pass, issue #467 batch 3)
        Emit("firstOpportunity", $"{Player(holder).Name} has the first opportunity to take a Battle action or pass ({reason}).", holder);
        if (holder == engage.Attacker)
        {
            if (ControlsEventEffect(0, "rebuilding-kyuden-hida") || ControlsEventEffect(1, "rebuilding-kyuden-hida"))
            {
                // "The Attacker may not announce a Battle action before the Defender's first normal opportunity to act or
                // pass" -- the exception the event's own spec named ("an attacker who has the most Naval cards, or plays
                // Sneak Attack, must pass instead"), unreachable until a first-opportunity claim could exist.
                _state.FirstActionRestriction = new(holder, null, true);
                Emit("firstActionLimited", $"{Player(holder).Name} may not announce a Battle action before the Defender's first opportunity (Rebuilding Kyuden Hida): pass.", holder);
            }
            else if (engage.NavalPerformerId is not null)
            {
                _state.FirstActionRestriction = new(holder, engage.NavalPerformerId, false);
                Emit("firstActionLimited", $"{Player(holder).Name}'s first Battle action must be performed by {Definition(Card(engage.NavalPerformerId)).Name} ({FirstPerformerSource()}).", holder, engage.NavalPerformerId); // issue #503: or Scouting the Pass
            }
        }
        // Jade's Roar - exp: the claimant's first action must carry the keyword -- whether they hold the opportunity as the Attacker or as the Defender.
        if (_state.FirstActionRestriction is null && engage.FirstActionKeyword is { } keyword && engage.FirstActionKeywordPlayerId == holder)
        {
            _state.FirstActionRestriction = new(holder, null, false, keyword);
            Emit("firstActionLimited", $"{Player(holder).Name}'s first Battle action must be a {keyword} action.", holder);
        }
        // Shosuro Toson - exp2 (issue #503, EmperorP3.cs): the claimant's first action must be performed by the card that claimed it.
        if (_state.FirstActionRestriction is null && P3FirstActionPerformerLimit(engage, holder) is { } forcedPerformer)
        {
            _state.FirstActionRestriction = forcedPerformer;
            Emit("firstActionLimited", $"{Player(holder).Name}'s first Battle action must be performed by {forcedPerformer.Reason}.", holder, forcedPerformer.PerformerId);
        }
        return holder;
    }

    // "Naval cards" (Naval Invasion): the Naval-keyword cards in a side's army at the battlefield -- its
    // Personalities and every attachment on them, printed or granted (Keyword() covers Suitengu's Path, Scourge of the
    // Sea and Isawa Kojiro's live conditional Naval alike). STRICTLY more than the opposing side is required.
    private int NavalCardCount(int playerId, int province) =>
        Army(playerId, province).Sum(p => (Keyword(p, "Naval") ? 1 : 0) + Attachments(p.Id).Count(a => Keyword(a, "Naval")));

    // ---- the one-shot limit on the first action -----------------------------------------------------------

    // Core rules, Choose Performers: an action on a Personality or a Follower is performed by that card itself, an
    // action on an Item or Spell by the Personality it is attached to, and an ability naming a performer by that
    // card. (ActingPersonality, used elsewhere, also folds a Follower into its host; this one follows the printed rule
    // because Naval Invasion's "which he must perform" turns on it.) Null when no card performs it.
    private CardInstance? ActionPerformerPerRules(GameCommand command)
    {
        if (command.Type != "useAbility" || command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return null;
        var performerId = ability.Performer is not null ? command.PerformerId : command.CardId;
        if (performerId is null || !_state.Cards.TryGetValue(performerId, out var card)) return null;
        return Definition(card).Type is CardType.Item or CardType.Spell && card.Zone == Zone.Attached && card.AttachedTo is string host ? Card(host) : card;
    }

    private string FirstActionLimitMessage(FirstActionRestriction limit) => limit.MustPass
        ? new BarNotice("Announcing a Battle action before the Defender's first normal opportunity to act or pass", "Rebuilding Kyuden Hida").Message
        : limit.ActionKeyword is { } keyword ? new BarNotice($"Starting the battle with anything but a {keyword} action", "Jade's Roar").Message
        : new BarNotice($"Starting the battle with anyone but {Definition(Card(limit.PerformerId)).Name}", limit.Reason ?? FirstPerformerSource()).Message; // issue #503: or Scouting the Pass

    // Applied to a useAbility by ValidateAbility (so offered actions already respect it) and to the two other
    // Battle-action commands by Apply. Ignored inside any reaction window (a Reaction is not the first Battle action).
    private void ValidateFirstActionRestriction(GameCommand command)
    {
        if (_state.FirstActionRestriction is not { } limit || _state.PendingChoice is not null || command.PlayerId != limit.PlayerId) return;
        Require(!limit.MustPass, FirstActionLimitMessage(limit));
        if (limit.ActionKeyword is { } keyword)
        {
            Require(command.Type == "useAbility" && command.AbilityId is { } limitedAbilityId && AbilityCatalog.All.TryGetValue(limitedAbilityId, out var limitedAbility)
                && ActionCarriesKeyword(limitedAbility, command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var limitedSource) ? limitedSource : null, keyword), FirstActionLimitMessage(limit));
            return;
        }
        Require(ActionPerformerPerRules(command)?.Id == limit.PerformerId, FirstActionLimitMessage(limit));
    }

    // ---- the public Battle Phase view ------------------------------------------------------------------------

    private BattleStepView BuildBattleStepView()
    {
        var s = _state;
        string segment, step;
        switch (s.Phase)
        {
            case Phase.AttackDeclaration: (segment, step) = (BattleSteps.Declaration, BattleSteps.DeclareOrDecline); break;
            case Phase.InfantryDeclaration: (segment, step) = (BattleSteps.InfantryManeuvers, BattleSteps.AttackerAssigns); break;
            case Phase.DefenseAssignment: (segment, step) = (BattleSteps.InfantryManeuvers, BattleSteps.DefenderAssigns); break;
            case Phase.CavalryDeclaration: (segment, step) = (BattleSteps.CavalryManeuvers, BattleSteps.AttackerAssigns); break;
            case Phase.CavalryDefense: (segment, step) = (BattleSteps.CavalryManeuvers, BattleSteps.DefenderAssigns); break;
            case Phase.BattleSelection: (segment, step) = (BattleSteps.FightBattles, BattleSteps.ChooseBattle); break;
            case Phase.Battle:
                if (s.PendingEngage is not null) (segment, step) = (BattleSteps.FightBattles, BattleSteps.Engage);
                else if (s.PendingBeforeBattleResolve is not null) (segment, step) = (BattleSteps.Resolution, BattleSteps.BeforeResolution);
                // Once ContinueResolveBattle has compared Force, everything left (a Conqueror choice, the
                // Overconfident draw window, Your Clan Needs You!'s province choice) is the battle's aftermath.
                else if (s.CurrentBattleProvince is null || s.PendingConqueror is not null) (segment, step) = (BattleSteps.Resolution, BattleSteps.Aftermath);
                else (segment, step) = (BattleSteps.FightBattles, BattleSteps.BattleActions);
                break;
            default: (segment, step) = (BattleSteps.None, BattleSteps.None); break;
        }
        var inBattle = segment != BattleSteps.None;
        int? holder = null; string? reason = null; var resolved = false;
        if (s.PendingEngage is { } engage) (holder, reason) = ResolveFirstOpportunity(engage);
        else if (s.Phase == Phase.Battle && s.FirstActionPlayerId is not null) { holder = s.FirstActionPlayerId; reason = s.FirstActionReason; resolved = true; }
        var limit = s.FirstActionRestriction;
        var before = s.PendingBeforeBattleResolve;
        return new BattleStepView(segment, step, inBattle && s.WinnerId is null ? s.PriorityPlayerId : null, inBattle ? s.ActivePlayerId : null,
            s.CurrentBattleProvince,
            holder, reason, resolved, (s.PendingEngage?.Claims ?? []).ToArray(),
            limit is null ? null : limit.MustPass ? BattleSteps.LimitMustPass : limit.ActionKeyword is not null ? BattleSteps.LimitActionKeyword : BattleSteps.LimitPerformerOnly, limit?.PerformerId,
            s.ConsecutivePasses, before is null ? [] : before.RemainingSourceIds.Concat(before.ActiveSourceId is null ? [] : [before.ActiveSourceId]).ToArray(),
            before?.ActiveSourceId, s.PendingChoice?.Type, BuildBeforeResolutionEffects(before));
    }
}
