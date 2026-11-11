using L5R.Core;

namespace L5R.AI;

// Issue #1185 tuning: further Contender rules, each named in BotContext.Rules and switched on separately so a tournament can measure it (the c2 arm reads them from L5R_C2).
// Every rule reads only the seat's own view and printed card data.
//   econ      Holdings that are fair value (cost at most one Gold above production) are bought first in the early turns, and a province card the bot can pay for with its
//             production next turn is not discarded (Mendon: the bot over-discards; a kept card plans next turn).
//   border    the once-per-game Border Keep refresh is kept for a turn when nothing in the row can be paid for.
//   presence  defending, when an attack would raze a Province and the bot's removals (send home, bow, destroy, -Force) would stop it, the cheapest unit that is needed is sent there:
//             without a unit at the battlefield the bot may take no Battle action at all (the Rule of Presence).
public static partial class TrainingOpponent
{
    private static bool Rule(Ctx x, string name) => x.Memory.Has(name);

    /// <summary>A numeric knob given as a rule "name=value" (tuning sweeps); the default when absent.</summary>
    private static double Knob(Ctx x, string name, double fallback)
    {
        foreach (var rule in x.Memory.Rules)
            if (rule.Length > name.Length + 1 && rule.StartsWith(name, StringComparison.Ordinal) && rule[name.Length] == '=' && double.TryParse(rule[(name.Length + 1)..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
        return fallback;
    }

    private static bool FairHolding(CardDefinition def) => def.Type == CardType.Holding && def.GoldCost <= def.GoldProduction + 1;

    private static bool FairHolding(Ctx x, CardDefinition def) => def.Type == CardType.Holding && def.GoldCost <= def.GoldProduction + Knob(x, "fair", 1);

    private static int Holdings(PlayerView p) => p.Home.Count(c => c.Definition?.Type == CardType.Holding);

    /// <summary>The Contender's further rules over an action's final score.</summary>
    private static double Contender2Score(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (x.Memory.Rules.Count == 0) return score;
        var scored = Contender2Rules(x, action, command, score);
        // keepup (Mendon's Crane game, 2026-11-09): an ability that bows its performer is performed by a unit that cannot straighten others while one that can is offered too; the
        // straightener stays up to straighten the performer and use the ability again (Nanae straightening Yasutora for Shinden Asahina). It only orders the performers.
        if (Rule(x, "keepup") && action.Type == "useAbility" && command.PerformerId is { } performerId && command.AbilityId is { } bowingId && AbilityCatalog.All.TryGetValue(bowingId, out var bowing)
            && (bowing.Costs ?? []).Any(c => c.Verb == CostVerb.Bow) && Straightener(x, performerId)
            && x.Actions.Any(a => a.Enabled && a.Command is { PerformerId: { } other } c && other != performerId && c.AbilityId == bowingId && c.CardId == command.CardId && !Straightener(x, other)))
            return scored - 5;
        return scored;
    }

    private static double Contender2Rules(Ctx x, ActionOption action, GameCommand command, double score)
    {
        // nofoeboost (Mendon's Lion game, 2026-11-09): a helping ability any Personality may receive (Akodo Kamina's +4F) is never given to an opponent's card.
        if (Rule(x, "nofoeboost") && action.Type == "useAbility" && AbilityIntent.Of(command.AbilityId) is { Rank: EffectRank.Help, Side: not TargetSide.Enemy }
            && x.Card(command.TargetId) is { } helped && helped.OwnerId == x.Opponent.Id) return Math.Min(score, SelfHarmScore);
        // nobowgift (Mendon's Phoenix game, 2026-11-09: seven times he let the opponent gain the Honor rather than bow one of his Personalities): a choice to bow one of the bot's own
        // Personalities or let the opponent gain Honor gives the Honor while the opponent stays well short of an Honor victory.
        if (Rule(x, "nobowgift") && action.Type == "chooseDecider" && x.View.PendingChoice is { Type: "deciderChoice", Prompt: { } asked } && GiftHonor.Match(asked) is { Success: true } gift
            && x.Opponent.Honor + int.Parse(gift.Groups[1].Value) < Knob(x, "nbgH", 34))
            return command.Choice == "done" ? Math.Max(score, 150) : x.Card(command.TargetId) is { OwnerId: var mine } && mine == x.Self.Id ? Math.Min(score, 40) : score;
        // favorshame (Mendon's Crab game, 2026-11-09: eleven times he spent the Favor to cost the opponent Honor where the bot would have spent it to draw): of the bot's Favor-spending
        // abilities, one that costs the opponent Honor goes before one that only draws, unless the bot is racing for its own Honor.
        if (Rule(x, "favorshame") && action.Type == "useAbility" && AbilityIntent.Of(command.AbilityId) is { DiscardsFavor: true } spend && x.View.FavorPlayerId == x.Self.Id && !WantsOwnGain(x))
            return spend.HonorLoss && !spend.SelfHonorLoss && (x.Card(command.TargetId) is not { } shamed || shamed.OwnerId == x.Opponent.Id) ? Math.Max(score, 145) : spend.Draws && !spend.HonorLoss ? Math.Min(score, 135) : score;
        // dhdrive: once the opponent is down to dhH Honor, an ability that costs it Honor goes before drawing, the Favor and passing.
        if (Rule(x, "dhdrive") && action.Type == "useAbility" && x.Opponent.Honor <= Knob(x, "dhH", 10) && AbilityIntent.Of(command.AbilityId) is { HonorLoss: true, SelfHonorLoss: false, TakesFavor: false }
            && x.Card(command.TargetId) is { } pressed && pressed.OwnerId == x.Opponent.Id)
            return Math.Max(score, 150);
        // duelodds (Mendon's Scottish Lion game, 2026-11-09: the Champion challenged Akodo Dairuko three times with weaker duelists and lost every duel): a duel is only issued against a
        // Personality whose duel stat is below the challenger's (by doM); a Duelist wins ties against a non-Duelist (Mendon), so a Duelist may also challenge a non-Duelist it ties.
        if (Rule(x, "duelodds") && action.Type == "useAbility" && command.AbilityId is { } duelId && AbilityCatalog.All.TryGetValue(duelId, out var duelAbility) && duelAbility.IssuesDuel
            && duelAbility.DuelStat is "Chi" or "Force" && x.Card(command.PerformerId ?? command.CardId) is { Definition.Type: CardType.Personality } challenger
            && x.Card(command.TargetId) is { Definition.Type: CardType.Personality } challenged && challenged.OwnerId == x.Opponent.Id
            && DuelStatOf(challenger, duelAbility.DuelStat) - DuelStatOf(challenged, duelAbility.DuelStat) < Knob(x, "doM", 1) - (IsDuelist(challenger) && !IsDuelist(challenged) ? 1 : 0))
            return Math.Min(score, BarredScore);
        // duelplay (Mendon's Scottish Lion game, 2026-11-09: ahead on the duel stat he struck at once and kept his cards): strike while the bot's total beats the opponent's (its unseen
        // focused cards counted at dsE each), otherwise focus the highest Focus Value.
        if (Rule(x, "duelplay") && action.Type is "focus" or "strike" && x.View.PendingDuel is { Stage: "Focusing" } duel)
        {
            var challenging = duel.ChallengerPlayerId == x.Self.Id;
            if (x.Card(challenging ? duel.ChallengerId : duel.ChallengedId) is { } mineCard && x.Card(challenging ? duel.ChallengedId : duel.ChallengerId) is { } theirCard)
            {
                var mine = DuelStatOf(mineCard, duel.Stat) + (x.Self.Focused ?? []).Sum(c => c.Definition?.Focus ?? 0);
                var theirs = DuelStatOf(theirCard, duel.Stat) + Knob(x, "dsE", 3) * x.Opponent.FocusCount + Knob(x, "dsM", 3);
                var ahead = mine > theirs || mine == theirs && IsDuelist(mineCard) && !IsDuelist(theirCard);
                if (action.Type == "strike" && ahead) return 300;
            }
        }
        // favorhold (Mendon's games, 2026-11-09: in battles he was already winning he held the Favor where the bot spent it): the Favor's Battle action waits while the bot already meets
        // its goal at this battle with fhM to spare.
        if (Rule(x, "favorhold") && action.Type == "useAbility" && command.AbilityId == "favor-battle" && x.Odds is { Satisfied: true } held && held.Margin >= Knob(x, "fhM", 2))
            return Math.Min(score, PassScore - 1);
        // selfplayer (Mendon's Unicorn game, 2026-11-09): an ability that names a player is offered once for each player; an Honor loss aimed at the bot, or a gain aimed at its opponent, only
        // helps the opponent (the self-harm test reads the target card, which these have none of).
        if (Rule(x, "selfplayer") && action.Type == "useAbility" && command.TargetPlayerId is int aimed && AbilityIntent.Of(command.AbilityId) is { } aimIntent && aimIntent.HonorGain != aimIntent.HonorLoss
            && aimed == (aimIntent.HonorLoss ? x.Self.Id : x.Opponent.Id)) return Math.Min(score, SelfHarmScore);
        // selfplayer: of a card's gain-for-the-bot and loss-for-the-opponent, the one the Honor race wants (an Honor deck near 40 gains) is taken while the other is offered too.
        if (Rule(x, "selfplayer") && action.Type == "useAbility" && command.TargetPlayerId is int toward && AbilityIntent.Of(command.AbilityId) is { } modeIntent && modeIntent.HonorGain != modeIntent.HonorLoss
            && modeIntent.HonorGain != WantsOwnGain(x) && x.Actions.Any(a => a.Enabled && a.Command is { TargetPlayerId: int other } c && c.CardId == command.CardId && c.AbilityId != command.AbilityId
                && AbilityIntent.Of(c.AbilityId) is { } alt && alt.HonorGain == WantsOwnGain(x) && alt.HonorGain != alt.HonorLoss && other == (alt.HonorGain ? x.Self.Id : x.Opponent.Id)))
            return Math.Min(score, BarredScore);
        // firstbody: short of bodies (none, or fewer than the opponent) from its second turn on, the best affordable Personality is bought before any Holding: provinces with nobody to
        // defend them are what the opponent's first attack takes.
        if (Rule(x, "firstbody") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { Type: CardType.Personality } body && x.Self.TurnsTaken >= 2
            && Personalities(x.Self) < Math.Max(1, Personalities(x.Opponent)) && !x.Memory.RecruitedThisTurn(x.View.TurnNumber))
            return SlateBonus + 100 + body.Force * 3 + score * 0.0001;
        // threatbody: against an opponent whose Personalities outnumber the bot's and whose army already beats everything the bot could defend with plus its weakest Province, a Personality is
        // bought before any Holding (the early razes against fast decks came with the bot still buying its economy and nobody home).
        if (Rule(x, "threatbody") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { Type: CardType.Personality } guard && Threatened(x)
            && (Rule(x, "tb2") || !x.Memory.RecruitedThisTurn(x.View.TurnNumber)))
            return SlateBonus + 100 + guard.Force * 3 + score * 0.0001;
        // bodyfirst (all six of Mendon's games, 2026-11-09: 21 times he bought a Personality where the bot bought a Holding): once the bot has bfH Holdings, a Holding waits while an
        // affordable Personality is in its provinces.
        if (Rule(x, "bodyfirst") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { Type: CardType.Holding } && Holdings(x.Self) >= Knob(x, "bfH", 3)
            && ProvinceCards(x).Any(c => c.Definition!.Type == CardType.Personality && EffectiveCost(x, c) <= Ready(x)))
            return Math.Min(score, 60);
        // lkPosture: under the bodies posture (Defend) the economy-first rule stands aside; the posture's own preference decides.
        if (Rule(x, "econ") && action.Type == "purchase" && x.Card(command.CardId)?.Definition is { } bought && FairHolding(x, bought) && !(Rule(x, "threatbody") && Threatened(x))
            && !(x.Memory.Posture is { Intent: not PlanIntent.Develop } posture && x.View.TurnNumber <= posture.Until) && (Holdings(x.Self) < Knob(x, "econN", 8) && x.Self.TurnsTaken <= Knob(x, "econT", Rule(x, "econT2") ? 2 : 4) || Rule(x, "econ2") && Holdings(x.Self) < 10)
            && !(Rule(x, "body1") && Personalities(x.Self) == 0 && x.Self.TurnsTaken >= 2 && ProvinceCards(x).Any(c => c.Definition!.Type == CardType.Personality && EffectiveCost(x, c) <= Ready(x))))
            return SlateBonus + 50 + 5 * (bought.GoldProduction - bought.GoldCost) + score * 0.0001;
        if (Rule(x, "econ") && action.Type == "discardProvince" && x.Card(command.CardId)?.Definition is { } kept)
        {
            // discardbad: with six Holdings, one that costs two or more above its production only holds the slot a Personality could fill.
            if (Rule(x, "discardbad") && kept.Type == CardType.Holding && kept.GoldCost >= kept.GoldProduction + 2 && Holdings(x.Self) >= 6) return 20;
            // nokeepH / nokeepP / nokeep: the champion's discard decides for Holdings, Personalities or both (the protection keeps the row from cycling: the Contender discarded 0.5 cards a game, the champion 4).
            // keepready: a Personality is kept only when this turn's unbowed Gold could buy it, not the whole capacity.
            if (FairHolding(kept) && Holdings(x.Self) < 8 && !Rule(x, "nokeepH") && !Rule(x, "nokeep")) return -5;
            if (kept.Type == CardType.Personality && EffectiveCost(x, x.Card(command.CardId)!) <= (Rule(x, "keepready") ? Ready(x) : Capacity(x)) && !Rule(x, "nokeepP") && !Rule(x, "nokeep")) return -5;
        }
        if (Rule(x, "border") && action.Type == "useAbility" && command.AbilityId == "border-game"
            && ProvinceCards(x).Any(c => EffectiveCost(x, c) <= Ready(x) && (c.Definition!.Type == CardType.Personality || FairHolding(c.Definition))))
            return Math.Min(score, BarredScore);
        // honorclock: "gain Honor or make the opponent lose it" goes to whichever matters more: the side nearer an Honor victory.
        if (Rule(x, "honorclock") && action.Type == "chooseEffect" && command.Choice is "self-gain" or "opponent-lose" && 40 - x.Opponent.Honor < 40 - x.Self.Honor)
            return command.Choice == "opponent-lose" ? 140 : 70;
        // holdcap: with production past what the deck can spend, a Holding buys nothing more; it is a province slot that could hold a Personality.
        if (Rule(x, "holdcap") && Capacity(x) >= 20 && x.Card(command.CardId)?.Definition is { Type: CardType.Holding })
        {
            if (action.Type == "purchase") return -10;
            if (action.Type == "discardProvince") return 20;
        }
        // shinden: 1 Honor is cheap next to a bowed Personality (Mendon: the bot bowed its main unit and lost the next battle); only an opponent near an Honor victory changes that.
        if (Rule(x, "shinden") && action.Type == "chooseShinden" && x.Opponent.Honor < 34) return command.Choice == "honor" ? 120 : 60;
        // openbow: an Open-phase bow of an enemy Personality is worth most on the opponent's turn, when it stops that unit attacking (the bot's own cost, a bowed performer or spent Gold,
        // matters least then); on the bot's own turn only an attack it is about to make justifies it.
        if (Rule(x, "openbow") && action.Type == "useAbility" && x.View.Phase == Phase.Action && x.View.PendingChoice is null && command.AbilityId is { } bowId
            && AbilityCatalog.All.TryGetValue(bowId, out var bowAbility) && bowAbility.Timing is "Open" or "BattleOrOpen" && AbilityIntent.Of(bowAbility) is { IsReaction: false } bowIntent
            && (bowIntent.Rank == EffectRank.Bow && bowIntent.Side != TargetSide.Own || bowId == "shinden-choice") && x.Card(command.TargetId) is { Bowed: false, Definition.Type: CardType.Personality } bowed && bowed.OwnerId != x.Self.Id)
            return x.View.ActivePlayerId == x.Self.Id ? (DeclareScore(x) > PassScore ? score : Math.Min(score, BarredScore)) : Math.Max(score, 200 + UnitForceOf(x, bowed));
        if (action.Type is "useAbility" or "playStrategy" or "equip" && Contender2Bar(x, action, command)) return Math.Min(score, BarredScore);
        // cutoff (Mendon, 2026-11-09): printed abilities in play go first, but when the opponent can cut off enough of the bot's removals that only one would remain for the unit that decides
        // the battle, that unit is removed now, with whatever can do it.
        if (Rule(x, "cutoff") && x.View.Phase == Phase.Battle && action.Type is "useAbility" or "playStrategy" && CutoffPriority(x, command) is { } urgent) return Math.Max(score, urgent);
        // conf (Mendon, 2026-11-09): with low confidence, the hardest threat takes the biggest hit (destroy, then send home, then bow); with high confidence, the cheapest removal that does
        // the job: a card from hand waits while a printed ability in play reaches the same unit, and a destroy waits while a bow does as much (no attachments to strip). Cards in hand are options.
        if (Rule(x, "conf") && x.View.Phase == Phase.Battle && action.Type is "useAbility" or "playStrategy" && ConfidenceScore(x, action, command, score) is { } judged) return judged;
        // nogift: a defense that loses the battle hands an Honor deck Honor for every card destroyed; with Provinces to spare it is not made (a presence defense is judged by its own rule).
        if (Rule(x, "nogift") && action.Type == "assignDefense" && command.Assignments is { Count: > 0 } given && x.Opponent.Honor >= Knob(x, "nogiftH", 20) && x.Self.Provinces.Count(p => !p.Destroyed) > 2
            && !(Rule(x, "presence") && PresencePlan(x, action) is { } presenceKept && SameAssignments(presenceKept, given))
            && given.GroupBy(p => p.Value).Any(g => DefendersForce(x, g.Select(p => p.Key)) <= AttackersAt(x, g.Key)))
            return Math.Min(score, BarredScore);
        if (Rule(x, "presence") && action.Type == "assignDefense" && PresencePlan(x, action) is { } plan && SameAssignments(plan, command.Assignments)) return Math.Max(score, 130);
        // hostilehand: a defense is judged after the attacker's hand has removed the strongest defenders; one that then neither wins nor saves the Province only feeds it Personalities.
        // aggro: an army that beats its Province's Strength and, on its own, every unbowed enemy defender is sent: the champion defends only where it wins, so it must commit or lose the Province.
        // aggro2: beating the Strength alone is enough.
        if ((Rule(x, "aggro") || Rule(x, "aggro2") || Rule(x, "raid") && Undefended(x)) && action.Type == "attack" && command.Assignments is { Count: > 0 } && score < 85)
        {
            var defence = ArmyForce(x.Opponent.Home);
            var armies = WholeAttack(x, action, command.Assignments).GroupBy(p => p.Value)
                .Select(g => (Province: x.Opponent.Provinces.FirstOrDefault(p => p.Index == g.Key), Force: g.Sum(p => ArmyForce(x.Self.Home.Concat(x.Self.Army).Where(c => c.Id == p.Key || c.AttachedTo == p.Key))))).ToArray();
            var boost = (Rule(x, "raidboost") || Rule(x, "raid")) && Undefended(x) ? RaidBoost(x, command.Assignments.Keys.Select(x.Card).OfType<CardView>().ToArray()) : 0;
            if (armies.All(a => a.Province is { Destroyed: false } && a.Force + boost > a.Province.Strength && (Rule(x, "aggro2") || Rule(x, "raid") || a.Force > defence))) return 85;
        }
        if ((Rule(x, "aggro") || Rule(x, "aggro2") || Rule(x, "raid") && Undefended(x)) && action.Type == "declareAttack" && score < 85 && AggroAttack(x, x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray(),
                x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray()) is not null) return 85;
        // hostileatk: an army the opponent could defend against is judged after its hand has removed the strongest attackers; if it would then lose to the defenders, it is not sent.
        if (Rule(x, "hostileatk") && action.Type == "attack" && command.Assignments is { Count: > 0 } && !HonorRace(x) && !HostileAttackHolds(x, WholeAttack(x, action, command.Assignments))) return Math.Min(score, BarredScore);
        if (Rule(x, "hostilehand") && action.Type == "assignDefense" && command.Assignments is { Count: > 0 } defense && !HostileDefenseHolds(x, defense)) return Math.Min(score, BarredScore);
        return score;
    }

    /// <summary>How many of the bot's units the opponent's hand may remove at one battle: its hand sized by the share of Battle-timed Strategies among the Fate cards it has shown,
    /// at most two per attacking Personality there (each removal needs a performer).</summary>
    private static int HandRemovals(Ctx x, int performers)
    {
        var seen = x.Opponent.Discard.Concat(x.Opponent.RemovedFromGame).Where(c => c.Definition is { } d && d.Type is CardType.Strategy or CardType.Follower or CardType.Item or CardType.Spell or CardType.Ring).ToArray();
        var share = seen.Length >= 3 ? seen.Count(c => c.Definition!.Type == CardType.Strategy && BattlePolicy.Printed(c).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen")) / (double)seen.Length : UnseenStrategyShare;
        return (int)Math.Min((Rule(x, "hh1") ? 1 : 2) * performers, Math.Ceiling(x.Opponent.HandCount * share));
    }

    /// <summary>
    /// deter: the champion attacks only with an army that beats the unbowed Force at the defender's home. When the attack has spare units, the weakest are kept home until that home Force
    /// reaches the opponent's whole army (bowed or not: it straightens), provided every army still beats its Province's Strength plus every enemy defender by a margin. Null when that cannot be done.
    /// </summary>
    private static Dictionary<string, int>? DeterPlan(Ctx x, Dictionary<string, int> attack)
    {
        if (HonorRace(x)) return null;
        var enemyArmy = x.Opponent.Home.Concat(x.Opponent.Army).Where(c => c.Definition?.Type is CardType.Personality or CardType.Follower or CardType.Item).Sum(c => Math.Max(0, c.Force)) + 2;
        var defence = ArmyForce(x.Opponent.Home);
        var unitForce = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToDictionary(c => c.Id, c => UnitForce(x, c), StringComparer.Ordinal);
        var home = unitForce.Where(p => !attack.ContainsKey(p.Key)).Sum(p => p.Value);
        if (home >= enemyArmy) return null; // already deterred
        var plan = new Dictionary<string, int>(attack, StringComparer.Ordinal);
        int ArmyAt(int province) => plan.Where(p => p.Value == province).Sum(p => unitForce.GetValueOrDefault(p.Key));
        int Need(int province) => (x.Opponent.Provinces.FirstOrDefault(p => p.Index == province)?.Strength ?? 99) + defence + 3;
        foreach (var unit in attack.Keys.OrderBy(id => unitForce.GetValueOrDefault(id)).ThenBy(id => id, StringComparer.Ordinal))
        {
            if (home >= enemyArmy) break;
            var province = plan[unit];
            if (ArmyAt(province) - unitForce.GetValueOrDefault(unit) < Need(province)) continue;
            plan.Remove(unit); home += unitForce.GetValueOrDefault(unit);
        }
        return home >= enemyArmy && plan.Count > 0 ? plan : null;
    }

    /// <summary>aggro: every offered unit at the weakest Province, when that army beats its Strength (and, for aggro, every unbowed enemy defender); null otherwise.</summary>
    private static Dictionary<string, int>? AggroAttack(Ctx x, CardView[] units, int[] provinces)
    {
        if (units.Length == 0 || HonorRace(x)) return null;
        var weakest = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderBy(p => p.Strength).ThenBy(p => p.Index).FirstOrDefault();
        if (weakest is null) return null;
        var force = units.Sum(u => UnitForce(x, u)) + AlreadyAssigned(x).Where(c => c.ProvinceIndex == weakest.Index).Sum(u => UnitForce(x, u))
            + ((Rule(x, "raidboost") || Rule(x, "raid")) && Undefended(x) ? RaidBoost(x, units) : 0);
        if (force <= weakest.Strength || !Rule(x, "aggro2") && !Rule(x, "raid") && force <= ArmyForce(x.Opponent.Home)) return null;
        return units.ToDictionary(u => u.Id, _ => weakest.Index, StringComparer.Ordinal);
    }

    /// <summary>raidboost (Mendon's Lion game, 2026-11-09: a lone Tactician razed an undefended Province on turn 3): the Force an unopposed attack can still add from the hand,
    /// one Tactical Advantage per unspent Tactician (the best Focus Value in hand, each card used once).</summary>
    private static readonly System.Text.RegularExpressions.Regex GiftHonor = new(@"if you choose none, .* gains (\d+) Honor", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool IsDuelist(CardView card) => card.Definition?.Keywords?.Contains("Duelist", StringComparer.OrdinalIgnoreCase) == true
        || card.GrantedKeywords?.Any(k => k.Keyword.Equals("Duelist", StringComparison.OrdinalIgnoreCase)) == true;

    private static int DuelStatOf(CardView card, string stat) => stat == "Force" ? card.Force : card.Chi;

    private static bool Undefended(Ctx x) => !x.Opponent.Home.Any(c => c.Definition?.Type == CardType.Personality && !c.Bowed);

    private static int RaidBoost(Ctx x, CardView[] units)
    {
        var tacticians = units.Count(u => u.Definition?.Keywords?.Contains("Tactician", StringComparer.OrdinalIgnoreCase) == true && !x.Memory.SpentThisTurn(x.View.TurnNumber, u.Definition!.Name, "tactical-advantage"));
        return x.Self.Hand.Select(c => c.Definition?.Focus ?? 0).OrderByDescending(f => f).Take(tacticians).Sum();
    }

    /// <summary>Whether every army of an attack still beats every unbowed enemy defender once the opponent's hand has removed its strongest units. Undefended homes always hold.</summary>
    private static bool HostileAttackHolds(Ctx x, Dictionary<string, int> attack)
    {
        var defence = ArmyForce(x.Opponent.Home);
        var defenders = x.Opponent.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed);
        if (defence == 0 || defenders == 0) return true;
        var removals = HandRemovals(x, defenders);
        foreach (var army in attack.GroupBy(p => p.Value))
        {
            var forces = army.Select(p => ArmyForce(x.Self.Home.Concat(x.Self.Army).Where(c => c.Id == p.Key || c.AttachedTo == p.Key))).OrderByDescending(f => f).Skip(removals).Sum();
            if (forces <= defence) return false;
        }
        return true;
    }

    /// <summary>Whether every group of a defense still wins its battle or saves its Province once the opponent's hand has removed its strongest units. A last stand always holds.</summary>
    private static bool HostileDefenseHolds(Ctx x, Dictionary<string, int> defense)
    {
        if (x.Self.Provinces.Count(p => !p.Destroyed) <= 1 || LastStandProvince(x, x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray()) is not null) return true;
        foreach (var group in defense.GroupBy(p => p.Value))
        {
            var province = x.Self.Provinces.FirstOrDefault(p => p.Index == group.Key);
            if (province is null) continue;
            var attackers = AttackersAt(x, group.Key);
            if (attackers == 0) continue;
            var performers = x.Opponent.Army.Count(c => c.ProvinceIndex == group.Key && c.Definition?.Type == CardType.Personality);
            var forces = group.Select(p => DefendersForce(x, [p.Key])).OrderByDescending(f => f).Skip(HandRemovals(x, performers)).Sum();
            if (!(forces > attackers) && !(attackers > province.Strength && attackers <= province.Strength + forces)) return false;
        }
        return true;
    }

    private static bool SameAssignments(Dictionary<string, int> a, Dictionary<string, int>? b) => b is not null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var v) && v == p.Value);

    // ---- presence defense -------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>A removal the bot could take as a defender once present: how much attacking Force it takes away (Full: the largest attacking unit), and its Gold.</summary>
    private sealed record Removal(bool Full, int Minus, int Cost);

    private static readonly HashSet<string> AttackerTargets = new(StringComparer.Ordinal) { "attacking", "enemy-attacking", "enemy-attacking-card", "enemy-personality", "enemy-unit", "enemy-force", "enemy-card" };

    /// <summary>cutoff: the enemy unit whose removal turns the battle (defending: saves the Province; attacking: razes it or stops a loss), the largest first; null when none does.</summary>
    private static CardView? DecisiveThreat(Ctx x, BattlePolicy.Odds odds)
    {
        var tier = odds.Tier();
        if (odds.IsAttacker ? tier >= 3 : tier >= 1) return null;
        return EnemyArmyAt(x).Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).OrderByDescending(c => UnitForceOf(x, c)).ThenBy(c => c.Id, StringComparer.Ordinal)
            .FirstOrDefault(c => (odds with { Enemy = odds.Enemy - UnitForceOf(x, c) }).Tier() > tier);
    }

    /// <summary>cutoff: whether an offered command bows, destroys, sends home or duels the given unit.</summary>
    private static bool RemovesUnit(Ctx x, string type, GameCommand command, string unitId, string[]? legal)
    {
        var intent = type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        if (intent.IsReaction || intent.Side == TargetSide.Own) return false;
        var duel = command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.IssuesDuel;
        if (!(intent.Rank is EffectRank.Destroy or EffectRank.Bow || BattlePolicy.SendsHome(command.AbilityId) || duel)) return false;
        // A bow that is the ability's cost ("Bow Kouki · enemy −3 Force") is not what it does to the target.
        if (command.AbilityId is { } costId && AbilityCatalog.All.TryGetValue(costId, out var costed) && (costed.Costs ?? []).Any(c => c.Verb == CostVerb.Bow) && costed.Label is { } label
            && label.LastIndexOf('·') is var dot and >= 0 && !RemovalWords.IsMatch(label[(dot + 1)..])) return false;
        return command.TargetId == unitId || legal?.Contains(unitId) == true;
    }

    /// <summary>cutoff: the score that puts this command first, or null. The bot's removals that can reach the deciding unit, less the cut-offs the opponent can make (its unbowed units at the battle
    /// with an unused threatening ability, and a share of its hand's expected removals), leave one or none: then it is now.</summary>
    private static double? CutoffPriority(Ctx x, GameCommand command)
    {
        if (x.Odds is not { } odds || DecisiveThreat(x, odds) is not { } threat || command.TargetId != threat.Id) return null;
        var offered = x.Actions.Where(a => a.Enabled && a.Command is { } && a.Type is "useAbility" or "playStrategy").ToArray();
        var mine = offered.FirstOrDefault(a => a.Command!.AbilityId == command.AbilityId && a.Command.CardId == command.CardId);
        if (mine is null || !RemovesUnit(x, mine.Type, command, threat.Id, null)) return null;
        var sources = offered.Where(a => RemovesUnit(x, a.Type, a.Command!, threat.Id, a.TargetIds)).Select(a => a.Command!.PerformerId ?? a.Command.CardId).Distinct().Count();
        var turn = x.View.TurnNumber;
        var enemy = EnemyArmyAt(x).Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && !c.ActionLocked).ToArray();
        var inPlay = enemy.Count(c => BattlePolicy.Printed(c).Any(p => TimedAbilities.Contains(p.Ability.Timing ?? "") && BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent) && !x.Memory.SpentThisTurn(turn, c.Definition!.Name, p.Id)));
        var cuts = inPlay + Knob(x, "cuth", 50) / 100.0 * HandRemovals(x, enemy.Length);
        return sources - cuts <= Knob(x, "cutk", 1) ? 160 : null;
    }

    /// <summary>keepup: the bot's unit has a printed ability, not yet used this turn, that straightens a unit other than itself.</summary>
    private static bool Straightener(Ctx x, string cardId) => x.Card(cardId) is { Definition: not null } card && card.OwnerId == x.Self.Id
        && BattlePolicy.Printed(card).Any(p => StraightenText.IsMatch(AbilityIntent.EffectText(p.Ability.Label ?? "")) && p.Intent.Side != TargetSide.Enemy && !x.Memory.SpentThisTurn(x.View.TurnNumber, card.Definition!.Name, p.Id));

    /// <summary>The enemy units with an unused printed Battle ability that threatens the bot's units (bow, send home, destroy, duel).</summary>
    private static int EnemyRemovalSources(Ctx x, IEnumerable<CardView> enemy)
    {
        var turn = x.View.TurnNumber;
        return enemy.Count(c => BattlePolicy.Printed(c).Any(p => TimedAbilities.Contains(p.Ability.Timing ?? "") && BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent) && !x.Memory.SpentThisTurn(turn, c.Definition!.Name, p.Id)));
    }

    /// <summary>conf: the bot's removals that reach the deciding unit, less the opponent's expected cut-offs (see <see cref="CutoffPriority"/>); null when no unit decides the battle.</summary>
    private static double? Confidence(Ctx x, CardView threat)
    {
        var offered = x.Actions.Where(a => a.Enabled && a.Command is { } && a.Type is "useAbility" or "playStrategy").ToArray();
        var sources = offered.Where(a => RemovesUnit(x, a.Type, a.Command!, threat.Id, a.TargetIds)).Select(a => a.Command!.PerformerId ?? a.Command.CardId).Distinct().Count();
        var turn = x.View.TurnNumber;
        var enemy = EnemyArmyAt(x).Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && !c.ActionLocked).ToArray();
        var inPlay = enemy.Count(c => BattlePolicy.Printed(c).Any(p => TimedAbilities.Contains(p.Ability.Timing ?? "") && BattlePolicy.IsThreatening(p.Id, p.Ability, p.Intent) && !x.Memory.SpentThisTurn(turn, c.Definition!.Name, p.Id)));
        return sources - inPlay - Knob(x, "cuth", 50) / 100.0 * HandRemovals(x, enemy.Length);
    }

    private static bool FromHand(Ctx x, GameCommand command) => x.Card(command.CardId) is not { } source || source.OwnerId != x.Self.Id || source.Zone == Zone.Resolving || x.Self.Hand.Any(h => h.Id == source.Id);

    /// <summary>conf: how hard a removal hits: destroy, send home, duel, bow.</summary>
    private static int HitWeight(Ctx x, string type, GameCommand command)
    {
        var intent = type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        return intent.Rank == EffectRank.Destroy ? 8 : BattlePolicy.SendsHome(command.AbilityId) ? 6 : command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var a) && a.IssuesDuel ? 4 : 2;
    }

    private static double? ConfidenceScore(Ctx x, ActionOption action, GameCommand command, double score)
    {
        if (x.Odds is not { } odds || DecisiveThreat(x, odds) is not { } threat || x.Card(command.TargetId) is not { } target || target.OwnerId == x.Self.Id) return null;
        if (!RemovesUnit(x, action.Type, command, target.Id, null)) return null;
        var confidence = Confidence(x, threat) ?? 0;
        if (confidence <= Knob(x, "cutk", 1)) return target.Id == threat.Id ? Math.Max(score, 160 + HitWeight(x, action.Type, command)) : null;
        var reach = x.Actions.Where(a => a.Enabled && a.Command is { } c && a.Type is "useAbility" or "playStrategy" && (c.CardId != command.CardId || c.AbilityId != command.AbilityId) && RemovesUnit(x, a.Type, c, target.Id, a.TargetIds)).ToArray();
        var cheaper = 0.0;
        if (FromHand(x, command) && reach.Any(a => !FromHand(x, a.Command!))) cheaper += 20;
        var noGear = !x.Cards.Values.Any(c => c.AttachedTo == target.Id);
        if (noGear && HitWeight(x, action.Type, command) >= 6 && reach.Any(a => HitWeight(x, a.Type, a.Command!) == 2)) cheaper += 10;
        return cheaper > 0 ? score - cheaper : null;
    }

    /// <summary>ownprov2 (Mendon, 2026-11-09: "I wouldn't risk it if the game was close unless I was confident I could hold at least one province regardless"): after giving one up, three Provinces
    /// remain, or two and the bot's Personalities outmatch the opponent's whole army.</summary>
    private static bool HoldsAProvince(Ctx x)
    {
        var left = x.Self.Provinces.Count(p => !p.Destroyed) - 1;
        return left >= 3 || left >= 2 && UnitCards(x.Self).Sum(c => UnitForceOf(x, c)) >= UnitCards(x.Opponent).Sum(c => UnitForceOf(x, c));
    }

    /// <summary>threatbody: the opponent has more Personalities and their whole unit Force beats the bot's plus its weakest Province's Strength.</summary>
    private static bool Threatened(Ctx x)
    {
        if (Personalities(x.Self) >= Personalities(x.Opponent)) return false;
        var theirs = UnitCards(x.Opponent).Sum(c => UnitForceOf(x, c));
        var mine = UnitCards(x.Self).Sum(c => UnitForceOf(x, c));
        var weakest = x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Strength).DefaultIfEmpty(0).Min();
        return theirs > mine + weakest;
    }

    /// <summary>presence2 (Mendon, grill 2026-11-09, Crane Dominance against Perfect Sacrifice): a bow, send-home or duel aimed at any enemy card or Personality counts, not only one whose target
    /// names an attacker; the Political bow and the duel he would have used were invisible to presence.</summary>
    private static bool WideAttackerTarget(AbilityDefinition ability, AbilityIntent intent) => intent.Side != TargetSide.Own
        && (ability.Target is { } t && (t.StartsWith("enemy-", StringComparison.Ordinal) && t != "enemy-attachment" || t is "slots" or "personality" or "attacking-personality" or "any-unit-card" or "personality-at-any-location")
            || ability.IssuesDuel);

    private static IEnumerable<Removal> DefenderRemovals(Ctx x)
    {
        var sources = x.Self.Hand.Select(c => (Card: c, FromHand: true))
            .Concat(x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Select(c => (Card: c, FromHand: false)));
        foreach (var (card, fromHand) in sources)
        {
            if (card.Definition is null || !fromHand && (card.Bowed || card.ActionLocked)) continue;
            foreach (var (id, ability, intent) in BattlePolicy.Printed(card))
            {
                if (ability.Timing is not ("Battle" or "BattleOrOpen") || intent.IsReaction || !(AttackerTargets.Contains(ability.Target ?? "") || Rule(x, "presence2") && WideAttackerTarget(ability, intent) && !(Rule(x, "p2noduel") && ability.IssuesDuel))) continue;
                var minus = System.Text.RegularExpressions.Regex.Match(AbilityIntent.EffectText(ability.Label ?? ""), @"[-−]\s?(\d+)\s?F");
                var full = BattlePolicy.SendsHome(id) || intent.Rank is EffectRank.Destroy or EffectRank.Bow && !minus.Success || Rule(x, "presence2") && !Rule(x, "p2noduel") && ability.IssuesDuel;
                if (!full && !minus.Success) continue;
                yield return new(full, minus.Success ? int.Parse(minus.Groups[1].Value) : 0, fromHand ? card.Definition.GoldCost : 0);
                break; // one use per card
            }
        }
        if (x.View.FavorPlayerId == x.Self.Id && AbilityCatalog.All.ContainsKey("favor-battle")) yield return new(true, 0, 0);
    }

    /// <summary>The attacking Force left at a battlefield after the removals the Gold pays for, each taking the largest unit (or its Force bonus off the largest).</summary>
    private static int ForceAfterRemovals(int[] attackingUnits, Removal[] removals, int gold)
    {
        var units = attackingUnits.OrderByDescending(f => f).ToList();
        foreach (var r in removals.OrderByDescending(r => r.Full).ThenBy(r => r.Cost))
        {
            if (r.Cost > gold || units.Count == 0) continue;
            gold -= r.Cost;
            if (r.Full) units.RemoveAt(0);
            else { units[0] = Math.Max(0, units[0] - r.Minus); units = units.OrderByDescending(f => f).ToList(); }
        }
        return units.Sum();
    }

    /// <summary>
    /// The presence defense: at the battlefield where the attack would raze the Province and the removals would stop it, the cheapest of the offered units whose presence (and Force) keeps the
    /// Province standing. Null when no attack threatens a Province, when the removals would not save it, or when nothing is offered.
    /// </summary>
    private static Dictionary<string, int>? PresencePlan(Ctx x, ActionOption action)
    {
        if (x.View.Phase is not (Phase.DefenseAssignment or Phase.CavalryDefense)) return null;
        var legal = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var offered = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legal is null || legal.Contains(c.Id)))
            .OrderBy(c => UnitValue(x.Self, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        // pdstrong (Mendon's games, 2026-11-09: his defenders were his strongest, the bot's its cheapest): a presence defense sends the strongest units first.
        if (Rule(x, "pdstrong")) offered = offered.OrderByDescending(c => UnitForce(x, c)).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
        if (offered.Length == 0) return null;
        var removals = DefenderRemovals(x).ToArray();
        if (removals.Length == 0) return null;
        var provinces = action.Provinces ?? x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        foreach (var province in x.Self.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderByDescending(p => AttackersAt(x, p.Index)))
        {
            var attackers = x.Opponent.Army.Where(c => c.ProvinceIndex == province.Index && c.Definition?.Type == CardType.Personality)
                .Select(c => ArmyForce(x.Opponent.Army.Where(h => h.Id == c.Id || h.AttachedTo == c.Id))).Where(f => f > 0).ToArray();
            var already = ArmyForce(x.Self.Army.Where(c => c.ProvinceIndex == province.Index));
            if (attackers.Length == 0 || attackers.Sum() <= province.Strength + already) continue; // it does not fall
            var left = ForceAfterRemovals(attackers, removals, Ready(x));
            var sent = new List<CardView>(); var force = already;
            foreach (var unit in offered)
            {
                if (sent.Count > 0 && left <= province.Strength + force) break;
                sent.Add(unit); force += UnitForce(x, unit);
            }
            if (left > province.Strength + force) continue; // even with removals and everyone, it falls: the ordinary defense decides
            // presdepth (Mendon, 2026-11-09: "two send home abilities, so you'll need a minimum of 3 personalities to keep someone throughout the entire battle"): one defender more than the
            // attackers' unused printed removals there and a share of the opponent's hand could take away, so presence, and the removals that need it, survive.
            // defwin (Mendon's games, 2026-11-09: he defended with three to nine units where the bot sent one): a defense that can win is sent big enough to win before any removal.
            if (Rule(x, "defwin"))
                foreach (var unit in offered.Where(u => !sent.Contains(u)))
                {
                    if (province.Strength + force > attackers.Sum() + Knob(x, "dwM", 0)) break;
                    sent.Add(unit); force += UnitForce(x, unit);
                }
            if (Rule(x, "presdepth"))
            {
                var there = x.Opponent.Army.Where(c => c.ProvinceIndex == province.Index && c.Definition?.Type == CardType.Personality && !c.Bowed && !c.ActionLocked).ToArray();
                var need = 1 + EnemyRemovalSources(x, there) + (int)Math.Round(Knob(x, "pdh", 50) / 100.0 * HandRemovals(x, there.Length));
                foreach (var unit in offered.Where(u => !sent.Contains(u)))
                {
                    if (sent.Count >= need) break;
                    sent.Add(unit);
                }
            }
            return sent.ToDictionary(u => u.Id, _ => province.Index, StringComparer.Ordinal);
        }
        return null;
    }

    // ---- waste ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static readonly System.Text.RegularExpressions.Regex BattlefieldClause = new(@"\bbattlefield\b|\battacking\b|\bopposed\b|\bdefending\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex RemovalWords = new(@"\bbow|\bdestroy|\bhome\b|\bduel", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex MayNot = new(@"\bmay not\b|\bcannot\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex MinusForce = new(@"[-−]\s?\d+\s?F\b|\bForce\b[^.]*\b(penalty|to 0)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    private static readonly System.Text.RegularExpressions.Regex StraightenText = new(@"\bstraighten\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>The waste rules: an action they rule out is scored below passing.</summary>
    private static bool Contender2Bar(Ctx x, ActionOption action, GameCommand command)
    {
        var intent = action.Type == "useAbility" ? AbilityIntent.Of(command.AbilityId) : StrategyIntent(x.Card(command.CardId));
        var target = x.Card(command.TargetId);
        var ownTurn = x.View.ActivePlayerId == x.Self.Id;
        var openPhase = x.View.Phase == Phase.Action && x.View.PendingChoice is null;
        // lobbyok: lobbying is not self-harm.
        // selfharm: the bot's own unit is sent home or bowed only to save it from a battle it is losing.
        if (Rule(x, "selfharm") && target is not null && target.OwnerId == x.Self.Id && !(Rule(x, "lobbyok") && intent.TakesFavorBowing) && target.Definition?.Type is CardType.Personality or CardType.Follower
            && (BattlePolicy.SendsHome(command.AbilityId) || intent.Rank is EffectRank.Bow or EffectRank.Penalize && intent.Side != TargetSide.Own))
            return !(x.Odds is { Winning: false } && target.ProvinceIndex is not null
                && !(Rule(x, "lastprov") && !x.Odds.IsAttacker && x.Self.Provinces.Count(p => !p.Destroyed) <= 1)); // lastprov: the last Province is never given up
        // slate: Gold spent in the Action phase is Gold the Dynasty phase that follows cannot spend on the plan's purchases.
        // equipok: a Follower or Item that adds Force is part of the army the plan attacks with, not a drain on its purchases.
        if (Rule(x, "slate") && openPhase && ownTurn && (action.Gold?.Cost ?? 0) is var cost and > 0 && !intent.IsReaction && x.Plan is { Slate.Length: > 0 } plan
            && !(Rule(x, "equipok") && action.Type == "equip" && x.Card(command.CardId)?.Definition is { Type: CardType.Follower or CardType.Item, Force: > 0 }))
        {
            var slateCost = plan.Slate.Select(id => x.Card(id)).OfType<CardView>().Sum(c => EffectiveCost(x, c));
            if (Ready(x) - cost < slateCost) return true;
        }
        // purpose: on its own turn, a straighten or a bow is worth something only for an attack it will make.
        if (Rule(x, "purpose") && openPhase && ownTurn && !intent.IsReaction && command.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability)
            && (StraightenText.IsMatch(AbilityIntent.EffectText(ability.Label ?? "")) || intent.Rank == EffectRank.Bow && intent.Side != TargetSide.Own) && DeclareScore(x) <= PassScore)
            return true;
        // lobbyatk (Mendon's games, 2026-11-09: he lobbied after his attack or not at all, the bot before it): on its own turn before an attack it will make, the Favor is not taken by
        // bowing a Personality with Force who would attack.
        if (Rule(x, "lobbyatk") && intent.TakesFavorBowing && openPhase && ownTurn && x.Card(command.TargetId) is { Bowed: false } lobbied && UnitForce(x, lobbied) > 0 && DeclareScore(x) > PassScore) return true;
        // lobby: the Favor is not worth bowing one of the last two Personalities who could defend.
        if (Rule(x, "lobby") && intent.TakesFavorBowing && x.Self.Home.Count(c => c.Definition?.Type == CardType.Personality && !c.Bowed) <= Knob(x, "lobbyN", 2)) return true;
        // provaim: a cut to a Province's strength outside a battle (Shrine to Nimuro) is made only on a turn the bot attacks; Complete aims it at the opponent's weakest Province.
        if (Rule(x, "provaim") && action.Type == "useAbility" && x.View.Phase == Phase.Action && command.AbilityId is { } aimId && AbilityCatalog.All.TryGetValue(aimId, out var aim)
            && LowersStrength.IsMatch(AbilityIntent.EffectText(aim.Label ?? "")) && (!ownTurn || DeclareScore(x) <= PassScore && !ArmyRazesAfterCut(x)))
            return true;
        // provtarget: a cut to a Province's strength is only worth anything at a battle the bot is attacking (there the existing rule aims it at the attacked Province); anywhere else it
        // lands on the first Province listed, often the bot's own.
        if (Rule(x, "provtarget") && action.Type == "useAbility" && command.AbilityId is { } lowerId && AbilityCatalog.All.TryGetValue(lowerId, out var lower)
            && LowersStrength.IsMatch(AbilityIntent.EffectText(lower.Label ?? "")) && !(x.Odds is { IsAttacker: true } && AttackedProvince(x) is not null))
            return true;
        // ownprov: a Province is a quarter of the game; giving one away for Honor is only right when that Honor wins it.
        if (Rule(x, "ownprov") && command.AbilityId is { } costId && AbilityCatalog.All.TryGetValue(costId, out var costly)
            && (costly.Costs ?? []).Any(c => c.Verb is CostVerbs.DestroyLeftmostProvince or CostVerbs.DestroyRightmostProvince)
            && (Rule(x, "ownprov2") ? x.Self.Honor < Knob(x, "ownprovH", 36) || !HoldsAProvince(x) : x.Self.Honor < 36))
            return true;
        if (command.AbilityId is { } wasteId && AbilityCatalog.All.TryGetValue(wasteId, out var waste))
        {
            var effect = AbilityIntent.EffectText(waste.Label ?? "");
            // holdbo: a Battle-or-Open effect that depends on a battlefield ("if he is at a battlefield, bow him", attacking, opposed) loses that half in the Open phase.
            if (Rule(x, "holdbo") && openPhase && waste.Timing == "BattleOrOpen" && BattlefieldClause.IsMatch(effect)) return true;
            // selflock: "may not perform actions / assign" on one of its own cards, or on itself.
            var lockTarget = target ?? (intent.Side == TargetSide.None ? x.Card(command.CardId) : null);
            if (Rule(x, "selflock") && MayNot.IsMatch(effect) && lockTarget is not null && lockTarget.OwnerId == x.Self.Id) return true;
            // noop0: a Force penalty on a card that has no Force left.
            if (Rule(x, "noop0") && target is { Force: <= 0 } && target.OwnerId != x.Self.Id && MinusForce.IsMatch(effect) && intent.Rank < EffectRank.Bow) return true;
        }
        // selfboost: a Force bonus a card buys by bowing itself, given to itself, does nothing (a bowed unit adds no Force).
        if (Rule(x, "selfboost") && command.CardId is { } boostSource && (command.TargetId == boostSource || command.PerformerId == boostSource && command.TargetId is null)
            && command.AbilityId is { } boostId && AbilityCatalog.All.TryGetValue(boostId, out var boost)
            && ((boost.Costs ?? []).Any(c => c.Verb == CostVerb.Bow) || (boost.CostOptions ?? []).Any(c => c.Verb == CostVerb.Bow))
            && ForceBonus(boost) > 0) return true;
        // selfboost: on its own turn an Open-phase Force bonus matters only for an attack it is about to make.
        if (Rule(x, "selfboost") && openPhase && ownTurn && action.Type == "useAbility" && command.AbilityId is { } pumpId && AbilityCatalog.All.TryGetValue(pumpId, out var pump)
            && pump.Timing == "Open" && ForceBonus(pump) > 0 && intent.Side != TargetSide.Enemy && DeclareScore(x) <= PassScore) return true;
        // nostraighten: a straighten on a unit that is already straight does nothing.
        if (Rule(x, "nostraighten") && command.AbilityId is { } upId && AbilityCatalog.All.TryGetValue(upId, out var up) && StraightenText.IsMatch(AbilityIntent.EffectText(up.Label ?? ""))
            && intent.Side != TargetSide.Enemy && (target ?? x.Card(command.PerformerId)) is { Bowed: false }) return true;
        // tiecheck: a cost that bows (or an effect that removes) one of the bot's own units at a battle is not paid when its Force there would fall to the enemy's or below: a tie destroys both armies.
        if (Rule(x, "tiecheck") && x.Odds is { } tieOdds && x.View.Phase == Phase.Battle && command.AbilityId is { } tieId && AbilityCatalog.All.TryGetValue(tieId, out var tieAbility))
        {
            var lost = new List<CardView>();
            if (((tieAbility.Costs ?? []).Any(c => c.Verb == CostVerb.Bow) || command.CostOption == "bow") && (x.Card(command.PerformerId) ?? x.Card(command.CardId)) is { Bowed: false, ProvinceIndex: not null } payer && payer.OwnerId == x.Self.Id) lost.Add(payer);
            var lostForce = lost.Sum(c => ArmyForce(x.Self.Army.Where(h => h.Id == c.Id || h.AttachedTo == c.Id)));
            if (lostForce > 0 && tieOdds.Own - lostForce <= tieOdds.Enemy && tieOdds.Own > tieOdds.Enemy) return true;
        }
        // overkill (Mendon, grill 2026-11-09: "playing actions when it doesn't need to"): a Force bonus at a battle already decided keeps the card for one that is not. An attack that beats its
        // Province by a wide margin needs no more; a defense whose Province falls even with the bonus loses the same cards either way.
        if (Rule(x, "overkill") && x.View.Phase == Phase.Battle && x.Odds is { } killOdds && !intent.IsReaction && intent.Side != TargetSide.Enemy && command.AbilityId is { } killId
            && AbilityCatalog.All.TryGetValue(killId, out var killAbility) && ForceBonus(killAbility) is > 0 and var killGain)
        {
            if (killOdds.IsAttacker && killOdds.Own - killOdds.Enemy > (killOdds.Strength ?? 0) + Knob(x, "okm", 6)) return true;
            if (!killOdds.IsAttacker && killOdds.Strength is { } held && killOdds.Enemy - killOdds.Own - killGain >= held) return true;
        }
        // handkeep (Mendon, 2026-11-09: "cards generally = options"): behind on cards, a card from hand is not spent in the Action phase on anything short of a removal.
        if (Rule(x, "handkeep") && openPhase && action.Type is "useAbility" or "playStrategy" && !intent.IsReaction && FromHand(x, command) && intent.Rank < EffectRank.Bow
            && x.Self.Hand.Count() <= x.Opponent.HandCount - Knob(x, "hkN", 2)) return true;
        // cycle: the Favor's discard-and-draw throws away a card the bot chose to keep.
        if (Rule(x, "cycle") && command.AbilityId == "favor-cycle") return true;
        return false;
    }

    // ---- lean attack ------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// lean: the champion defends only where it can win or save the Province, so an army with more Force than the Province's Strength plus every unbowed enemy defender razes it unopposed (and an
    /// undefended battlefield gives the defender no presence for Battle actions). The bot sends the smallest such armies at as many Provinces as it can, and keeps every other unit home, where it is a
    /// defender the opponent's next attack must beat. Null when no army can raze: the bot does not attack.
    /// </summary>
    private static Dictionary<string, int>? LeanPlan(Ctx x, CardView[] units, int[] provinces, bool maxRaze = false)
    {
        var defence = ArmyForce(x.Opponent.Home);
        var pool = units.Select(u => (Unit: u, Force: UnitForce(x, u))).Where(u => u.Force > 0).OrderByDescending(u => u.Force).ThenBy(u => u.Unit.Id, StringComparer.Ordinal).Take(9).ToArray();
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).OrderBy(p => p.Strength).ThenBy(p => p.Index).ToArray();
        for (var k = Math.Min(3, Math.Min(targets.Length, pool.Length)); k >= 1; k--)
        {
            var chosen = targets.Take(k).ToArray();
            var need = chosen.Select(t => (Rule(x, "lean2") || maxRaze ? Math.Max(t.Strength, defence) : t.Strength + defence) + 1).ToArray(); // lean2: beating each of them separately razes it or destroys the defense
            int[]? best = null; var bestUsed = int.MaxValue; var slot = new int[pool.Length];
            void Search(int i, int[] force, int used)
            {
                if (used >= bestUsed) return;
                if (i == pool.Length)
                {
                    for (var j = 0; j < k; j++) if (force[j] < need[j]) return;
                    bestUsed = used; best = (int[])slot.Clone(); return;
                }
                for (var choice = 0; choice <= k; choice++) // k means home
                {
                    slot[i] = choice;
                    if (choice < k) { force[choice] += pool[i].Force; Search(i + 1, force, used + pool[i].Force); force[choice] -= pool[i].Force; }
                    else Search(i + 1, force, used);
                }
            }
            Search(0, new int[k], 0);
            if (best is not null)
                return Enumerable.Range(0, pool.Length).Where(i => best[i] < k).ToDictionary(i => pool[i].Unit.Id, i => chosen[best[i]].Index, StringComparer.Ordinal);
        }
        return null;
    }

    /// <summary>lean2, Cavalry segment: every army already sent beats both its Province's Strength and the enemy's unbowed defenders, so no Cavalry need join.</summary>
    private static bool LeanArmiesSuffice(Ctx x)
    {
        var defence = ArmyForce(x.Opponent.Home);
        return x.Self.Army.Where(c => c.ProvinceIndex is not null).GroupBy(c => c.ProvinceIndex!.Value)
            .All(g => ArmyForce(g) > Math.Max(defence, x.Opponent.Provinces.FirstOrDefault(p => p.Index == g.Key)?.Strength ?? 0));
    }

    /// <summary>favoraware: the opponent holds the Imperial Favor, whose Battle action sends one attacking unit home. A plan of a single army that no longer beats its Province once its largest unit
    /// is gone razes nothing: the bot keeps its army home instead.</summary>
    private static bool FavorStopsAttack(Ctx x, Dictionary<string, int> plan)
    {
        if (x.View.FavorPlayerId != x.Opponent.Id || plan.Values.Distinct().Count() != 1) return false;
        var province = x.Opponent.Provinces.FirstOrDefault(p => p.Index == plan.Values.First());
        if (province is null) return false;
        var units = x.Self.Home.Concat(x.Self.Army).Where(c => plan.ContainsKey(c.Id)).Select(c => UnitForce(x, c)).OrderByDescending(f => f).ToArray();
        return units.Skip(1).Sum() <= Math.Max(province.Strength, ArmyForce(x.Opponent.Home));
    }

    /// <summary>maxraze: the most Provinces the army can raze this turn, found exactly (each army beats its Province's Strength and the unbowed defenders, as lean2), with every unit the armies do not
    /// need added to the army with the thinnest margin: nobody stays home (keeping units home lost games).</summary>
    private static Dictionary<string, int>? MaxRazePlan(Ctx x, CardView[] units, int[] provinces)
    {
        if (LeanPlan(x, units, provinces, maxRaze: true) is not { } lean) return null;
        var plan = new Dictionary<string, int>(lean, StringComparer.Ordinal);
        var defence = ArmyForce(x.Opponent.Home);
        int Margin(int province) => units.Where(u => plan.TryGetValue(u.Id, out var p) && p == province).Sum(u => UnitForce(x, u))
            - Math.Max(defence, x.Opponent.Provinces.First(p => p.Index == province).Strength);
        foreach (var unit in units.Where(u => !plan.ContainsKey(u.Id)).OrderByDescending(u => UnitForce(x, u)))
            plan[unit.Id] = plan.Values.Distinct().OrderBy(Margin).ThenBy(p => p).First();
        return plan;
    }

    /// <summary>lethal (Mendon, grill 2026-11-09): with the opponent down to its last Provinces, an attack that razes every one of them is made with everyone; no defense is needed after it.</summary>
    private static Dictionary<string, int>? LethalPlan(Ctx x, CardView[] units, int[] provinces)
    {
        var alive = x.Opponent.Provinces.Count(p => !p.Destroyed);
        if (!Rule(x, "lethal") || alive > Knob(x, "lethalN", 2) || units.Length == 0 || MaxRazePlan(x, units, provinces) is not { } plan) return null;
        return plan.Values.Distinct().Count() + AlreadyAssigned(x).Select(c => c.ProvinceIndex).Distinct().Count(i => i is not null && !plan.ContainsValue(i.Value)) >= alive ? plan : null;
    }

    /// <summary>lean3, Cavalry segment: every army already sent beats its Province's Strength plus every unbowed defender, so it razes whatever the defense does.</summary>
    private static bool LeanArmiesRaze(Ctx x)
    {
        var defence = ArmyForce(x.Opponent.Home);
        return x.Self.Army.Where(c => c.ProvinceIndex is not null).GroupBy(c => c.ProvinceIndex!.Value)
            .All(g => ArmyForce(g) > defence + (x.Opponent.Provinces.FirstOrDefault(p => p.Index == g.Key)?.Strength ?? 0));
    }

    /// <summary>provaim: the whole unbowed home army beats every unbowed defender, so with one Province's strength cut to nothing it razes it.</summary>
    private static bool ArmyRazesAfterCut(Ctx x) => ArmyForce(x.Self.Home) > ArmyForce(x.Opponent.Home);
}
