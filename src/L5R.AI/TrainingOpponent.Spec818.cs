using System.Text.RegularExpressions;
using L5R.Core;

namespace L5R.AI;

// Issue #818 (Mendon's eleven bot changes, 2026-10-10). Everything is read from the bot's own permitted view and printed card data, never a card's name. BotContext.UseSpec818 (ON) gates it all.
//   1 holdings to 10, and buy every province card when it can afford them all     5 lobby / defend with the Imperial Favor    9 equips count Force removed from the enemy (Battle.cs)
//   2 catch-up recruit                                                            6 attack force: hand bonuses, beat each     10 Shinden and other optional bows
//   3 discard Personalities down to what the Gold production affords              7 attacker passes once it destroys the     11 never decline an optional destroy or bow (Policy.cs)
//   4 overlay for free only when ahead or the card would be discarded                 province; only Honor plays that resolve
//                                                                                8 Honor attacks
public static partial class TrainingOpponent
{
    private static bool Spec(Ctx x) => x.Memory.UseSpec818;

    // ---- Dynasty phase: Gold, province cards, discards (items 1-4) ----------------------------------------------------------------------------------------------------

    /// <summary>The Gold the bot's Stronghold and Holdings could produce in a turn, bowed or not (total capacity, as against what is unbowed right now).</summary>
    private static int Capacity(Ctx x) => CapacityOf(x.Self.Stronghold, x.Self.Clan) + x.Self.Home.Sum(c => CapacityOf(c, x.Self.Clan));

    private static int CapacityOf(CardView card, string clan) => (card.Definition?.GoldProduction ?? 0) + ClanGoldEffects.Bonus(card.Definition?.Effect, clan);

    private static int Ready(Ctx x) => Production(x.Self.Stronghold, x.Self.Clan) + x.Self.Home.Sum(c => Production(c, x.Self.Clan));

    /// <summary>Face-up Dynasty cards in the bot's live provinces.</summary>
    private static CardView[] ProvinceCards(Ctx x) =>
        x.Self.Provinces.Where(p => !p.Destroyed).Select(p => p.Card).OfType<CardView>().Where(c => c.FaceUp && c.Definition is not null).ToArray();

    private static bool ClanAligned(CardDefinition def, string clan) => def.Clan == clan || def.Clans?.Contains(clan) == true;

    /// <summary>A Personality's Gold cost, two less when it is aligned with the bot's Clan (Mendon: the Clan discount); other cards cost their printed Gold.</summary>
    private static int EffectiveCost(Ctx x, CardView card) =>
        card.Definition is not { } def ? 0 : def.Type == CardType.Personality && ClanAligned(def, x.Self.Clan) ? Math.Max(0, def.GoldCost - 2) : def.GoldCost;

    /// <summary>Item 1: the Gold available right now pays for every face-up card in the provinces, so none is discarded and every one is bought whatever its Holdings count.</summary>
    private static bool CanAffordAllProvinceCards(Ctx x)
    {
        if (!Spec(x) || x.View.Phase != Phase.Dynasty) return false;
        var cards = ProvinceCards(x);
        return cards.Length > 0 && cards.Sum(c => EffectiveCost(x, c)) <= Ready(x);
    }

    /// <summary>Item 1: Holdings stay a priority up to ten; with Gold for the whole row they are always bought.</summary>
    private static double HoldingPurchaseScore(Ctx x, int holdings)
    {
        var score = holdings < 5 ? 160 - holdings * 10 : holdings < (Spec(x) ? 10 : 8) ? 70 - holdings : 5;
        return CanAffordAllProvinceCards(x) ? Math.Max(score, 100) : score;
    }

    /// <summary>Item 2: after its second turn, a bot with fewer Personalities than its opponent recruits one before anything else (the best Personality, by its usual scoring), once, then goes back to normal.</summary>
    private static double CatchUpRecruitBonus(Ctx x) =>
        Spec(x) && x.Self.TurnsTaken > 2 && Personalities(x.Self) < Personalities(x.Opponent) && !x.Memory.RecruitedThisTurn(x.View.TurnNumber) ? 400 : 0;

