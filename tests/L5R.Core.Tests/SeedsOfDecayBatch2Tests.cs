using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Seeds of Decay pass (issue #453), batch 2: strategy-wave-50 (seventeen Strategies), personality-wave-53 (seven Personalities) and follower-wave-24 (the created
// Ronin Follower). Every card is driven through QueryActions and the commands it offers; each primitive of SeedsOfDecayBatch2.cs has a scenario that fails without it.
internal static class SeedsOfDecayBatch2Tests
{
    private const string CourtingTrouble = "oracle-l5r-10140", OutOfMySight = "oracle-l5r-10162", DebtToTheKaiu = "oracle-l5r-10141", ImperialProtection = "oracle-l5r-10152",
        IncriminatingTestimony = "oracle-l5r-10153", Ingenuity = "oracle-l5r-10154", Testimony = "oracle-l5r-10171", SharingGossip = "oracle-l5r-10166", Urgency = "oracle-l5r-10179",
        TurningTheStory = "oracle-l5r-10174", FairWarning = "oracle-l5r-10149", SentinelSpirit = "oracle-l5r-10165", InsultAnswered = "oracle-l5r-10136", ManipulateEnergies = "oracle-l5r-10156",
        DragonTattooExp = "oracle-l5r-10144", DubiousResources = "oracle-l5r-10145", BrotherhoodSchism = "oracle-l5r-10138",
        Baiko = "oracle-l5r-10044", TsujikenExp = "oracle-l5r-10045", Yuka = "oracle-l5r-10050", Yusugi = "oracle-l5r-10051", Ugimori = "oracle-l5r-10084", Hotaka = "oracle-l5r-10059",
        ShibataExp = "oracle-l5r-10080", RoninFollower = "oracle-l5r-10203";

    public static void Run(TestRunner runner) => runner.Run("seeds-batch-2", [
        ("Published shapes: the three waves pin exactly their cards, each the printed card with the abilities and Effect named for it, and each ships its art", PublishedShapes),
        ("Timed province strength: Incriminating Testimony bows its target, bows the performing Courtier at any location and gives the battlefield's province the Courtier's Chi for the turn -- gone once the turn ends, still under Times of Strife's ceiling", IncriminatingTestimonyStrength),
        ("Bayushi Shibata - exp bows an attacking Personality from away from the battlefield and gives the province +3 only when the target is dishonorable; Debt to the Kaiu's +1 is permanent", ShibataAndKaiu),
        ("Imperial Protection: the watched Personality is dishonored and his controller loses his base Personal Honor after he is next assigned to attack this turn (not as a defender, not next turn); the province rule counts each dishonorable attacker for the turn", ImperialProtectionWatchAndProvinces),
        ("Fair Warning's watch pays 2 Honor when the target is assigned to attack or destroyed this turn; its Focus Effect lets the winner's player give a Personality -4F, and asks nobody when his Personality lost", FairWarningWatchAndFocus),
        ("Courting Trouble gives Overconfident permanently to a face-up Personality in a province (and in play), never a face-down or own one; Out of My Sight! gives Brash permanently and the Defender draws when he attacks", PermanentKeywordGrants),
        ("Manipulate Energies: the Henshin gives what the target lacks or removes what he has, permanently (a removal beats a later Turn grant, a later give restores it), one use per turn", ManipulateEnergiesGiveAndRemove),
        ("Ingenuity dishonors one or two Personalities and draws an additional card in its player's next End Phase; Testimony's honorable performer dishonors or rehonors a lower-base-Personal-Honor target only; Sharing Gossip counts every copy in the discard piles", IngenuityTestimonyGossip),
        ("Urgency limits each player to two more non-Reaction actions this phase (Reactions and the next phase are free); its Limited half pays 2 Gold for a card", UrgencyAllowance),
        ("Turning the Story negates a Proclaim before it resolves: no Honor, the Proclaim spent, the Proclaim window resumed; Kakita Yusugi may be Proclaimed after a Proclaim this turn; Yogo Ugimori locks a chosen player's Proclaim until his controller's next turn begins", ProclaimNegationLimitAndLock),
        ("Sentinel Spirit: usable only on another player's turn while he controls a Personality; when the next Dynasty Phase begins, 2 Honor if the Spirit was opposed, 3 more if no unit attacked and its player's Honor is lowest", SentinelSpiritWatch),
        ("An Insult Answered: after another player's performed Limited action cost you Honor, your Duelist duels its performer (an Iaijutsu duel), the loser is destroyed and the winner gets two +1F Revenge tokens; not after a harmless action", InsultAnsweredDuel),
        ("Dragon Tattoo - exp grants its Tattooed performer the Fire token ability permanently, once per game per Personality; Dubious Resources creates the 2F Ronin Follower on its performer; Brotherhood Schism bars a second Ring this turn and shuffles an enemy discard-pile Event into its deck", TattooResourcesSchism),
        ("Yasuki Baiko and Yasuki Tsujiken - exp: the Favor is discarded to cost a dishonorable target's controller 1 Honor, 2 Gold takes the Favor; Tsujiken bows or straightens a Holding or his own Stronghold (not the enemy's), draws for 5 Gold and dishonors for his bow", BaikoAndTsujiken),
        ("Doji Yuka has +3C while her Political action resolves (Incriminating Testimony reads it) and not afterwards; Akodo Hotaka has +1PH against a Dragon player, may not perform Dark Virtue actions, and lowers his controller's Honor Victory to 38", YukaAndHotaka),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static int Strength(MatchEngine e, int player, int province) => e.GetView(player).Players[player].Provinces[province].Strength;
    private static void Decline(MatchEngine e) => Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    private static void Gold(MatchState s, int player, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", player, Zone.Home); }

    // A pending duel driven to its end: each player focuses from the deck, then the striker strikes.
    private static void PlayOutDuel(MatchEngine e)
    {
        var focused = new HashSet<int>();
        for (var guard = 0; e.State.PendingDuel is not null && guard < 12; guard++)
        {
            if (e.State.PendingChoice is { Type: "duelReaction" }) { Decline(e); continue; }
            if (e.State.PendingChoice is not null) break;
            var p = e.State.PriorityPlayerId;
            var options = e.QueryActions(p);
            var focus = options.FirstOrDefault(o => o.Type == "focus" && o.Command?.Choice == "deck");
            if (focus is not null && focused.Add(p)) { Accept(e, focus.Command!); continue; }
            var strike = options.FirstOrDefault(o => o.Type == "strike");
            if (strike is not null) { Accept(e, strike.Command!); continue; }
            var accept = options.FirstOrDefault(o => o.Type == "acceptDuel");
            if (accept is not null) { Accept(e, accept.Command!); continue; }
            var keep = options.FirstOrDefault(o => o.Type == "duelistRefocus" && o.Command?.CardId is null);
            if (keep is not null) { Accept(e, keep.Command!); continue; }
            break;
        }
    }

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        void Shipped(string id)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        var strategies = new (string Id, string Name, int Focus, int Cost, string[] Abilities)[]
        {
            (CourtingTrouble, "Courting Trouble", 4, 0, ["courting-trouble-overconfident"]), (OutOfMySight, "Out of My Sight!", 4, 0, ["out-of-my-sight-brash"]),
            (DebtToTheKaiu, "Debt to the Kaiu", 2, 0, ["debt-to-the-kaiu-strength"]), (ImperialProtection, "Imperial Protection", 3, 0, ["imperial-protection-watch", "imperial-protection-provinces"]),
            (IncriminatingTestimony, "Incriminating Testimony", 2, 0, ["incriminating-testimony-bow"]), (Ingenuity, "Ingenuity", 3, 0, ["ingenuity-dishonor"]),
            (Testimony, "Testimony", 2, 0, ["testimony-dishonor", "testimony-rehonor"]), (SharingGossip, "Sharing Gossip", 1, 0, ["sharing-gossip-honor"]),
            (Urgency, "Urgency", 3, 0, ["urgency-limit", "urgency-draw"]), (TurningTheStory, "Turning the Story", 3, 0, ["turning-the-story-negate"]),
            (FairWarning, "Fair Warning", 3, 0, ["fair-warning-focus", "fair-warning-watch"]), (SentinelSpirit, "Sentinel Spirit", 3, 0, ["sentinel-spirit-watch"]),
            (InsultAnswered, "An Insult Answered", 3, 0, ["insult-answered-duel"]),
            (ManipulateEnergies, "Manipulate Energies", 4, 0, ["manipulate-energies-give-cavalry", "manipulate-energies-give-naval", "manipulate-energies-give-tactician", "manipulate-energies-remove-cavalry", "manipulate-energies-remove-naval", "manipulate-energies-remove-tactician"]),
            (DragonTattooExp, "Dragon Tattoo", 2, 0, ["dragon-tattoo-exp-imbue"]), (DubiousResources, "Dubious Resources", 1, 2, ["dubious-resources-create"]),
            (BrotherhoodSchism, "Brotherhood Schism", 3, 0, ["brotherhood-schism-rings", "brotherhood-schism-shuffle"]),
        };
        foreach (var (id, name, focus, cost, abilities) in strategies)
        {
            var def = CardCatalog.Get(id);
            Check(def.Type == CardType.Strategy && def.Name == name && def.Focus == focus && def.GoldCost == cost && !def.ScenarioOnly && (def.Abilities ?? []).SequenceEqual(abilities), $"{name}: {def.Focus}/{def.GoldCost}/{string.Join(",", def.Abilities ?? [])}");
            Shipped(id);
        }
        foreach (var (id, kw) in new[] { (ImperialProtection, "Political"), (IncriminatingTestimony, "Political"), (Ingenuity, "Political"), (Testimony, "Political"), (SharingGossip, "Political"), (Urgency, "Political"), (TurningTheStory, "Political"), (InsultAnswered, "Iaijutsu"), (InsultAnswered, "Revenge"), (ManipulateEnergies, "Kiho"), (DragonTattooExp, "Tattoo"), (DragonTattooExp, "Fire"), (DragonTattooExp, "Experienced") })
            Check(CardCatalog.Get(id).Keywords!.Contains(kw), $"{id} prints {kw}");
        Check(AbilityCatalog.All["insult-answered-duel"] is { IssuesDuel: true, Timing: "Reaction" } insult && insult.ActionKeywords!.Contains("Iaijutsu"), "An Insult Answered is an Iaijutsu duel Reaction");
        Check(AbilityCatalog.All["fair-warning-focus"].Timing == "FocusEffect" && AbilityCatalog.All["turning-the-story-negate"].Trigger == "before-proclaim-resolves", "Fair Warning's Focus Effect, Turning the Story's trigger");
        Check(AbilityCatalog.All["dubious-resources-create"].Creates is { Type: CardType.Follower, Force: 2, ProxyId: RoninFollower }, "Dubious Resources creates the Ronin Follower scan");

        var personalities = new (string Id, string Name, string Clan, int F, int C, int PH, int Cost, int? HR, string Effect, string[] Abilities)[]
        {
            (Baiko, "Yasuki Baiko", "Crab", 0, 3, 1, 6, null, "none", ["baiko-shame", "baiko-favor"]),
            (TsujikenExp, "Yasuki Tsujiken", "Crab", 3, 5, 0, 11, null, "none", ["tsujiken-exp-bow", "tsujiken-exp-straighten", "tsujiken-exp-draw", "tsujiken-exp-dishonor"]),
            (Yuka, "Doji Yuka", "Crane", 1, 3, 4, 5, 10, "yuka-political-chi", []),
            (Yusugi, "Kakita Yusugi", "Crane", 2, 3, 2, 4, 6, "proclaim-ignores-turn-limit", []),
            (Ugimori, "Yogo Ugimori", "Scorpion", 3, 4, 2, 7, null, "none", ["ugimori-proclaim-lock"]),
            (Hotaka, "Akodo Hotaka", "Lion", 3, 3, 3, 7, 7, "hotaka-paragon", []),
            (ShibataExp, "Bayushi Shibata", "Scorpion", 1, 4, 0, 8, null, "none", ["shibata-exp-bow"]),
        };
        foreach (var (id, name, clan, f, c, ph, cost, hr, effect, abilities) in personalities)
        {
            var def = CardCatalog.Get(id);
            Check(def.Type == CardType.Personality && def.Name == name && def.Clan == clan && def.Force == f && def.Chi == c && def.PersonalHonor == ph && def.GoldCost == cost && def.HonorRequirement == hr
                && def.Effect == effect && (def.Abilities ?? []).SequenceEqual(abilities), $"{name}: {def.Clan} {def.Force}/{def.Chi}/{def.PersonalHonor} cost {def.GoldCost} HR {def.HonorRequirement} {def.Effect}");
            Shipped(id);
        }
        Check(CardCatalog.Get(TsujikenExp).Keywords!.Contains("Experienced") && CardCatalog.Get(ShibataExp).Keywords!.Contains("Experienced") && CardCatalog.Get(Hotaka).Keywords!.Contains("Paragon")
            && CardCatalog.Get(Yusugi).Keywords!.Contains("Duelist") && CardCatalog.Get(Ugimori).Keywords!.Contains("Shugenja") && CardCatalog.Get(Baiko).Keywords!.Contains("Courtier"), "printed keywords");
        var ronin = CardCatalog.Get(RoninFollower);
        Check(ronin.Type == CardType.Follower && ronin.CreatedOnly && ronin.Name == "Ronin Follower" && ronin.Force == 2 && ronin.GoldCost == 0 && (ronin.Keywords ?? []).SequenceEqual(["Ronin"]), "the Ronin Follower is the created-only 2F Ronin");
        Shipped(RoninFollower);
        foreach (var (wave, ids) in new[] { ("strategy-wave-50", strategies.Select(x => x.Id).ToArray()), ("personality-wave-53", personalities.Select(x => x.Id).ToArray()), ("follower-wave-24", new[] { RoninFollower }) })
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).SequenceEqual(ids), wave + " pins exactly its cards");
    }

    private static void IncriminatingTestimonyStrength()
    {
        // Player 0 defends province 0 (Table: player 1 attacks); a Courtier of player 0's at home performs from "any location".
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var courtier = Add(s, "training-courtier-personality", 0, Zone.Home); // Chi 2
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, IncriminatingTestimony, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = Strength(e, 0, 0);
        Accept(e, Offer(e, 0, "incriminating-testimony-bow", card.Id, attacker.Id, courtier.Id));
        Check(e.State.Cards[attacker.Id].Bowed && e.State.Cards[courtier.Id].Bowed, "the target and the performing Courtier are bowed");
        Eq(Strength(e, 0, 0), before + 2, "the province has the Courtier's Chi more strength");
        Eq(Strength(e, 0, 1), before, "only this battlefield's province");
        Check(e.State.Players[0].Provinces[0].Strength == before, "the permanent strength is untouched: the bonus is a timed effect");
        // The bonus ends with the turn.
        var t = e.State; t.TurnNumber++;
        var later = MatchEngine.FromTestState(t);
        Eq(Strength(later, 0, 0), before, "gone once the turn ended");
        // Times of Strife's ceiling caps the timed bonus like any other.
        var capped = Edit(e, st => Add(st, "oracle-l5r-8494", 1, Zone.EventInPlay));
        Eq(Strength(capped, 0, 0), before, "under Times of Strife the read is the printed strength");
        // A bowed Courtier cannot perform; an own Personality is not a target.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bowedCourtier = Add(s2, "training-courtier-personality", 0, Zone.Home, bowed: true);
        var own = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card2 = Add(s2, IncriminatingTestimony, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "incriminating-testimony-bow", card2.Id), "a bowed Courtier cannot pay the cost");
    }

    private static void ShibataAndKaiu()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var shibata = Add(s, ShibataExp, 0, Zone.Home); // away from the battlefield
        var honest = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var dishonorable = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); dishonorable.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var before = Strength(e, 0, 0);
        Accept(e, Offer(e, 0, "shibata-exp-bow", shibata.Id, honest.Id));
        Check(e.State.Cards[honest.Id].Bowed, "an honorable attacker is bowed");
        Eq(Strength(e, 0, 0), before, "no strength for an honorable target");
        var e2 = Edit(e, st => { st.Cards[shibata.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; });
        Accept(e2, Offer(e2, 0, "shibata-exp-bow", shibata.Id, dishonorable.Id));
        Eq(Strength(e2, 0, 0), before + 3, "+3 for a dishonorable target, for the turn");
        var t = e2.State; t.TurnNumber++;
        Eq(Strength(MatchEngine.FromTestState(t), 0, 0), before, "gone with the turn");
        // Only an attacking Personality at the battlefield: a defender of his own side is refused.
        var e3 = Edit(e, st => { st.Cards[shibata.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; });
        var ownDefender = e3.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Reject(e3, Use(e3, 0, "shibata-exp-bow", shibata.Id, ownDefender.Id));

        // Debt to the Kaiu: permanent, every province of the acting player's.
        var s4 = Table(Phase.Action, 0);
        var kaiu = Add(s4, DebtToTheKaiu, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        var printed = Strength(e4, 0, 0);
        Accept(e4, Enabled(e4, 0, "debt-to-the-kaiu-strength", kaiu.Id).Command!);
        Check(e4.State.Players[0].Provinces.All(p => p.Strength == printed + 1) && e4.State.Players[1].Provinces.All(p => p.Strength == printed), "each of the player's provinces has +1, permanently, and not the opponent's");
        var t4 = e4.State; t4.TurnNumber++;
        Eq(Strength(MatchEngine.FromTestState(t4), 0, 0), printed + 1, "still there next turn");
    }

    private static void ImperialProtectionWatchAndProvinces()
    {
        // The watch: player 1's Personality, set on player 1's own Action phase; he is then assigned to attack (RunUntil attacks province 0 with everything).
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var target = Add(s, "training-lion-samurai", 1, Zone.Home); // base PH 2
        var card = Add(s, ImperialProtection, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 1);
        Accept(e, Offer(e, 0, "imperial-protection-watch", card.Id, target.Id));
        Eq(e.State.AssignmentWatches.Count, 1, "the watch stands");
        RunUntil(e, () => e.State.Cards[target.Id].Dishonorable || e.State.Phase == Phase.Dynasty);
        Check(e.State.Cards[target.Id].Dishonorable, "dishonored once assigned to attack");
        Eq(Honor(e, 1), honorBefore - 2, "his controller lost his base Personal Honor");
        Eq(e.State.AssignmentWatches.Count, 0, "the watch fired once and is gone");

        // Not a defender's assignment, and not next turn: a watch set on the Defender's own Personality never fires this turn.
        var s2 = Table(Phase.Action, 1); s2.PriorityPlayerId = 0;
        var mine = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Home);
        var card2 = Add(s2, ImperialProtection, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "imperial-protection-watch", card2.Id, mine.Id));
        RunUntil(e2, () => e2.State.Phase == Phase.Dynasty);
        Check(!e2.State.Cards[mine.Id].Dishonorable, "defending is not being assigned to attack");
        var t2 = e2.State; t2.TurnNumber++;
        var e2b = MatchEngine.FromTestState(t2);
        Check(e2b.State.AssignmentWatches.All(w => w.Turn != e2b.State.TurnNumber), "a watch of a past turn is dead");

        // The province rule: +2 per dishonorable attacker at each province, for the turn.
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 0;
        Add(s3, "training-crane-samurai", 0, Zone.Home);
        var a1 = Add(s3, "training-lion-samurai", 1, Zone.Home); a1.Dishonorable = true;
        var a2 = Add(s3, "training-lion-veteran", 1, Zone.Home); a2.Dishonorable = true;
        Add(s3, "training-lion-samurai", 1, Zone.Home); // honorable
        var card3 = Add(s3, ImperialProtection, 0, Zone.Hand);
        var e3a = MatchEngine.FromTestState(s3);
        var before = Strength(e3a, 0, 0);
        Accept(e3a, Enabled(e3a, 0, "imperial-protection-provinces", card3.Id).Command!);
        Eq(Strength(e3a, 0, 0), before, "nobody attacks yet: nothing");
        // The battle of the same turn: everyone at province 0.
        var e3 = Edit(e3a, st =>
        {
            st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0];
            foreach (var c in st.Cards.Values.Where(c => c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality)) { c.Zone = Zone.Battlefield; c.ProvinceIndex = 0; }
        });
        Eq(Strength(e3, 0, 0), before + 4, "two dishonorable attackers: +4");
        Eq(Strength(e3, 0, 1), before, "a province nobody attacks: nothing");
        var e3b = Edit(e3, st => st.Cards[a1.Id].Dishonorable = false);
        Eq(Strength(e3b, 0, 0), before + 2, "read live: one rehonored, +2");
        var t3 = e3.State; t3.TurnNumber++;
        Eq(Strength(MatchEngine.FromTestState(t3), 0, 0), before, "over with the turn");
    }

    private static void FairWarningWatchAndFocus()
    {
        // Assigned to attack: +2 Honor to the watcher's player.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var card = Add(s, FairWarning, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 0);
        Accept(e, Offer(e, 0, "fair-warning-watch", card.Id, target.Id));
        RunUntil(e, () => Honor(e, 0) != honorBefore || e.State.Phase == Phase.Dynasty);
        Eq(Honor(e, 0), honorBefore + 2, "2 Honor once he attacked");
        // Destroyed: the same 2 Honor, once.
        var s2 = Table(Phase.Action, 1); s2.PriorityPlayerId = 0;
        var destroyer = Add(s2, "training-no-attachments-destroyer", 0, Zone.Home);
        var victim = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var card2 = Add(s2, FairWarning, 0, Zone.Hand);
        var e2a = MatchEngine.FromTestState(s2);
        var h2 = Honor(e2a, 0);
        Accept(e2a, Offer(e2a, 0, "fair-warning-watch", card2.Id, victim.Id));
        var e2 = Edit(e2a, st =>
        {
            st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 0;
            foreach (var id in new[] { destroyer.Id, victim.Id }) { st.Cards[id].Zone = Zone.Battlefield; st.Cards[id].ProvinceIndex = 0; }
        });
        Accept(e2, Offer(e2, 0, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(Honor(e2, 0), h2 + 2, "2 Honor when he is destroyed");
        Reject(e2, Use(e2, 0, "fair-warning-watch", card2.Id, victim.Id));
        // An own Personality is not a target.
        var s3 = Table(Phase.Action, 0);
        var own = Add(s3, "training-crane-samurai", 0, Zone.Home);
        var card3 = Add(s3, FairWarning, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Reject(e3, Use(e3, 0, "fair-warning-watch", card3.Id, own.Id));

        // The Focus Effect: player 0 challenges and wins, focusing Fair Warning; he may give a Personality -4F.
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var mine = Add(s4, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bystander = Add(s4, "training-lion-veteran", 1, Zone.Home);
        var duel = Add(s4, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s4, FairWarning, 0, Zone.Hand);
        var top = Add(s4, "training-focus-strategy", 1, Zone.FateDeck); s4.Players[1].FateDeck.Insert(0, top.Id);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e4, new GameCommand("focus", 1, e4.Version, Choice: "deck"));
        Accept(e4, new GameCommand("focus", 0, e4.Version, CardId: focused.Id));
        Accept(e4, new GameCommand("strike", 1, e4.Version));
        Eq(e4.State.PendingChoice?.Type, "focusEffectChoice", "the winner's player is asked");
        Eq(e4.State.PendingChoice?.PlayerId, 0);
        var forceBefore = e4.CardForceForTest(bystander.Id);
        Accept(e4, e4.QueryActions(0).Single(o => o.Type == "chooseFocusEffect" && o.CardId == bystander.Id).Command!);
        Eq(e4.CardForceForTest(bystander.Id), Math.Max(0, forceBefore - 4), "-4F on the chosen Personality");
        Check(e4.State.PendingDuel is null, "the duel finished");
        // Lost: nobody is asked.
        var s5 = Table(Phase.Battle, 0); s5.PriorityPlayerId = 0;
        var weak = Add(s5, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strong = Add(s5, "training-paneki-exp3", 1, Zone.Battlefield, province: 0);
        var duel5 = Add(s5, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused5 = Add(s5, FairWarning, 0, Zone.Hand);
        var top5 = Add(s5, "training-focus-strategy", 1, Zone.FateDeck); s5.Players[1].FateDeck.Insert(0, top5.Id);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "steel-on-steel-duel", duel5.Id, strong.Id, weak.Id));
        Accept(e5, new GameCommand("focus", 1, e5.Version, Choice: "deck"));
        Accept(e5, new GameCommand("focus", 0, e5.Version, CardId: focused5.Id));
        Accept(e5, new GameCommand("strike", 1, e5.Version));
        Check(e5.State.PendingChoice is null && e5.State.PendingDuel is null, "his Personality lost: no choice, the duel finished");
    }

    private static void PermanentKeywordGrants()
    {
        var s = Table(Phase.Action, 0);
        var inProvince = s.Cards[s.Players[1].Provinces[0].CardId!]; inProvince.DefinitionId = "training-lion-samurai"; inProvince.FaceUp = true;
        var faceDown = s.Cards[s.Players[1].Provinces[1].CardId!]; faceDown.DefinitionId = "training-lion-samurai"; faceDown.FaceUp = false;
        var enemyHome = Add(s, "training-lion-veteran", 1, Zone.Home);
        var own = Add(s, "training-crane-samurai", 0, Zone.Home);
        var card = Add(s, CourtingTrouble, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "courting-trouble-overconfident", card.Id).TargetIds!;
        Check(targets.Contains(inProvince.Id) && targets.Contains(enemyHome.Id) && !targets.Contains(faceDown.Id) && !targets.Contains(own.Id), "a face-up enemy Personality in a province or in play; not face down, not own: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, "courting-trouble-overconfident", card.Id, inProvince.Id));
        Check(e.State.Cards[inProvince.Id].GrantedKeywords.Any(g => g.Keyword == "Overconfident" && g.Duration == GrantDuration.Permanent), "Overconfident, permanently");
        // He enters play later: the other player may draw (Overconfident).
        var t = e.State; t.Phase = Phase.Dynasty; t.ActivePlayerId = 1; t.PriorityPlayerId = 1; t.TurnNumber++;
        for (var i = 0; i < 4; i++) Add(t, "training-marketplace", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(t);
        var buy = e2.QueryActions(1).First(a => a.Type == "purchase" && a.CardId == inProvince.Id && a.Enabled);
        Accept(e2, buy.Command!);
        Check(e2.State.PendingChoice?.Type == "overconfidentDraw" || e2.State.PendingOverconfidentDraws.Contains(0), "an Overconfident card entered play: the other player may draw");

        // Out of My Sight!: Brash, permanently; the Defender may draw when he is assigned to attack.
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 0;
        var brash = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var card3 = Add(s3, OutOfMySight, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "out-of-my-sight-brash", card3.Id, brash.Id));
        Check(e3.State.Cards[brash.Id].GrantedKeywords.Any(g => g.Keyword == "Brash" && g.Duration == GrantDuration.Permanent), "Brash, permanently");
        RunUntil(e3, () => e3.State.PendingChoice?.Type == "brashDraw" || e3.State.Phase == Phase.Dynasty);
        Eq(e3.State.PendingChoice?.Type, "brashDraw", "the Defender is offered the Brash draw when he attacks");
        Reject(e3, Use(e3, 0, "out-of-my-sight-brash", card3.Id, own.Id));
    }

    private static void ManipulateEnergiesGiveAndRemove()
    {
        var s = Table(Phase.Action, 0);
        var henshin = Add(s, "training-henshin-personality", 0, Zone.Home);
        var cavalry = Add(s, "training-cavalry", 1, Zone.Home);
        var plain = Add(s, "training-lion-samurai", 1, Zone.Home);
        var card = Add(s, ManipulateEnergies, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        // Give what he lacks, not what he has; remove what he has, not what he lacks.
        Check(CanUse(e, 0, "manipulate-energies-give-cavalry", card.Id) && CanUse(e, 0, "manipulate-energies-remove-cavalry", card.Id), "both halves have a legal target");
        Reject(e, Use(e, 0, "manipulate-energies-give-cavalry", card.Id, cavalry.Id, henshin.Id));
        Reject(e, Use(e, 0, "manipulate-energies-remove-cavalry", card.Id, plain.Id, henshin.Id));
        Accept(e, Offer(e, 0, "manipulate-energies-remove-cavalry", card.Id, cavalry.Id, henshin.Id));
        Check(e.State.Cards[cavalry.Id].RemovedKeywords.Contains("Cavalry"), "Cavalry removed, permanently");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played: its one printed ability is spent with it");
        var t = e.State; t.TurnNumber += 2; t.PriorityPlayerId = 0;
        var e2 = MatchEngine.FromTestState(t);
        Check(e2.State.Cards[cavalry.Id].RemovedKeywords.Contains("Cavalry"), "still removed on a later turn");
        // A later Turn grant does not beat a permanent removal; a later permanent give does.
        var card2 = Add(t, ManipulateEnergies, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(t);
        Accept(e3, Offer(e3, 0, "manipulate-energies-give-cavalry", card2.Id, cavalry.Id, henshin.Id));
        Check(!e3.State.Cards[cavalry.Id].RemovedKeywords.Contains("Cavalry"), "given back permanently: the removal is lifted (his printed Cavalry counts again)");
        // Only a Henshin performs.
        var s4 = Table(Phase.Action, 0);
        var monk = Add(s4, "training-monk", 0, Zone.Home);
        Add(s4, "training-lion-samurai", 1, Zone.Home);
        var card4 = Add(s4, ManipulateEnergies, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "manipulate-energies-give-naval", card4.Id), "no Henshin, no performer");
    }

    private static void IngenuityTestimonyGossip()
    {
        // Ingenuity: two targets dishonored, one extra draw at this player's End Phase.
        var s = Table(Phase.Action, 0);
        var magistrate = Add(s, "training-magistrate-personality", 0, Zone.Home);
        var t1 = Add(s, "training-lion-samurai", 1, Zone.Home);
        var t2 = Add(s, "training-lion-veteran", 1, Zone.Home);
        var card = Add(s, Ingenuity, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "ingenuity-dishonor", card.Id);
        Accept(e, option.Command! with { TargetId = t1.Id, PerformerId = magistrate.Id, Selections = [Pick("targets", t1.Id, t2.Id)] });
        Check(e.State.Cards[t1.Id].Dishonorable && e.State.Cards[t2.Id].Dishonorable, "both dishonored");
        Eq(e.State.ExtraEndPhaseDraws[0], 1, "an extra End Phase draw is owed");
        var handBefore = e.State.Players[0].Hand.Count;
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase is Phase.Straighten or Phase.Events or Phase.Action);
        Eq(e.State.Players[0].Hand.Count, handBefore + 2, "the End Phase drew two: the usual one and the additional one");
        Eq(e.State.ExtraEndPhaseDraws[0], 0, "and owes no more");

        // Testimony: an honorable performer, a target with lower BASE Personal Honor; dishonor or rehonor, one use.
        var s2 = Table(Phase.Action, 0);
        var honorable = Add(s2, "training-crane-veteran", 0, Zone.Home); // PH 3
        var dishonorablePerformer = Add(s2, "training-crane-samurai", 0, Zone.Home); dishonorablePerformer.Dishonorable = true;
        var lower = Add(s2, "training-lion-samurai", 1, Zone.Home); // PH 2
        var equal = Add(s2, "training-lion-veteran", 1, Zone.Home); // PH 3
        var card2 = Add(s2, Testimony, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Reject(e2, Use(e2, 0, "testimony-dishonor", card2.Id, lower.Id, dishonorablePerformer.Id));
        Reject(e2, Use(e2, 0, "testimony-dishonor", card2.Id, equal.Id, honorable.Id));
        Accept(e2, Offer(e2, 0, "testimony-dishonor", card2.Id, lower.Id, honorable.Id));
        Check(e2.State.Cards[lower.Id].Dishonorable, "dishonored");
        Check(!CanUse(e2, 0, "testimony-rehonor", card2.Id), "the other half shares the one use");
        var s3 = Table(Phase.Action, 0);
        var performer3 = Add(s3, "training-crane-veteran", 0, Zone.Home);
        var shamed = Add(s3, "training-lion-samurai", 1, Zone.Home); shamed.Dishonorable = true;
        var card3 = Add(s3, Testimony, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "testimony-rehonor", card3.Id, shamed.Id, performer3.Id));
        Check(!e3.State.Cards[shamed.Id].Dishonorable, "rehonored");

        // Sharing Gossip: this copy plus every copy already in a discard pile.
        var s4 = Table(Phase.Action, 0);
        var courtier = Add(s4, "training-courtier-personality", 0, Zone.Home);
        Add(s4, SharingGossip, 0, Zone.FateDiscard); Add(s4, SharingGossip, 1, Zone.FateDiscard);
        var card4 = Add(s4, SharingGossip, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        var h = Honor(e4, 0);
        Accept(e4, Enabled(e4, 0, "sharing-gossip-honor", card4.Id).Command! with { PerformerId = courtier.Id });
        Eq(Honor(e4, 0), h + 3, "three copies in the discard piles once this one is there");
        Eq(e4.State.Cards[card4.Id].Zone, Zone.FateDiscard, "the played copy is in the discard pile");
    }

    private static void UrgencyAllowance()
    {
        var s = Table(Phase.Action, 0);
        var bower = Add(s, "training-skin-donor", 0, Zone.Home); // training-bow-any (Open), training-create-token (Open)
        Add(s, "training-lion-samurai", 1, Zone.Home);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var urgency = Add(s, Urgency, 0, Zone.Hand);
        var later1 = Add(s, "training-plain-strategy", 0, Zone.Hand);
        var later2 = Add(s, "training-plain-strategy", 0, Zone.Hand);
        var later3 = Add(s, "training-plain-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "urgency-limit", urgency.Id).Command!);
        Check(e.State.ActionAllowances.Count == 2 && e.State.ActionAllowances.All(a => a.Remaining == 2), "each player may take two more");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, Enabled(e, 0, "training-plain-open", later1.Id).Command!);
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, Enabled(e, 0, "training-plain-open", later2.Id).Command!);
        Accept(e, new GameCommand("pass", 1, e.Version));
        Check(!CanUse(e, 0, "training-plain-open", later3.Id), "a third is listed disabled");
        Reject(e, Use(e, 0, "training-plain-open", later3.Id));
        Check(e.QueryActions(0).Any(o => o.Type == "pass" && o.Enabled), "passing stays open");
        // The next phase is free again (a new phase instance).
        var t = e.State; t.Phase = Phase.Battle; t.CurrentBattleProvince = 0; t.UnresolvedBattlefields = [0]; t.ActivePlayerId = 1; t.PriorityPlayerId = 0;
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Check(e2.State.ActionAllowances.All(a => a.PhaseInstance != PrintedPhases.Instance(e2.State.TurnNumber, Phase.Battle)), "the allowance belonged to the Action Phase");
        // The Limited half: pay 2 Gold, draw.
        var s3 = Table(Phase.Action, 0); Gold(s3, 0, 1);
        var urgency3 = Add(s3, Urgency, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        var hand = e3.State.Players[0].Hand.Count;
        Accept(e3, Enabled(e3, 0, "urgency-draw", urgency3.Id).Command!);
        Eq(e3.State.Players[0].Hand.Count, hand, "drew one, played one");
        Check(e3.State.Cards.Values.Any(c => c.OwnerId == 0 && c.Bowed && (CardCatalog.Get(c.DefinitionId).GoldProduction > 0 || CardCatalog.Get(c.DefinitionId).Type == CardType.Stronghold)), "the 2 Gold were paid");
    }

    private static MatchEngine BuyInClan(string card, string clan, int buyer, Action<MatchState>? tweak = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.ActivePlayerId = buyer; s.PriorityPlayerId = buyer; s.Phase = Phase.Dynasty;
        s.Players[buyer].Clan = clan; s.Players[buyer].Honor = 30;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", buyer, Zone.Home);
        var entering = s.Cards[s.Players[buyer].Provinces[0].CardId!]; entering.DefinitionId = card; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(buyer).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false } && a.Quote is { ProclaimEligible: true });
        Accept(e, option.Command!);
        Eq(e.State.PendingChoice?.Type, "proclaim", $"{card}: the Proclaim window is open");
        return e;
    }

    private static void ProclaimNegationLimitAndLock()
    {
        // Turning the Story in the opponent's hand: the Proclaim waits, is negated, the window resumes.
        var e = BuyInClan("training-crane-proclaim-loss", "Crane", 0, s => Add(s, TurningTheStory, 1, Zone.Hand));
        var honor = Honor(e, 0);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        Eq(e.State.PendingChoice?.Type, "proclaimNegateReaction", "the opponent is asked before the Proclaim resolves");
        Eq(e.State.PendingChoice?.PlayerId, 1);
        var story = e.QueryActions(1).Single(o => o.Type == "useAbility" && o.Command?.AbilityId == "turning-the-story-negate");
        Accept(e, story.Command!);
        Check(e.State.PendingChoice is null, "the window closed: the negated Proclaim left nothing to react to (issue #639)");
        Eq(Honor(e, 0), honor - 2, "no Proclaim Honor (the entering loss still applies)");
        Eq(e.State.Players[0].ProclaimUsedTurn, e.State.TurnNumber, "the Proclaim was taken (and spent) all the same");
        Check(e.State.HeldProclaim is null && e.State.Cards.Values.Single(c => c.DefinitionId == TurningTheStory).Zone == Zone.FateDiscard, "the Reaction was played and discarded");
        // Passing instead: the Proclaim resolves.
        var e2 = BuyInClan("training-crane-proclaim-loss", "Crane", 0, s => Add(s, TurningTheStory, 1, Zone.Hand));
        var honor2 = Honor(e2, 0);
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        Decline(e2);
        Eq(Honor(e2, 0), honor2 + 3 - 2, "Proclaim +3, then the entering loss");
        // Nothing in hand: no window at all.
        var e3 = BuyInClan("training-crane-proclaim-loss", "Crane", 0);
        Accept(e3, e3.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        Check(e3.State.PendingChoice is null, "no Reaction held: the Proclaim resolved at once, and the window with it (issue #639)");

        // Kakita Yusugi: Proclaim already used this turn, he may still be Proclaimed.
        var e4 = BuyInClan(Yusugi, "Crane", 0, s => s.Players[0].ProclaimUsedTurn = s.TurnNumber);
        var honor4 = Honor(e4, 0);
        Accept(e4, e4.QueryActions(0).Single(a => a.Type == "proclaim").Command!);
        Eq(Honor(e4, 0), honor4 + 2, "Yusugi's Personal Honor gained");
        var s5 = MatchEngine.CreateEconomyScenario().State; s5.Players[0].Clan = "Crane"; s5.Players[0].Honor = 30; s5.Players[0].ProclaimUsedTurn = s5.TurnNumber;
        for (var i = 0; i < 8; i++) Add(s5, "training-marketplace", 0, Zone.Home);
        var other5 = s5.Cards[s5.Players[0].Provinces[0].CardId!]; other5.DefinitionId = "training-crane-proclaim-loss"; other5.FaceUp = true;
        var e5 = MatchEngine.FromTestState(s5);
        Check(e5.QueryActions(0).Where(a => a.Type == "purchase" && a.CardId == other5.Id).All(a => a.Quote is { ProclaimEligible: false }), "any other Personality: not after a Proclaim this turn");

        // Yogo Ugimori: his Reaction locks the chosen player until his controller's next turn begins.
        var e6 = BuyInClan(Ugimori, "Scorpion", 0);
        var ugimori = e6.State.Cards.Values.Single(c => c.DefinitionId == Ugimori);
        var lockOption = e6.QueryActions(0).Single(o => o.Type == "useAbility" && o.Command?.AbilityId == "ugimori-proclaim-lock" && o.Label.Contains("opponent"));
        Accept(e6, lockOption.Command!);
        Check(e6.State.ProclaimLocks.Any(l => l.PlayerId == 1 && l.OwnerId == 0), "player 1 is locked");
        Decline(e6); // the opponent holds nothing to react with, so this one pass closes the window (issue #644)
        // Player 1, on his turn, buys in Clan: no Proclaim.
        var t = e6.State; t.ActivePlayerId = 1; t.PriorityPlayerId = 1; t.Phase = Phase.Dynasty; t.TurnNumber++; t.Players[1].Clan = "Lion"; t.Players[1].Honor = 30;
        for (var i = 0; i < 8; i++) Add(t, "training-marketplace", 1, Zone.Home);
        var lion = t.Cards[t.Players[1].Provinces[0].CardId!]; lion.DefinitionId = "training-lion-samurai"; lion.FaceUp = true;
        var e7 = MatchEngine.FromTestState(t);
        Check(e7.QueryActions(1).Where(a => a.Type == "purchase" && a.CardId == lion.Id).All(a => a.Quote is { ProclaimEligible: false }), "locked: no Proclaim-eligible purchase");
        // Once Ugimori's controller's next turn has begun, the lock is over.
        t.Players[0].TurnsTaken++;
        var e8 = MatchEngine.FromTestState(t);
        Check(e8.QueryActions(1).Any(a => a.Type == "purchase" && a.CardId == lion.Id && a.Quote is { ProclaimEligible: true }), "free again");
        // Not from a province: Ugimori entering play another way has no Reaction to offer.
        var s9 = Table(Phase.Action, 0);
        var ugimori9 = Add(s9, Ugimori, 0, Zone.Home);
        var e9 = MatchEngine.FromTestState(s9);
        Reject(e9, Use(e9, 0, "ugimori-proclaim-lock", ugimori9.Id));
    }

    private static void SentinelSpiritWatch()
    {
        // Player 1's turn; player 0's Spirit stands watch, is assigned to defend and opposed: 2 Honor when the Dynasty Phase begins.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var spirit = Add(s, "training-spirit-personality", 0, Zone.Home);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Home);
        var card = Add(s, SentinelSpirit, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "sentinel-spirit-watch", card.Id).Command! with { PerformerId = spirit.Id });
        Eq(e.State.SentinelWatches.Count, 1, "the watch stands");
        RunUntil(e, () => e.State.Phase == Phase.Dynasty);
        Eq(Honor(e, 0), honor + 2, "opposed this turn: 2 Honor; units attacked, so not the 3");
        Eq(e.State.SentinelWatches.Count, 0, "spent");
        // Not on your own turn; not while the active player controls no Personality.
        var s2 = Table(Phase.Action, 0);
        var spirit2 = Add(s2, "training-spirit-personality", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Home);
        var card2 = Add(s2, SentinelSpirit, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "sentinel-spirit-watch", card2.Id), "not on your own turn");
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 0;
        var spirit3 = Add(s3, "training-spirit-personality", 0, Zone.Home);
        var card3 = Add(s3, SentinelSpirit, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "sentinel-spirit-watch", card3.Id), "the active player controls no Personality");
        // No attack this turn and the lowest Honor: 3 Honor (and no 2: never opposed).
        var s4 = Table(Phase.Action, 1); s4.PriorityPlayerId = 0; s4.Players[0].Honor = 1; s4.Players[1].Honor = 10;
        var spirit4 = Add(s4, "training-spirit-personality", 0, Zone.Home);
        Add(s4, "training-lion-samurai", 1, Zone.Home);
        var card4 = Add(s4, SentinelSpirit, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 0, "sentinel-spirit-watch", card4.Id).Command! with { PerformerId = spirit4.Id });
        Accept(e4, new GameCommand("pass", 1, e4.Version)); Accept(e4, new GameCommand("pass", 0, e4.Version));
        Eq(e4.State.Phase, Phase.AttackDeclaration);
        Accept(e4, e4.QueryActions(1).Single(o => o.Type == "pass").Command!); // declines to attack
        Eq(e4.State.Phase, Phase.Dynasty);
        Eq(Honor(e4, 0), 4, "3 Honor: no unit attacked and its player's Honor is the lowest");
    }

    private static void InsultAnsweredDuel()
    {
        // Player 1 (active) has Baiko and the Favor; his Limited action costs player 0 1 Honor. Player 0 holds An Insult Answered and an unbowed Duelist.
        var s = Table(Phase.Action, 1);
        s.FavorPlayerId = 1;
        var baiko = Add(s, Baiko, 1, Zone.Home); // Chi 3
        var shamed = Add(s, "training-crane-samurai", 0, Zone.Home); shamed.Dishonorable = true;
        var duelist = Add(s, "training-duelist-personality", 0, Zone.Home); // Chi 3, Duelist: wins ties
        var card = Add(s, InsultAnswered, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 1, "baiko-shame", baiko.Id, shamed.Id));
        Eq(Honor(e, 0), honor - 1, "Baiko cost player 0 1 Honor");
        Check(e.State.LastResolvedAction is { } facts && facts.HonorLosers.Contains(0) && facts.PerformerId == baiko.Id, "the resolved action records its performer and the Honor it cost");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opened for the Reaction");
        var offer = Enabled(e, 0, "insult-answered-duel", card.Id);
        Check(offer.TargetIds!.SequenceEqual([baiko.Id]), "the action's performer is the only target");
        Accept(e, offer.Command! with { TargetId = baiko.Id, PerformerId = duelist.Id });
        Check(e.State.PendingDuel is { } duel && duel.ChallengerId == duelist.Id && duel.ChallengedId == baiko.Id, "the Duelist duels Baiko");
        PlayOutDuel(e);
        Check(e.State.PendingDuel is null, "the duel finished");
        Eq(e.State.Cards[baiko.Id].Zone, Zone.Dead, "the loser is destroyed (the Duelist won the tie)");
        Eq(e.State.Cards[duelist.Id].Tokens.Count(t => t.Type == "Revenge" && t.Stat == "Force" && t.Amount == 1), 2, "two +1F Revenge tokens on the winner");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played");

        // A harmless action opens nothing for it.
        var s2 = Table(Phase.Action, 1);
        var bower = Add(s2, "training-skin-donor", 1, Zone.Home);
        var victim = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-duelist-personality", 0, Zone.Home);
        var card2 = Add(s2, InsultAnswered, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-bow-any", bower.Id, victim.Id));
        Check(e2.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(e2, 0, "insult-answered-duel", card2.Id), "no Honor loss: no Reaction");
    }

    private static void TattooResourcesSchism()
    {
        // Dragon Tattoo - exp.
        var s = Table(Phase.Action, 0);
        var tattooed = Add(s, "training-tattooed", 0, Zone.Home);
        var tattoo = Add(s, DragonTattooExp, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "dragon-tattoo-exp-imbue", tattoo.Id).Command! with { PerformerId = tattooed.Id });
        Check(e.State.Cards[tattooed.Id].GrantedAbilities.Any(g => g.AbilityId == "dragon-tattoo-fire-token" && g.Duration == GrantDuration.Permanent), "the ability is his, permanently");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, Enabled(e, 0, "dragon-tattoo-fire-token", tattooed.Id).Command!);
        Eq(e.State.Cards[tattooed.Id].Tokens.Count(t => t.Type == "Fire"), 1, "a +1F Fire token from the granted ability");
        var t = e.State; t.TurnNumber += 2; t.PriorityPlayerId = 0; Add(t, DragonTattooExp, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        var second = e2.State.Cards.Values.First(c => c.DefinitionId == DragonTattooExp && c.Zone == Zone.Hand);
        Check(!CanUse(e2, 0, "dragon-tattoo-exp-imbue", second.Id), "he has performed an action from a Dragon Tattoo this game: never again");
        Reject(e2, Use(e2, 0, "dragon-tattoo-exp-imbue", second.Id, null, tattooed.Id));

        // Dubious Resources: 2 Gold, a 2F Ronin Follower attached to the performer.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0; Gold(s3, 0, 1);
        var performer = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var resources = Add(s3, DubiousResources, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "dubious-resources-create", resources.Id).Command! with { PerformerId = performer.Id });
        var ronin = e3.State.Cards.Values.SingleOrDefault(c => c.DefinitionId == RoninFollower);
        Check(ronin is { Zone: Zone.Attached } && ronin.AttachedTo == performer.Id && ronin.OwnerId == 0, "the Ronin Follower is attached to him");
        Eq(e3.CardForceForTest(ronin!.Id), 2, "2F");

        // Brotherhood Schism: the Open bars a second Ring of one owner's this turn; the Limited shuffles an enemy discard-pile Event into its deck.
        var s4 = Table(Phase.Action, 1); s4.PriorityPlayerId = 0;
        var schism = Add(s4, BrotherhoodSchism, 0, Zone.Hand);
        var ring = Add(s4, "oracle-l5r-6338", 1, Zone.Hand); // Ring of the Void: a standing Limited recruit
        s4.Players[1].RingPutIntoPlayTurn = s4.TurnNumber;
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 0, "brotherhood-schism-rings", schism.Id).Command!);
        Eq(e4.State.RingSchismTurn, e4.State.TurnNumber);
        var t4 = e4.State; t4.PriorityPlayerId = 1;
        t4.Phase = Phase.Battle; t4.AdditionalActions.Add(new AdditionalActionGrant(1, [ring.Id], Battle: t4.BattleNumber)); // the recruit is a granted Battle action (issue #1157)
        var e5 = MatchEngine.FromTestState(t4);
        Reject(e5, Use(e5, 1, "ring-of-void-recruit", ring.Id));
        t4.Players[1].RingPutIntoPlayTurn = -1;
        var e6 = MatchEngine.FromTestState(t4);
        Accept(e6, Use(e6, 1, "ring-of-void-recruit", ring.Id));
        Eq(e6.State.Cards[ring.Id].Zone, Zone.Home, "a first Ring of the turn enters play");
        var s7 = Table(Phase.Action, 0);
        var eventCard = Add(s7, "oracle-l5r-8494", 1, Zone.DynastyDiscard);
        var ownEvent = Add(s7, "oracle-l5r-8494", 0, Zone.DynastyDiscard);
        var schism7 = Add(s7, BrotherhoodSchism, 0, Zone.Hand);
        var e7 = MatchEngine.FromTestState(s7);
        var deckBefore = e7.State.Players[1].DynastyDeck.Count;
        Reject(e7, Use(e7, 0, "brotherhood-schism-shuffle", schism7.Id, ownEvent.Id));
        Accept(e7, Offer(e7, 0, "brotherhood-schism-shuffle", schism7.Id, eventCard.Id));
        Check(e7.State.Cards[eventCard.Id].Zone == Zone.DynastyDeck && e7.State.Players[1].DynastyDeck.Count == deckBefore + 1 && e7.State.Players[1].DynastyDeck.Contains(eventCard.Id), "shuffled into its owner's Dynasty deck");
    }

    private static void BaikoAndTsujiken()
    {
        var s = Table(Phase.Action, 0); Gold(s, 0, 1);
        var baiko = Add(s, Baiko, 0, Zone.Home);
        var shamed = Add(s, "training-lion-samurai", 1, Zone.Home); shamed.Dishonorable = true;
        var honest = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "baiko-shame", baiko.Id), "without the Favor the Limited action is not usable");
        Accept(e, Enabled(e, 0, "baiko-favor", baiko.Id).Command!);
        Eq(e.State.FavorPlayerId, 0, "2 Gold took the Favor");
        Accept(e, new GameCommand("pass", 1, e.Version));
        Reject(e, Use(e, 0, "baiko-shame", baiko.Id, honest.Id));
        var honor1 = Honor(e, 1);
        Accept(e, Offer(e, 0, "baiko-shame", baiko.Id, shamed.Id));
        Eq(Honor(e, 1), honor1 - 1, "his controller loses 1 Honor");
        Check(e.State.FavorPlayerId is null, "the Favor was discarded");

        var s2 = Table(Phase.Action, 0); Gold(s2, 0, 3);
        var tsujiken = Add(s2, TsujikenExp, 0, Zone.Home);
        var holding = Add(s2, "training-farm", 1, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        var ownStronghold = e2.State.Players[0].StrongholdId; var enemyStronghold = e2.State.Players[1].StrongholdId;
        var targets = Enabled(e2, 0, "tsujiken-exp-bow", tsujiken.Id).TargetIds!;
        Check(targets.Contains(ownStronghold) && !targets.Contains(enemyStronghold) && targets.Contains(holding.Id), "a Holding of either player's, or his own Stronghold, never the enemy's: " + string.Join(",", targets));
        Accept(e2, Offer(e2, 0, "tsujiken-exp-straighten", tsujiken.Id, holding.Id));
        Check(!e2.State.Cards[holding.Id].Bowed, "straightened");
        Check(!CanUse(e2, 0, "tsujiken-exp-bow", tsujiken.Id), "bow-or-straighten is one use");
        Accept(e2, new GameCommand("pass", 1, e2.Version));
        var hand = e2.State.Players[0].Hand.Count;
        Accept(e2, Enabled(e2, 0, "tsujiken-exp-draw", tsujiken.Id).Command!);
        Eq(e2.State.Players[0].Hand.Count, hand + 1, "5 Gold drew a card");
        Accept(e2, new GameCommand("pass", 1, e2.Version));
        var t2 = e2.State; var victim = Add(t2, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(t2);
        var h = Honor(e3, 1);
        Accept(e3, Offer(e3, 0, "tsujiken-exp-dishonor", tsujiken.Id, victim.Id));
        Check(e3.State.Cards[victim.Id].Dishonorable && Honor(e3, 1) == h - 1 && e3.State.Cards[tsujiken.Id].Bowed, "dishonored, 1 Honor lost, Tsujiken bowed as the cost");
    }

    private static void YukaAndHotaka()
    {
        // Yuka performs Incriminating Testimony (Political): her Chi is 3 + 3 while it resolves, so the province gets 6; afterwards she is back to 3.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var yuka = Add(s, Yuka, 0, Zone.Home);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, IncriminatingTestimony, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = Strength(e, 0, 0);
        Accept(e, Offer(e, 0, "incriminating-testimony-bow", card.Id, attacker.Id, yuka.Id));
        Eq(Strength(e, 0, 0), before + 6, "the bonus read her Chi at 6");
        Eq(e.PersonalityChiForTest(yuka.Id), 3, "her Chi is back to 3 once the action ended");
        // A non-Political action of hers gets nothing (a Battle bow with no keyword: training-performed-bow is Open, use a plain Personality ability instead).
        var s2 = Table(Phase.Action, 0);
        var yuka2 = Add(s2, Yuka, 0, Zone.Home);
        var plain = Add(s2, "training-plain-strategy", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "training-plain-open", plain.Id).Command!);
        Check(e2.State.Cards[yuka2.Id].Modifiers.Count == 0, "no Political action of hers: no modifier left behind");

        // Hotaka: +1PH against a Dragon player.
        var s3 = Table(Phase.Action, 0); s3.Players[1].Clan = "Dragon";
        var hotaka = Add(s3, Hotaka, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Eq(e3.PersonalHonorForTest(hotaka.Id), 4, "+1PH: the other player is Dragon Clan");
        var s3b = Table(Phase.Action, 0);
        var hotaka3b = Add(s3b, Hotaka, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s3b).PersonalHonorForTest(hotaka3b.Id), 3, "printed PH otherwise");
        // He may not perform Dark Virtue actions; another Personality may.
        var s4 = Table(Phase.Action, 0);
        var hotaka4 = Add(s4, Hotaka, 0, Zone.Home);
        var other = Add(s4, "training-crane-samurai", 0, Zone.Home);
        var target4 = Add(s4, "training-lion-samurai", 1, Zone.Home);
        var darkVirtue = Add(s4, "training-dark-virtue-performed", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Reject(e4, Use(e4, 0, "training-performed-bow", darkVirtue.Id, target4.Id, hotaka4.Id));
        Accept(e4, Use(e4, 0, "training-performed-bow", darkVirtue.Id, target4.Id, other.Id));
        Check(e4.State.Cards[target4.Id].Bowed, "another performer is fine");
        // The Honor Victory threshold: 40 less (3 - 1) = 38 with Hotaka in play; 40 without him.
        var s5 = Table(Phase.Dynasty, 1); s5.Players[0].Honor = 38;
        Add(s5, Hotaka, 0, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, new GameCommand("endDynasty", 1, e5.Version));
        Check(e5.State.WinnerId == 0 && e5.State.VictoryReason!.Contains("Honor victory"), "38 Honor wins with Hotaka in play");
        var s6 = Table(Phase.Dynasty, 1); s6.Players[0].Honor = 38;
        var e6 = MatchEngine.FromTestState(s6);
        Accept(e6, new GameCommand("endDynasty", 1, e6.Version));
        Check(e6.State.WinnerId is null, "38 Honor does not win without him");
    }
}
