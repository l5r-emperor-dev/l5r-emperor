using System.Text.Json.Serialization;

namespace L5R.Core;

// Issue #738, wave batch 7: the turn-boundary, rule-flag and Events-Phase cards, wired on the merged P4 (turn begins, durations, player grants, standing rules, AddTurnScopedRule), P13 (rule flags,
// RuleFlags.cs) and P6 (the turned-face-up fact) primitives, with P7's player-level negation watch, P10's Clan-alignment grant and the existing forced-discard queue. Every legality and every effect is
// decided in Core. Ability ids are "w738-". Readings for Mendon to confirm or correct are in docs/decisions/2026-10-08-wave-batch-7-turn-boundaries-and-rule-flags.md. Waves are those of
// tools/content/package_wave_738.py.
//
// THE SCANS DO NOT PRINT THE P5 CLAUSE. The catalogue (Oracle) text of A Dangerous Game, An Empire of Madness, Open Recruitment and Rejected Mediation opens "After you reveal cards in your Provinces at
// the start of your turn, if this Event is face-up in one of them, ..."; the period scans print only the effect, so each is an ordinary Event that resolves when it is revealed (the P5 "stays face up and
// fires every turn" flag is not used). Open Recruitment's and Rejected Mediation's durations follow the scans too.
//
//   Doji Tatsuki - exp        "After another player's turn begins, straighten Tatsuki."                                                                        a TurnBegins callback (AnotherPlayers)
//   Shosuro Takazaki          "Expendable" / "Political Engage: Other players do not gain Honor for destroying your cards in this battle's resolution."       w738-takazaki-engage; a P13 battle flag
//   Dagger of Crystal Wind    "After your turn begins, give this Personality a +1F Obsession token; then, if he has more Obsession tokens than printed Chi, destroy him."   a TurnBegins callback (Yours)
//   Lantern Oil               "The Attacker may not take Engage actions at this Item's battlefield."                                                           the P13 engage bar, read from the Item's host
//   Drain of Effort           "Open: ... after each action other than this one resolves, its player discards a card." / "Open: Draw a card."                  w738-drain-watch (a record read by an after-action trait); w738-drain-draw
//   Suck the Marrow           "Open: Bow and destroy your performing Shugenja and target a Personality: Poison token. Lose 3 Honor. ... after each Events Phase begins, ..."   w738-marrow-curse; a P4 standing rule read as each Events Phase begins
//   Temple Grounds            "If you do not control a Cavalry unit here, negate each Battle action taken by a player without units here." / "Battle/Engage: Destroy a Terrain (if able). Put this Terrain into play."   w738-temple-battle / w738-temple-engage; a P13 battlefield flag
//   Abundant Farmlands        "After each player's turn begins, straighten this Holding." / "Bow: Produce 1 Gold."                                             a TurnBegins callback (EachPlayers); GoldProduction 1
//   A Dangerous Game          "You may discard your hand (even if it is empty). If you did, draw four cards."                                                  an Event decision (yes/no)
//   An Empire of Madness      "Until the game ends, each player has the ability, 'Open: Your target Personality with a Madness token commits Seppuku' and any player who ends his turn with 20 or more ..."   a P4 player grant; a P13 madness-loss flag
//   Journey's End Siege       "Limited: Defending players may not play Terrains. After the first time you destroy a Province in battle this turn, if you do not have more Provinces than the Defender, gain a Province."   a P13 flag; a province-destroyed call in the battle resolution
//   Open Recruitment          "Until your Events Phase two turns from now begins, after each time an unaligned Human Personality turns face-up in one of your provinces, permanently give him your Clan alignment."   a P4 standing rule; a P6 turned-face-up trait
//   Rejected Mediation        "Any player who controls the Imperial Favor discards it. Until your second turn from now begins, negate the effects of the first Political action taken each turn."   the Favor release; a P7 player-level negation watch
//   The Jade Championship     "Choose one of your Shugenja in play. Permanently give him the traits, ..."                                                     an Event decision; a saved list of the marked Shugenja read by a Force term and an after-action trait
//   The Madness Spreads       "Limited: Until your second turn from now begins, once per turn, you may take a Limited action on a Strategy as an Open action."   the P13 limited-as-open flag
//   Chagatai's Wrath          "Limited: End the Action Phase. Discard this Celestial."                                                                         w738-chagatai-end-phase; the P13 end-action-phase flag
//
// Issue #835: Revelations was wired afterwards in Wave835.cs, on Mendon's rulings of 2026-10-11 (docs/decisions/2026-10-11-twelve-ruled-cards.md); the "not wired" note here is the state when this wave merged.
// Not wired, with a blocker note (tools/content/pe_wave_738_blockers.json): Revelations (a look at the top five cards with another player's discard choice and a reorder, which neither the look nor P20's
// search covers) and The Onyx Championship (a card given a permanent ability whose search recruits from the provinces paying all costs: P20's province recruit ignores costs).
public static class Wave738Abilities
{
    public const string TakazakiEngage = "w738-takazaki-engage", DrainWatch = "w738-drain-watch", DrainDraw = "w738-drain-draw", MarrowCurse = "w738-marrow-curse", TempleBattle = "w738-temple-battle",
        TempleEngage = "w738-temple-engage", ChagataiEnd = "w738-chagatai-end-phase", EmpireSeppuku = "w738-empire-seppuku";