    /// <summary>
    /// Item 3: the face-up Personalities in the provinces the bot will discard. If its total Gold capacity (bowed Holdings included) pays for all of them, none; otherwise it discards the cheapest
    /// until the rest fit, counting a Clan Personality two Gold cheaper (a 14 Gold bot with Clan Personalities costing 10 and 9 counts 8 and 7, and discards the 7).
    /// </summary>
    private static HashSet<string> PersonalityDiscards(Ctx x)
    {
        var cards = ProvinceCards(x).Where(c => c.Definition!.Type == CardType.Personality).Select(c => (c.Id, Cost: EffectiveCost(x, c))).OrderBy(c => c.Cost).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        var capacity = Capacity(x);
        var discarded = new HashSet<string>(StringComparer.Ordinal);
        while (cards.Sum(c => c.Cost) > capacity && cards.Count > 0) { discarded.Add(cards[0].Id); cards.RemoveAt(0); }
        return discarded;
    }

    /// <summary>The score for discarding one province card at the end of the Dynasty Phase. Ending the phase scores 0, so a negative score keeps the card.</summary>
    private static double DiscardProvinceScore(Ctx x, CardView? card)
    {
        var holdings = x.Self.Home.Count(c => c.Definition?.Type == CardType.Holding);
        if (!Spec(x)) return card?.Definition?.Type == CardType.Holding && holdings >= 8 ? 40 : 20;
        if (CanAffordAllProvinceCards(x)) return -5;
        if (card?.Definition?.Type == CardType.Personality) return PersonalityDiscards(x).Contains(card.Id) ? 20 : -5;
        return card?.Definition?.Type == CardType.Holding && holdings >= 10 ? 40 : 20;
    }

    /// <summary>Item 4: an experienced Personality is overlaid for free only when the bot has more Personalities than its opponent, or leaving the card in its province would see it discarded.</summary>
    private static double OverlayScore(Ctx x, CardView? card)
    {
        if (card?.Definition is null) return -100;
        var score = 180 + card.Definition.Force * 2;
        if (!Spec(x)) return score;
        var ahead = Personalities(x.Self) > Personalities(x.Opponent);
        var discarded = !CanAffordAllProvinceCards(x) && PersonalityDiscards(x).Contains(card.Id);
        return ahead || discarded ? score : -5;
    }

    // ---- Imperial Favor (item 5) ---------------------------------------------------------------------------------------------------------------------------------------

    private static IEnumerable<CardView> UnitCards(PlayerView p) => p.Home.Concat(p.Army).Where(c => c.Definition?.Type == CardType.Personality).DistinctBy(c => c.Id);

    /// <summary>A Personality's unit Force: its own and its attachments', bowed or not.</summary>
    private static int UnitForceOf(Ctx x, CardView card) =>
        card.UnitForce ?? Math.Max(0, card.Force) + x.Cards.Values.Where(a => a.AttachedTo == card.Id).Sum(a => Math.Max(0, a.Force));

    /// <summary>Item 5: with the higher Honor, and an opponent whose Personalities average more unit Force than the bot's weakest unit, the bot lobbies for the Favor however many Personalities each has.</summary>
    private static bool FavorLobbyWanted(Ctx x)
    {
        if (!Spec(x) || x.Self.Honor <= x.Opponent.Honor) return false;
        var theirs = UnitCards(x.Opponent).Select(c => UnitForceOf(x, c)).ToArray();
        var mine = UnitCards(x.Self).Select(c => UnitForceOf(x, c)).ToArray();
        return theirs.Length > 0 && mine.Length > 0 && theirs.Average() > mine.Min();
    }

    // ---- Attack force (item 6) -----------------------------------------------------------------------------------------------------------------------------------------

    private static readonly Regex ConditionalBonus = new(@"\bopposed\b|\bbowed\b", RegexOptions.IgnoreCase);

    private static readonly Regex CreatedForce = new(@"\b(?:create|creates)\b[^.:]*?\b(\d+)\s?F\b", RegexOptions.IgnoreCase);

