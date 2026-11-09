using L5R.Core;

namespace L5R.AI;

// Issue #642: the scoring half of the bot. Hand-tuned scores for abilities the bot already understood stay in TrainingOpponent.cs; everything else is read from
// AbilityIntent (the label and target category of the ability), never from a card's name, so an ability wired tomorrow is scored the moment it exists.
public static partial class TrainingOpponent
{
    /// <summary>How much more an ability is worth for what it does, in Mendon's order: destroy opponent cards, then bow them, then penalize them, then help itself.</summary>
    internal static double RankBonus(EffectRank rank) => rank switch
    {
        EffectRank.Destroy => 50,
        EffectRank.Bow => 40,
        EffectRank.Penalize => 25,
        EffectRank.Help => 10,
        _ => 0
    };

    private static double AbilityScore(Ctx x, ActionOption action, GameCommand command)
    {
        var id = command.AbilityId;
        if (id is "border-first" or "border-game") return id == "border-first" ? 140 : BorderRefreshWanted(x) ? 175 : 35;
        if (id == "shunya-honor") return 100;
        // Deck search (issue #312): a modest bot uses Ikoma Shizuka's search when nothing more useful is on offer (just above passing), which is what lets a whole
        // AI-vs-AI match exercise the full search flow.
        if (id == "shizuka-search") return 12;
        // Read the Essence (issue #699): turning an enemy face-down province card face-up scouts it; turning the bot's own only tells the opponent, so that is worse than passing.
        if (id == WlEssenceAbilities.Flip) return command.TargetPlayerId == x.Self.Id ? SelfHarmScore : 110;
        var intent = AbilityIntent.Of(id);
        // Never aim a harmful ability (destroy, bow, penalize, an Honor loss) at the bot's own cards: that is worse than passing, so it scores below it. The target picker already
        // prefers the opponent's cards; this catches the case where only the bot's own are legal (found by the arena: Scars of War, Tsujiken and Baiko cost a bot 24 Honor).
        // Lobby bows one of the bot's own Personalities as its cost; that is the price of the Favor, not an attack on its own card (issue #818), so taking the Favor is scored before the self-harm test.
        if (intent.TakesFavor) return FavorTakeScore(x, intent);
        if ((intent.Rank >= EffectRank.Penalize || intent.HonorLoss) && x.Card(command.TargetId) is { } victim && victim.OwnerId == x.Self.Id) return SelfHarmScore;
        // Issue #818 item 5: defending, the Favor is always used to send home the largest attacking unit at a battlefield (it targets only enemy attackers), whether or not the bot can take it back.
        if (Spec(x) && id == "favor-battle" && x.Card(command.TargetId) is { } sent && sent.OwnerId != x.Self.Id) return 300 + UnitForceOf(x, sent);
        if (intent.DiscardsFavor) return FavorSpendScore(x);
        // Mendon's spec: always play a Reaction that negates an effect; a Reaction that does not is a weighted choice (higher-ranked effects are likelier), rolled
        // deterministically from the match state so a replay makes the same choice. Declining a Reaction scores 100.
        if (intent.IsReaction)
        {
            // Issue #825: negating the bot's OWN held action is never what it wants (and the Gold its Reaction costs can break the action it is holding).
            if (intent.Negates) return x.View.PendingChoice is { Type: "beforeActionReaction" } held && held.PlayerId == x.Self.Id ? SelfHarmScore : 1500;
            if (HarmsOpponentCard(x, command, intent)) return 1400; // issue #818 item 11: an optional destroy or bow of an opponent's card is never declined
            // Issue #644: a Reaction a Personality (or item) prints for its own entry -- Togashi Meiyu's and Torazu's Fire tokens, Satoshi's Dual Warfans' draw -- is
            // never a gamble: the bot always takes it, unless it would only cost the bot Honor.
            if (id is not null && AbilityCatalog.All.TryGetValue(id, out var printed) && printed.Trigger == "own-enters-play" && !intent.HonorLoss) return 160 + RankBonus(intent.Rank);
            var roll = BotContext.Roll(x.View.Version, id, command.CardId);
            var weight = x.Memory.UseSureReactions && intent.Rank >= EffectRank.Help ? 1.0 : 0.3 + 0.15 * (int)intent.Rank; // issue #963 round 4: a Reaction that does something is not declined by chance
            return roll < weight ? 150 + RankBonus(intent.Rank) : 50;
        }
        // Hand-tuned values for the abilities the bot understood before issue #642 (they score the chosen target).
        if (x.Card(command.TargetId) is { } target && AbilityValue(id, target, x.Self, x.Opponent, x.View) is var known && known > 0)
            return Math.Max(PassScore, known - RepeatPenalty * x.Memory.UsesThisTurn(x.View.TurnNumber, id, command.CardId));
        return Math.Max(PassScore, GenericAbilityScore(x, intent) - RepeatPenalty * x.Memory.UsesThisTurn(x.View.TurnNumber, id, command.CardId));
    }