    // Target categories of this file (TargetAllows).
    public const string OwnMadnessPersonality = "w738-own-madness-personality";

    // The cards (oracle ids).
    public const string Tatsuki = "oracle-l5r-10841", Takazaki = "oracle-l5r-10872", Dagger = "oracle-l5r-10903", LanternOil = "oracle-l5r-10692", DrainOfEffort = "oracle-l5r-10369", SuckTheMarrow = "oracle-l5r-10395",
        TempleGrounds = "oracle-l5r-10554", Farmlands = "oracle-l5r-10606", DangerousGame = "oracle-l5r-10259", EmpireOfMadness = "oracle-l5r-10426", JourneysEnd = "oracle-l5r-10604", OpenRecruitment = "oracle-l5r-5762",
        RejectedMediation = "oracle-l5r-10261", JadeChampionship = "oracle-l5r-8163", MadnessSpreads = "oracle-l5r-10419", Chagatai = "oracle-l5r-10816";

    // Catalogue Effects (a card names its own traits with one).
    public const string TatsukiEffect = "w738-tatsuki", DaggerEffect = "w738-dagger", LanternOilEffect = "w738-lantern-oil", FarmlandsEffect = "w738-farmlands", DangerousGameEffect = "w738-dangerous-game",
        EmpireEffect = "w738-empire-of-madness", JourneysEndEffect = "w738-journeys-end", OpenRecruitmentEffect = "w738-open-recruitment", RejectedMediationEffect = "w738-rejected-mediation",
        JadeEffect = "w738-jade-championship", MadnessSpreadsEffect = "w738-madness-spreads";

    // P4 standing-rule kinds of this file.
    public const string MarrowRule = "w738-suck-the-marrow", OpenRecruitmentRule = "w738-open-recruitment";

    // Event decisions.
    public const string DangerousGameChoice = "w738-dangerous-game", JadeChoice = "w738-jade";

    /// <summary>The Terrains whose own ability puts them into play (never the generic playStrategy Terrain rule).</summary>
    public static bool IsTerrainPlayAbility(string abilityId) => abilityId is TempleBattle or TempleEngage;