    /// <summary>Item 6: the Force each card in the bot's hand could add to a battle ("+N F" for its own side, or a card it creates with Force), one entry per card.</summary>
    private static int[] HandForceBonuses(Ctx x) =>
        x.Self.Hand.Where(c => c.Definition is not null).Select(c =>
        {
            if (c.Definition!.Effect == "force-three") return 3;
            var best = 0;
            foreach (var (_, ability, intent) in BattlePolicy.Printed(c))
            {
                if (ability.Timing is not ("Battle" or "BattleOrOpen") || intent.Side == TargetSide.Enemy) continue;
                if (Rule(x, "phantom") && ConditionalBonus.IsMatch(ability.Label ?? "")) continue; // issue #1185 tuning: an opposed or bowed performer is not there against an undefended Province
                var created = CreatedForce.Match(AbilityIntent.EffectText(ability.Label ?? ""));
                best = Math.Max(best, Math.Max(ForceBonus(ability), created.Success ? int.Parse(created.Groups[1].Value) : 0));
            }
            return best;
        }).Where(b => b > 0).ToArray();

    // ---- Attacker rules in the battle (item 7) ------------------------------------------------------------------------------------------------------------------------

    private static readonly Regex NeedsProvinceStanding = new(@"(has|have|is|are) not (been )?destroyed|unless[^.]*province", RegexOptions.IgnoreCase);

    private static IEnumerable<AbilityDefinition> BattleAbilitiesOf(Ctx x, ActionOption action, GameCommand command)
    {
        if (action.Type == "useAbility" && command.AbilityId is not null && AbilityCatalog.All.TryGetValue(command.AbilityId, out var one)) return [one];
        return x.Card(command.CardId) is { } card ? BattlePolicy.Printed(card).Where(p => p.Ability.Timing is "Battle" or "BattleOrOpen").Select(p => p.Ability) : [];
    }

    /// <summary>Item 7: an Honor gain or loss only counts when it will happen. A gain that needs the Province to survive (Preserving Beauty) fizzles once the bot's Force destroys it.</summary>
    private static bool HonorResolves(Ctx x, ActionOption action, GameCommand command, BattlePolicy.Odds odds) =>
        !(odds.IsAttacker && odds.Satisfied && BattleAbilitiesOf(x, action, command).Any(a => NeedsProvinceStanding.IsMatch(a.Label ?? "")));

    /// <summary>
    /// Items 6 and 7, applied to an action taken in a battle. Null leaves the ordinary score. An attacker nobody opposes, whose Force already destroys the Province, passes unless the action
    /// gains the bot Honor or costs the opponent Honor and will actually do so; a bot holding as many cards as its hand limit also plays Honor gains that change nothing.
    /// </summary>
    private static double? AttackerAdjust(Ctx x, ActionOption action, GameCommand command, AbilityIntent intent, double score)
    {
        var odds = x.Odds;
        if (!Spec(x) || odds is null || intent.Negates || intent.IsReaction) return null;
        var gainsHonor = intent.HonorGain && intent.Side != TargetSide.Enemy || intent.HonorLoss && intent.Side != TargetSide.Own;
        if (odds.IsAttacker && odds.Satisfied && !x.Memory.UseUnopposedGate && !EnemyArmyAt(x).Any(c => c.Definition?.Type == CardType.Personality)) // issue #1003: UnopposedAdjust decides this case when its gate is on
            return gainsHonor && HonorResolves(x, action, command, odds) ? Math.Max(score, 130) : BarredScore;
        if (odds.Satisfied && gainsHonor && x.Self.HandCount >= x.MaxHand && HonorResolves(x, action, command, odds)) return Math.Max(score, 105);
        return null;
    }

    // ---- Honor attacks (item 8) ----------------------------------------------------------------------------------------------------------------------------------------

    private static readonly Regex Performing = new(@"performing\s+(?:(?:opposed|unopposed|unbowed|bowed|engaged)\s+)*([\w'-]+(?:\s+Clan)?)\s+Personality", RegexOptions.IgnoreCase);