    /// <summary>Below passing: a harmful ability that would hit the bot's own card is never played when passing is possible.</summary>
    internal const double SelfHarmScore = -50;

    /// <summary>What each earlier use of the same ability from the same card this turn takes off its score (the floor stays at passing).</summary>
    internal const double RepeatPenalty = 20;

    /// <summary>The default for any ability the bot has no rule for: well above passing, higher for what it does, with Mendon's draw and Honor adjustments.</summary>
    private static double GenericAbilityScore(Ctx x, AbilityIntent intent)
    {
        var score = 30 + RankBonus(intent.Rank);
        // Cards that draw gain priority as the hand falls short of its maximum: the bigger the gap, the more it is worth.
        if (intent.Draws)
        {
            var gap = x.MaxHand - x.Self.HandCount;
            // A hand already at its maximum would discard what a pure draw ability draws, and its Gold is better kept for purchases: worth no more than passing.
            if (gap <= 0 && intent.Rank <= EffectRank.Help && !intent.HonorGain && !intent.HonorLoss) return PassScore;
            score += 12 * Math.Max(0, gap);
        }
        // A deck that wants Honor for itself prefers abilities that give it Honor; one that wants to hurt the opponent prefers abilities that take it away.
        var gain = WantsOwnGain(x);
        var military = x.Memory.UseArchetypeMilitary && x.Memory.Archetype is { Primary: WinRoute.Military, Secondary: null };
        if (intent.HonorGain && gain && !military) score += 25;
        if (x.Memory.UseArchetypeAbilities && x.Memory.Archetype is { } a)
        {
            if (a.Primary == WinRoute.Honor && intent.HonorGain) score += ArchetypeEngineBonus;
            if (a.Primary == WinRoute.Dishonor && intent.HonorLoss && !intent.SelfHonorLoss) score += ArchetypeEngineBonus;
        }
        if (intent.HonorGain && x.Memory.UseHonorAware && x.Profile.Tier == HonorTier.High) score += HighHonorGainBonus; // issue #870: a high-Honor deck wants Honor above all (abilities with a fixed score are untouched)
        if (intent.HonorLoss && !gain && !(x.Memory.UseHonorAware && intent.SelfHonorLoss)) score += 25; // issue #870: Honor the user loses himself is not "taking the opponent's"
        return score;
    }

    private static int Personalities(PlayerView p) => p.Home.Concat(p.Army).Count(c => c.Definition?.Type == CardType.Personality);