    public static Dictionary<string, AbilityDefinition> AddWave738(this Dictionary<string, AbilityDefinition> all)
    {
        all[TakazakiEngage] = new("Reaction", "Political Engage: Other players do not gain Honor for destroying your cards in this battle's resolution", "none", Trigger: "after-engaging", ActionKeywords: ["Political"]);
        all[DrainWatch] = new("Open", "Open: Until this phase ends or a player has discarded two cards from this effect, after each action other than this one resolves, its player discards a card", "none");
        all[DrainDraw] = new("Open", "Open: Draw a card", "none");
        all[MarrowCurse] = new("Open", "Open: Bow and destroy your performing Shugenja and target a Personality: Give the target a -1F/-1C Poison token. Lose 3 Honor. Until the game ends, after each Events Phase begins, give the target a -1F/-1C Poison token",
            "personality", Performer: "own-personality-with-keyword", RequiredKeywords: ["Shugenja"], ActionKeywords: ["Maho"], Costs: [new(CostVerb.Bow, CostSubject.Performer), new(CostVerbs.Sacrifice, CostSubject.Performer)]);
        all[TempleBattle] = new("Battle", "Battle: Destroy a Terrain (if able). Put this Terrain into play", "none");
        all[TempleEngage] = new("Reaction", "Engage: Destroy a Terrain (if able). Put this Terrain into play", "none", Trigger: "after-engaging");
        all[ChagataiEnd] = new("Limited", "Limited: End the Action Phase. Discard this Celestial", "none"); // the discard is printed after the colon: an effect (Mendon, issue #1114)
        all[EmpireSeppuku] = new("Open", "Open: Your target Personality with a Madness token commits Seppuku", OwnMadnessPersonality, PlayerAbility: true);
        return all;
    }
}

/// <summary>A Drain of Effort in effect: until its phase of its turn ends (or two discards have been owed), each other action's player discards a card.</summary>
public sealed class Wave738Drain
{
    public string SourceId { get; set; } = "";
    public int PlayerId { get; set; }
    public int Turn { get; set; }
    public string Phase { get; set; } = "";
    /// <summary>The discards this effect has caused so far.</summary>
    public int Discards { get; set; }
}