    /// <summary>Battle cards in the bot's hand that gain it Honor.</summary>
    private static CardView[] HonorCardsInHand(Ctx x) =>
        x.Self.Hand.Where(c => c.Definition is not null && BattlePolicy.Printed(c).Any(p => p.Ability.Timing is "Battle" or "BattleOrOpen" && !p.Intent.IsReaction && p.Intent.HonorGain && p.Intent.Side != TargetSide.Enemy)).ToArray();

    /// <summary>Whether a unit meets the performer a card names ("your performing Shugenja Personality"); a card that names none can be performed by anyone.</summary>
    private static bool CanPerform(CardView unit, CardView card) =>
        BattlePolicy.Printed(card).Where(p => p.Intent.HonorGain && p.Ability.Timing is "Battle" or "BattleOrOpen").Any(p =>
        {
            var m = Performing.Match(p.Ability.Label ?? "");
            if (!m.Success) return true;
            var word = m.Groups[1].Value.Replace(" Clan", "", StringComparison.OrdinalIgnoreCase);
            return word.Equals("Personality", StringComparison.OrdinalIgnoreCase) || unit.Definition?.Keywords?.Contains(word, StringComparer.OrdinalIgnoreCase) == true
                || unit.Definition is { } d && (d.Clan.Equals(word, StringComparison.OrdinalIgnoreCase) || d.Clans?.Contains(word, StringComparer.OrdinalIgnoreCase) == true);
        });

    private sealed record HonorAttackPlan(string UnitId, Dictionary<string, int> Assignments);

    /// <summary>
    /// Item 8: the bot holds Honor-gaining Battle cards, so a unit that can perform one goes at a Province it cannot destroy, to play them there, while the rest of the army separately attacks a
    /// different Province to destroy it (or alone, when nothing else can). The lowest-Force performer goes, provided the rest can still make their own attack when they did before.
    /// </summary>
    private static HonorAttackPlan? HonorAttackOf(Ctx x, CardView[] units, int[] provinces)
    {
        if (!Spec(x) || HonorRace(x) || units.Length == 0) return null;
        var honorCards = HonorCardsInHand(x);
        if (honorCards.Length == 0) return null;
        var whole = PlanAttack(x, units, provinces);
        var targets = x.Opponent.Provinces.Where(p => !p.Destroyed && provinces.Contains(p.Index)).ToArray();
        foreach (var unit in units.OrderBy(c => UnitForceOf(x, c)).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            if (!honorCards.Any(card => CanPerform(unit, card))) continue;
            var rest = units.Where(c => c.Id != unit.Id).ToArray();
            var main = rest.Length == 0 ? null : PlanAttack(x, rest, provinces);
            if (whole is not null && main is null) continue; // the army needs this unit for its own attack
            var taken = main?.Values.ToHashSet() ?? [];
            var force = UnitForceOf(x, unit);
            var province = targets.Where(p => !taken.Contains(p.Index) && p.Strength >= force).OrderBy(p => p.Strength).ThenBy(p => p.Index).FirstOrDefault();
            if (province is null) continue;
            var plan = new Dictionary<string, int>(main ?? [], StringComparer.Ordinal) { [unit.Id] = province.Index };
            return new(unit.Id, plan);
        }
        return null;
    }