    /// <summary>
    /// Lobbying and other ways to take the Imperial Favor. With the higher Family Honor (Stronghold starting Honor) AND more Personalities the bot ALWAYS takes it;
    /// with an equal or lower number of Personalities it is a low priority unless taking it costs no bow (an Open action that simply takes it); in between it is
    /// a moderate priority.
    /// </summary>
    private static double FavorTakeScore(Ctx x, AbilityIntent intent)
    {
        if (x.View.FavorPlayerId == x.Self.Id) return PassScore;
        if (FavorLobbyWanted(x)) return 900; // issue #818 item 5: the opponent's units average more Force than the bot's weakest, and the bot leads on Honor
        var ahead = (x.Self.Stronghold.Definition?.StartingHonor ?? 0) > (x.Opponent.Stronghold.Definition?.StartingHonor ?? 0) && Personalities(x.Self) > Personalities(x.Opponent);
        if (ahead) return 900;
        if (Personalities(x.Self) <= Personalities(x.Opponent)) return intent.TakesFavorBowing ? PassScore + 2 : 150;
        return 60;
    }

    /// <summary>
    /// An ability that discards the Imperial Favor as a cost. The bot spends the Favor as many times as it can while it still ends the turn holding it: unlimited
    /// when it can take the Favor back with an Open action (it need not worry about keeping it), otherwise once, and only when it will certainly Lobby again
    /// this turn (the always-lobby case). When it could not get it back the ability is worth no more than passing.
    /// </summary>
    private static double FavorSpendScore(Ctx x)
    {
        if (x.View.FavorPlayerId != x.Self.Id) return PassScore;
        if (OwnFreeFavorTakers(x).Any()) return 140;
        var ahead = (x.Self.Stronghold.Definition?.StartingHonor ?? 0) > (x.Opponent.Stronghold.Definition?.StartingHonor ?? 0) && Personalities(x.Self) > Personalities(x.Opponent);
        return ahead && !x.Memory.LobbiedThisTurn(x.View.TurnNumber) ? 140 : PassScore;
    }

    /// <summary>The bot's own cards in play that can take the Favor with a non-Reaction action that does not bow anything.</summary>
    private static IEnumerable<CardView> OwnFreeFavorTakers(Ctx x) =>
        x.Self.Home.Concat(x.Self.Army).Append(x.Self.Stronghold).Where(c => c.Definition?.Abilities?.Any(a =>
        {
            var i = AbilityIntent.Of(a);
            return i.TakesFavor && !i.TakesFavorBowing && !i.IsReaction;
        }) == true);

    /// <summary>Proclaim is taken whenever the profile does not deprioritize it; a deprioritized Proclaim (a low-Honor deck short of spare Gold, or an in-between deck
    /// that trails by more than 5) ranks just under declining the window.</summary>
    private static double ProclaimActionScore(Ctx x)
    {
        var ready = Production(x.Self.Stronghold, x.Self.Clan) + x.Self.Home.Sum(c => Production(c, x.Self.Clan));
        return x.Profile.StanceFor(x.Self.Honor, x.Opponent.Honor, ready >= 3) == ProclaimStance.Deprioritize ? 40 : 1000;
    }

    /// <summary>"Choose a player who gains or loses N Honor": each answer is scored from the Honor profile.</summary>
    private static double EffectChoiceScore(Ctx x, GameCommand command)
    {
        // Read the Essence's optional second flip (issue #699): an enemy face-down card is scouted, the bot's own is never turned, and declining beats the latter.
        if (command.Choice is { } essence && essence.StartsWith(WlEssenceAbilities.SecondFlipPrefix, StringComparison.Ordinal))
            return essence[WlEssenceAbilities.SecondFlipPrefix.Length..].StartsWith(x.Self.Id + ":", StringComparison.Ordinal) ? -100 : 110;
        // Mendon's rule (playtest 2026-10): offered the destruction of an opponent's attachment (Strength in Terror's "may destroy one of his attachments"), the bot always takes it;
        // declining is only chosen if nothing else is offered.
        if (command.Choice == "sit-decline") return 20;
        if (command.Choice is { } destroy && destroy.StartsWith("sit-destroy:", StringComparison.Ordinal))
            return x.Card(destroy["sit-destroy:".Length..]) is { AttachedTo: not null } attachment && attachment.OwnerId != x.Self.Id ? 125 : -100;
        var gain = WantsOwnGain(x);
        return command.Choice switch
        {
            "self-gain" => gain ? 130 : 70,
            "opponent-lose" => gain ? 70 : 130,
            // Never the opponent gaining Honor or the bot losing it: only chosen if nothing else is offered.
            "opponent-gain" or "self-lose" => -100,
            _ => 100
        };
    }