public sealed partial class MatchState
{
    // 22940-22949 is reserved for batch 7 of the wirable-card waves (issue #738). Each is omitted from the saved game until it is used.
    [JsonPropertyOrder(22940)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<Wave738Drain>? Wave738Drains { get; set; }

    /// <summary>The Shugenja a Jade Championship permanently gave its two traits (a Force penalty to Shadowlands cards at his location; "after a Spell's action resolves, straighten him").</summary>
    [JsonPropertyOrder(22941)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Wave738JadeShugenja { get; set; }
}

public sealed partial class MatchEngine
{
    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>Whether any card in this match (in play or not) carries the Effect: the "somebody could care" test of a trait that watches a fact.</summary>
    private bool Wave738Has(string effect) => _state.Cards.Values.Any(c => Definition(c).Effect == effect);

    // ---- target legality --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool Wave738TargetAllows(TargetAllowsContext context) =>
        context.ability.Target == Wave738Abilities.OwnMadnessPersonality && InPlay(context.target) && Definition(context.target).Type == CardType.Personality && context.target.OwnerId == context.player.Id && HasToken(context.target, "Madness");

    // ---- validation -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private void Wave738Validate(ValidationHookContext context)
    {
        switch (context.id)
        {
            case Wave738Abilities.TempleBattle:
            case Wave738Abilities.TempleEngage:
                Require(context.source is { Zone: Zone.Hand }, "Temple Grounds is put into play from your hand.");
                break;
        }
    }

    // ---- effects --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? RunWave738(RunHookContext c)
    {
        var (id, player, source, target) = (c.id, c.player, c.source, c.target);
        switch (id)
        {
            // "Other players do not gain Honor for destroying your cards in this battle's resolution."
            case Wave738Abilities.TakazakiEngage:
                SetRuleFlag(RuleFlagKinds.NoDestructionHonorForOthers, RuleFlagScope.Battle, player.Id, Definition(source!).Name);
                return false;
            case Wave738Abilities.DrainWatch:
                (_state.Wave738Drains ??= []).RemoveAll(d => !Wave738DrainLive(d));
                _state.Wave738Drains.Add(new() { SourceId = source!.Id, PlayerId = player.Id, Turn = _state.TurnNumber, Phase = _state.Phase.ToString() });
                Emit("watchSet", $"{Definition(source).Name}: until this phase ends or two cards have been discarded from it, after each other action resolves its player discards a card.", player.Id, source.Id);
                return false;
            case Wave738Abilities.DrainDraw:
                Draw(player);
                return false;
            case Wave738Abilities.MarrowCurse:
                {
                    // "Give the target a -1F/-1C Poison token. Lose 3 Honor. Until the game ends, after each Events Phase begins, give the target a -1F/-1C Poison token."
                    CreateTwoStatToken(target!, "Poison", source!.Id, "Force", -1, "Chi", -1);
                    GainHonor(player.Id, -3, Definition(source).Name, source);
                    AddTurnScopedRule(Wave738Abilities.MarrowRule, null, TurnUntil.GameEnds(), Definition(source).Name, arg: source.Id, cardId: target!.Id);
                    return false;
                }
            case Wave738Abilities.TempleBattle:
            case Wave738Abilities.TempleEngage:
                {
                    if (_state.CurrentBattleProvince is not int battlefield || !PlayedFromHand(source)) return false;
                    // "Destroy a Terrain (if able)": the Terrain already at the battlefield is replaced.
                    foreach (var existing in _state.Cards.Values.Where(t => t.Zone == Zone.Battlefield && t.ProvinceIndex == battlefield && Keyword(t, "Terrain") && t.Id != source.Id).OrderBy(t => t.Id, StringComparer.Ordinal).Take(1).ToArray())
                        DiscardTerrain(existing);
                    PutTerrainIntoPlay(player, source);
                    // "If you do not control a Cavalry unit here, negate each Battle action taken by a player without units here": while this Terrain stays in play (P13).
                    SetRuleFlag(RuleFlagKinds.NegateBattleActionWithoutUnits, RuleFlagScope.Battlefield, player.Id, Definition(source).Name, battlefield: battlefield, whileInPlay: source.Id);
                    return false;
                }
            case Wave738Abilities.ChagataiEnd:
                SetRuleFlag(RuleFlagKinds.EndActionPhase, source: Definition(source!).Name);
                WlItemsMiscDiscardCelestial(source!); // "Discard this Celestial." -- an effect (issue #1114)
                return false;
            case Wave738Abilities.EmpireSeppuku:
                CommitSeppuku(target!, player.Id);
                return false;
            default:
                return null;
        }
    }

    // ---- turn begins: Tatsuki, Farmlands, the Dagger ------------------------------------------------------------------------------------------------------------------------------------

    private void Wave738TurnBegins(TurnBeginsContext turn)
    {
        // "After another player's turn begins, straighten Tatsuki."
        foreach (var tatsuki in TurnBeginCards(Wave738Abilities.TatsukiEffect, turn, TurnBeginScope.AnotherPlayers)) Straighten(tatsuki);
        // "After each player's turn begins, straighten this Holding."
        foreach (var farm in TurnBeginCards(Wave738Abilities.FarmlandsEffect, turn, TurnBeginScope.EachPlayers)) Straighten(farm);
        // "After your turn begins, give this Personality a +1F Obsession token; then, if he has more Obsession tokens than printed Chi, destroy him."
        foreach (var dagger in TurnBeginCards(Wave738Abilities.DaggerEffect, turn, TurnBeginScope.Yours))
        {
            if (dagger.AttachedTo is not { } hostId || !_state.Cards.TryGetValue(hostId, out var host) || !InPlay(host) || Definition(host).Type != CardType.Personality) continue;
            CreateMultiStatToken(host, "Obsession", dagger.Id, ("Force", 1));
            if (!InPlay(host) || TokenCount(host, "Obsession") <= Definition(host).Chi) continue;
            Emit("obsessionDestroyed", $"{Definition(dagger).Name}: {Definition(host).Name} has {TokenCount(host, "Obsession")} Obsession tokens, more than his printed Chi {Definition(host).Chi}.", dagger.OwnerId, host.Id);
            DestroyUnits([host], false, dagger.OwnerId);
        }
    }

    // ---- Suck the Marrow: "after each Events Phase begins" ---------------------------------------------------------------------------------------------------------------------------

    /// <summary>EventsPhase's call, as each Events Phase begins (either player's): each Suck the Marrow's target is given another -1F/-1C Poison token.</summary>
    private void Wave738EventsPhaseBegins()
    {
        foreach (var rule in TurnScopedRulesFor(Wave738Abilities.MarrowRule).ToArray())
            if (rule.CardId is { } targetId && _state.Cards.TryGetValue(targetId, out var target) && InPlay(target) && Definition(target).Type == CardType.Personality)
                CreateTwoStatToken(target, "Poison", rule.Arg ?? rule.Source, "Force", -1, "Chi", -1);
    }

    // ---- Lantern Oil: "The Attacker may not take Engage actions at this Item's battlefield" ------------------------------------------------------------------------------------------

    /// <summary>EngageActionBarred's term: a Lantern Oil is attached to a Personality at this battlefield.</summary>
    private bool Wave738LanternOilBars(int battlefield) =>
        _state.Cards.Values.Any(c => c.Zone == Zone.Attached && Definition(c).Effect == Wave738Abilities.LanternOilEffect && c.AttachedTo is { } hostId
            && _state.Cards.TryGetValue(hostId, out var host) && host.Zone == Zone.Battlefield && host.ProvinceIndex == battlefield);

    // ---- Events ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private bool? ResolveWave738Event(PlayerState player, ProvinceState province, CardInstance card)
    {
        var name = Definition(card).Name;
        switch (Definition(card).Effect)
        {
            case Wave738Abilities.DangerousGameEffect:
                return BeginRevealChoice(Wave738Abilities.DangerousGameChoice, player, card, [], $"{name}: you may discard your hand (even if it is empty). If you do, draw four cards.");
            case Wave738Abilities.EmpireEffect:
                // "Until the game ends, each player has the ability ... and any player who ends his turn with 20 or more Madness tokens on his Personalities loses the game."
                GrantEachPlayerAbility(Wave738Abilities.EmpireSeppuku, TurnUntil.GameEnds(), name);
                SetRuleFlag(RuleFlagKinds.MadnessLoss, RuleFlagScope.Game, null, name, amount: 20);
                return DiscardResolvedEvent(player, province, card);
            case Wave738Abilities.JourneysEndEffect:
                // "Defending players may not play Terrains (this turn)." The siege itself is armed for this player and this turn (Wave738ProvinceDestroyed).
                SetRuleFlag(RuleFlagKinds.DefendersMayNotPlayTerrains, RuleFlagScope.Turn, player.Id, name);
                AddPeriodCount("w738-siege:" + player.Id, Period.Turn);
                return DiscardResolvedEvent(player, province, card);
            case Wave738Abilities.OpenRecruitmentEffect:
                {
                    // "Until your Events Phase two turns from now begins": read as until your second turn from now begins (nothing turns face up between a turn's beginning and its Events Phase).
                    foreach (var old in TurnScopedRulesFor(Wave738Abilities.OpenRecruitmentRule, player.Id).Where(r => r.CardId == card.Id).ToArray()) RemoveTurnScopedRule(old.Id);
                    AddTurnScopedRule(Wave738Abilities.OpenRecruitmentRule, player.Id, UntilOwnersSecondTurnBegins(player.Id), name, cardId: card.Id);
                    return DiscardResolvedEvent(player, province, card);
                }
            case Wave738Abilities.RejectedMediationEffect:
                {
                    // "Any player who controls the Imperial Favor discards it."
                    if (_state.FavorPlayerId is int holder)
                    {
                        ReleaseImperialFavor(holder, asCost: false);
                        Emit("favorDiscarded", $"{name}: {Player(holder).Name} discards the Imperial Favor.", holder, card.Id);
                    }
                    // "Until your second turn from now begins, negate the effects of the first Political action taken each turn": a player-level watch of any player's action, once each turn. Turns alternate, so the
                    // turn before this player's second turn from now begins is three turns on.
                    AddNegationWatch(new NegationWatch
                    {
                        Id = "w738-rejected-mediation-" + card.Id,
                        OwnerPlayerId = player.Id,
                        Kind = WatchKind.PoliticalAction,
                        Source = WatchSource.Any,
                        Remaining = -1,
                        Refresh = Period.Turn,
                        LastTurn = _state.TurnNumber + 3
                    });
                    Emit("watchSet", $"{name}: until {player.Name}'s second turn from now begins, the effects of the first Political action taken each turn are negated.", player.Id, card.Id);
                    return DiscardResolvedEvent(player, province, card);
                }
            case Wave738Abilities.JadeEffect:
                {
                    var shugenja = _state.Cards.Values.Where(s => s.OwnerId == player.Id && InPlay(s) && Definition(s).Type == CardType.Personality && Keyword(s, "Shugenja")).Select(s => s.Id).Order(StringComparer.Ordinal).ToList();
                    if (shugenja.Count == 0) return DiscardResolvedEvent(player, province, card);
                    if (shugenja.Count == 1)
                    {
                        Wave738MarkJade(Card(shugenja[0]), name);
                        return DiscardResolvedEvent(player, province, card);
                    }
                    return BeginRevealChoice(Wave738Abilities.JadeChoice, player, card, shugenja, $"{name}: choose one of your Shugenja in play; he permanently gains its two traits.");
                }
            case Wave738Abilities.MadnessSpreadsEffect:
                SetRuleFlag(RuleFlagKinds.LimitedAsOpenOncePerTurn, RuleFlagScope.Player, player.Id, name, until: UntilOwnersSecondTurnBegins(player.Id));
                return DiscardResolvedEvent(player, province, card);
            default:
                return null;
        }
    }

    private void Wave738MarkJade(CardInstance shugenja, string source)
    {
        var marked = _state.Wave738JadeShugenja ??= [];
        if (!marked.Contains(shugenja.Id)) marked.Add(shugenja.Id);
        Emit("traitsGranted", $"{source}: {Definition(shugenja).Name} permanently gains the traits \"Shadowlands Personalities and Followers have -1F while at this Personality's location\" and \"After the resolution of an action from a Spell: Straighten this Personality\".", shugenja.OwnerId, shugenja.Id);
    }

    // The answer to an Event decision of this file (the default arm of ResolveEventChoiceCommand). True when this handled it.
    private bool ResolveWave738EventChoice(PendingEventChoice pending, GameCommand command)
    {
        if (pending.Kind is not (Wave738Abilities.DangerousGameChoice or Wave738Abilities.JadeChoice)) return false;
        var card = Card(pending.EventCardId); var player = Player(pending.PlayerId);
        if (pending.Kind == Wave738Abilities.DangerousGameChoice)
        {
            Require(command.Choice is "yes" or "no", "Choose whether to discard your hand.");
            var discarded = command.Choice == "yes";
            if (discarded)
            {
                // "You may discard your hand (even if it is empty). If you did, draw four cards."
                PromoItemsNoteHandDiscard(player.Hand.Count);
                foreach (var handId in player.Hand.ToArray()) DiscardFate(player, Card(handId));
                for (var i = 0; i < 4; i++) Draw(player);
            }
            FinishRevealedEvent(card, message: discarded ? $"{Definition(card).Name}: {player.Name} discarded his hand and drew four cards." : $"{Definition(card).Name}: {player.Name} kept his hand.");
            return true;
        }
        Require(command.TargetId is not null && pending.Candidates.Contains(command.TargetId), "Choose one of your Shugenja in play.");
        Wave738MarkJade(Card(command.TargetId!), Definition(card).Name);
        FinishRevealedEvent(card, message: $"{Definition(card).Name} resolved and was discarded.");
        return true;
    }

    private IEnumerable<ActionOption> Wave738EventChoiceActions(PendingEventChoice pending, int playerId)
    {
        if (pending.Kind is not (Wave738Abilities.DangerousGameChoice or Wave738Abilities.JadeChoice)) yield break;
        var name = Definition(Card(pending.EventCardId)).Name;
        GameCommand Answer(string? target = null, string? choice = null) => new("eventChoice", playerId, _state.Version, CardId: pending.EventCardId, TargetId: target, Choice: choice);
        if (pending.Kind == Wave738Abilities.DangerousGameChoice)
        {
            yield return new("eventChoice", $"{name}: discard your hand and draw four cards", true, null, Answer(choice: "yes"), pending.EventCardId);
            yield return new("eventChoice", $"{name}: keep your hand", true, null, Answer(choice: "no"), pending.EventCardId);
            yield break;
        }
        foreach (var id in pending.Candidates)
            yield return new("eventChoice", $"{name}: {Definition(Card(id)).Name}", true, null, Answer(id), id);
    }

    // ---- Journey's End Siege: the first Province destroyed in battle this turn -----------------------------------------------------------------------------------------------------

    /// <summary>The battle resolution's call, once a Province was (or was not) destroyed. After the first time this turn the siege's player destroys a Province in battle, if he does not have more Provinces than the
    /// Defender (at that time), he gains a Province (to the left of his leftmost, as every Province gained at any time).</summary>
    private void Wave738ProvinceDestroyed(int attacker, bool provinceDestroyed)
    {
        if (!provinceDestroyed || PeriodCountOf("w738-siege:" + attacker, Period.Turn) == 0 || PeriodCountOf("w738-siege-done:" + attacker, Period.Turn) > 0) return;
        AddPeriodCount("w738-siege-done:" + attacker, Period.Turn);
        var mine = Player(attacker).Provinces.Count(p => !p.Destroyed);
        var theirs = Player(1 - attacker).Provinces.Count(p => !p.Destroyed);
        if (mine > theirs)
        {
            Emit("eventResolved", $"Journey's End Siege: {Player(attacker).Name} has {mine} Provinces to {Player(1 - attacker).Name}'s {theirs}, so he gains none.", attacker);
            return;
        }
        Emit("eventResolved", $"Journey's End Siege: {Player(attacker).Name} has {mine} Provinces to {Player(1 - attacker).Name}'s {theirs}, so he gains a Province.", attacker);
        GainProvince(Player(attacker), left: true);
    }

    // ---- Open Recruitment: after an unaligned Human Personality turns face up in one of your provinces -------------------------------------------------------------------------

    private void Wave738TurnedFaceUpFact(FactRecord fact)
    {
        if (fact.CardId is null || !_state.Cards.TryGetValue(fact.CardId, out var card) || Definition(card).Type != CardType.Personality) return;
        var rule = TurnScopedRulesFor(Wave738Abilities.OpenRecruitmentRule, fact.PlayerId).FirstOrDefault(r => r.PlayerId == fact.PlayerId);
        if (rule is null || !Keyword(card, "Unaligned") || !KeywordOrHuman(card, "Human")) return;
        if (GrantClanAlignment(card, Player(fact.PlayerId).Clan, rule.Source)) Emit("clanAligned", $"{rule.Source}: {Definition(card).Name} has sworn fealty to {Player(fact.PlayerId).Name} and permanently gains the {Player(fact.PlayerId).Clan} Clan alignment.", fact.PlayerId, card.Id);
    }

    // ---- after-action traits: Drain of Effort and the Jade Championship ----------------------------------------------------------------------------------------------------------

    private bool Wave738DrainLive(Wave738Drain drain) => drain.Turn == _state.TurnNumber && drain.Phase == _state.Phase.ToString() && drain.Discards < 2;

    private void Wave738AfterAction(ResolvedActionFacts facts)
    {
        // Drain of Effort: "after each action other than this one resolves, its player discards a card", until two cards have been discarded from it.
        if (_state.Wave738Drains is { Count: > 0 } drains)
            foreach (var drain in drains.Where(Wave738DrainLive).ToArray())
            {
                if (facts.AbilityId == Wave738Abilities.DrainWatch && facts.SourceId == drain.SourceId) continue;
                var acting = Player(facts.PlayerId);
                if (acting.Hand.Count <= _state.PendingForcedDiscards.Count(d => d.PlayerId == acting.Id) || P2HandDiscardBlocked(acting.Id, drain.PlayerId, false, "Drain of Effort")) continue;
                drain.Discards++;
                _state.PendingForcedDiscards.Add(new(acting.Id, "Drain of Effort"));
            }
        // The Jade Championship: "After the resolution of an action from a Spell: Straighten this Personality."
        if (_state.Wave738JadeShugenja is { Count: > 0 } marked && facts.SourceId is { } spellId && _state.Cards.TryGetValue(spellId, out var spell) && spell is { Zone: Zone.Attached, AttachedTo: { } wielderId }
            && Definition(spell).Type == CardType.Spell && marked.Contains(wielderId) && _state.Cards.TryGetValue(wielderId, out var wielder) && InPlay(wielder))
            Straighten(wielder);
    }

    // ---- the Jade Championship's Force term -------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"Shadowlands Personalities and Followers have -1F while at this Personality's location": -1 for each marked Shugenja in play at the card's location (a Follower's is its host's).</summary>
    private int Wave738JadeForce(CardInstance card)
    {
        if (_state.Wave738JadeShugenja is not { Count: > 0 } marked || !(Keyword(card, "Shadowlands") || HasClanAffiliation(card, "Shadowlands"))) return 0;
        var located = Definition(card).Type == CardType.Follower ? (card.AttachedTo is { } hostId && _state.Cards.TryGetValue(hostId, out var host) ? host : null) : card;
        if (located is null) return 0;
        return -marked.Count(id => _state.Cards.TryGetValue(id, out var shugenja) && InPlay(shugenja) && SameLocation(shugenja, located));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterWave738Hooks()
    {
        EngineHooks.Abilities.Register("Wave738Abilities", 13800, static abilities => abilities.AddWave738());
        EngineHooks.PlayerGrantAbilities.Register("Wave738PlayerGrants", 13800, static () => [Wave738Abilities.EmpireSeppuku]);
        EngineHooks.TargetAllows.Register("Wave738TargetAllows", 13800, static (engine, context) => engine.Wave738TargetAllows(context));
        EngineHooks.ValidateEarly.Register("Wave738Validate", 13800, static (engine, context) => engine.Wave738Validate(context));
        EngineHooks.RunEffects.Register("Wave738RunEffects", 13800, static (engine, context) => engine.RunWave738(context));
        EngineHooks.ResolveEvent.Register("Wave738ResolveEvent", 13800, static (engine, context) => engine.ResolveWave738Event(context.Player, context.Province, context.Card));
        EngineHooks.TurnBegins.Register("Wave738TurnBegins", 13800, static (engine, turn) => { engine.Wave738TurnBegins(turn); return false; });
        EngineHooks.AfterActionTraits.Register("Wave738AfterAction", 13800, static (engine, facts) => engine.Wave738AfterAction(facts));
        EngineHooks.PersonalityForce.Register("Wave738PersonalityForce", 13800, static (engine, context) => engine.Wave738JadeForce(context.card));
        EngineHooks.FollowerForce.Register("Wave738FollowerForce", 13800, static (engine, context) => engine.Wave738JadeForce(context.card));

        // P6: Open Recruitment's trait watches the turned-face-up fact only in a match that has the card.
        FactRegistry.Trait("Wave738TurnedFaceUp", 13800, FactKinds.TurnedFaceUp, static (engine, fact) => engine.Wave738TurnedFaceUpFact(fact), activeIn: static engine => engine.Wave738Has(Wave738Abilities.OpenRecruitmentEffect));
    }
}