    /// <summary>The units and Provinces an attack action offers, as <c>Prepare</c> reads them.</summary>
    private static (CardView[] Units, int[] Provinces) AttackInputs(Ctx x, ActionOption action)
    {
        var legalIds = action.TargetIds?.ToHashSet(StringComparer.Ordinal);
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed && (legalIds is null || legalIds.Contains(c.Id))).ToArray();
        var provinces = action.Provinces ?? x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        return (units, provinces);
    }

    // ---- Shinden and other optional bows (item 10) -------------------------------------------------------------------------------------------------------------------

    /// <summary>Whether the bot's attack/no-attack decision differs with this Personality's unit unavailable.</summary>
    private static bool ChangesAttackDecision(Ctx x, CardView target)
    {
        var units = x.Self.Home.Where(c => c.Definition?.Type == CardType.Personality && !c.Bowed).ToArray();
        var provinces = x.Opponent.Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToArray();
        return (PlanAttack(x, units, provinces) is not null) != (PlanAttack(x, units.Where(c => c.Id != target.Id).ToArray(), provinces) is not null);
    }

    /// <summary>Item 10: bow the Personality when its unit is under 15% of the bot's army Force, or the opponent's Honor is over 25 (do not feed it), otherwise only if that cannot change whether the bot attacks.</summary>
    private static bool BowAcceptable(Ctx x, CardView? target)
    {
        if (!Spec(x) || target is null || target.OwnerId != x.Self.Id || target.Definition?.Type != CardType.Personality) return true;
        var total = UnitCards(x.Self).Where(c => !c.Bowed).Sum(c => UnitForceOf(x, c));
        if (total == 0 || UnitForceOf(x, target) < 0.15 * total || x.Opponent.Honor > 25) return true;
        return !ChangesAttackDecision(x, target);
    }

    private static double ShindenScore(Ctx x, GameCommand command)
    {
        var bow = BowAcceptable(x, x.Card(x.View.PendingChoice?.CardId));
        return (command.Choice == "bow") == bow ? 100 : -100;
    }

    // ---- Never decline an optional destroy or bow (item 11) ---------------------------------------------------------------------------------------------------------

    /// <summary>A Reaction or optional effect that destroys or bows a card of the opponent's is always taken.</summary>
    private static bool HarmsOpponentCard(Ctx x, GameCommand command, AbilityIntent intent) =>
        Spec(x) && intent.Rank >= EffectRank.Bow && (x.Card(command.TargetId) is { } t ? t.OwnerId != x.Self.Id : intent.Side == TargetSide.Enemy);

    /// <summary>Kokujin's "Destroy a personality to take control of the loser?" (issue #827): the loser is destroyed anyway when declined, so the trade is the loser's worth against the Personality given up
    /// (Force plus Personal Honor), by at least one point, and Kokujin himself is the last to go.</summary>
    private static double Wave829Worth(CardView? card) => card is null ? 0 : Math.Max(0, card.Force) + (card.Definition?.PersonalHonor ?? 0);

    /// <summary>Benten's Blessing's multi-select (issue #829): tick its own Personalities (+1 Personal Honor helps a Proclaim and the honor race), never an enemy's, never remove one it ticked; then confirm.</summary>
    private static double Wave829BentenScore(Ctx x, ActionOption action)
    {
        if (action.Label.StartsWith("Remove", StringComparison.Ordinal)) return -50;
        return x.Card(action.CardId) is { } card && card.OwnerId == x.Self.Id ? 120 : -50;
    }

    /// <summary>The Events that stay face up in a province ask their owner a card (issue #829): Cross-Clan Wedding gives away the weakest Personality, Minor Blackmail names the other player's strongest; any
    /// other Event takes the first offered answer.</summary>
    private static double Wave829EventChoiceScore(Ctx x, ActionOption action, GameCommand command)
    {
        var effect = x.Card(action.CardId)?.Definition?.Effect;
        var chosen = x.Card(command.TargetId);
        return effect switch
        {
            Wave829Abilities.WeddingEffect when chosen is not null => 100 - Wave829Worth(chosen),
            Wave829Abilities.BlackmailEffect when chosen is not null => 100 + Wave829Worth(chosen),
            _ => PassScore + 1
        };
    }

    private static double Wave827KokujinScore(Ctx x, GameCommand command)
    {
        if (command.Choice == "decline") return 100;
        var duel = x.View.PendingDuel;
        var loser = duel is null ? null : x.Card(duel.ChallengedId);
        var victim = x.Card(command.TargetId);
        if (loser is null || victim is null) return -50;
        double Worth(CardView card) => Math.Max(0, card.Force) + (card.Definition?.PersonalHonor ?? 0);
        return 100 + Worth(loser) - Worth(victim) - 1 - (victim.Id == duel!.ChallengerId ? 5 : 0);
    }
}