    /// <summary>Extra score for an ability that is the deck's own engine (archetype S2).</summary>
    internal const double ArchetypeEngineBonus = 30;

    /// <summary>A dishonor deck declares an attack only with this much more Force than the defenders (archetype S1).</summary>
    internal const int DishonorAttackMargin = 4;

    /// <summary>Whether the bot would rather gain Honor than make the opponent lose it: the Honor tier's answer, unless the archetype switch reads the race (S4).</summary>
    private static bool WantsOwnGain(Ctx x)
    {
        var tier = x.Profile.PrefersOwnGain(x.Self.Honor, x.Opponent.Honor);
        if (!x.Memory.UseArchetypeSwitch || x.Memory.Archetype is not { } a) return tier;
        var honorRoute = a.Primary == WinRoute.Honor || a.Secondary == WinRoute.Honor;
        if (honorRoute && x.Self.Honor >= 28) return true; // within sight of 40: gain it
        if (a.Primary == WinRoute.Dishonor) return x.Opponent.Honor > -8 && x.Self.Honor < 0; // press the opponent unless the bot itself is in danger
        return tier;
    }

    public static bool WantsOwnGainForTest(MatchView view, BotContext memory)
    {
        var self = view.Players.Single(p => p.Id == view.ViewerId); var opponent = view.Players.Single(p => p.Id != view.ViewerId);
        return WantsOwnGain(new Ctx(view, self, opponent, BotProfile.Assess(self, memory.DeckAveragePersonalHonor), memory, AllCards(view)));
    }

    private static double DeclareScore(Ctx x)
    {
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var provinces = x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        var plan = PlanAttack(x, units, provinces);
        if (LethalPlan(x, units, provinces) is not null) return 200; // issue #1185 tuning (lethal)
        if (plan is null && HonorAttackOf(x, units, provinces) is not null) return 85; // issue #818 item 8: an attack made to gain Honor
        if (plan is null) return units.Length == 0 ? -100 : TradeFor(x, units) is { } trade ? TradeScore(trade) : 0;
        if (HonorRace(x)) return 200; // the opponent wins on Honor next turn: always attack
        var force = ArmyForce(x.Self.Home.Where(c => plan.ContainsKey(c.Id) || c.AttachedTo is not null && plan.ContainsKey(c.AttachedTo)));
        if (x.Memory.UseArchetypeAttack && x.Memory.Archetype is { Primary: WinRoute.Dishonor } && force - ArmyForce(x.Opponent.Home) < DishonorAttackMargin) return 0; // the engine wins this game, not a marginal attack
        return 90 + force - ArmyForce(x.Opponent.Home);
    }

    /// <summary>Remembers the one thing the bot must carry to the next decision: that it has lobbied this turn (Lobby is once a turn).</summary>
    private static void Remember(Ctx x, GameCommand command)
    {
        if (command.Type == "purchase" && x.Card(command.CardId)?.Definition?.Type == CardType.Personality) x.Memory.NoteRecruit(x.View.TurnNumber); // issue #818 item 2
        if (command.Type != "useAbility") return;
        var intent = AbilityIntent.Of(command.AbilityId);
        if (!intent.TakesFavor) return;
        if (command.AbilityId == "lobby") x.Memory.NoteLobby(x.View.TurnNumber);
        x.Memory.NoteFavorTaken(x.View.TurnNumber);
    }
}
