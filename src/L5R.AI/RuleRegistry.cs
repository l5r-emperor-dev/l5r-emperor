using System.Globalization;

namespace L5R.AI;

// Issue #1206 (docs/bots/CONAN_STRATEGY_BRIEF.md, step 1 item 1): every rule and knob name a bot reads is declared here, once. A rule set that names anything else fails
// when the BotContext is built, so a typo or a stale binary can no longer make an arm equal to its baseline in silence. The behaviour of each rule lives where it is
// read (TrainingOpponent.*.cs); the line here says what it is for and where it acts, so the fire counts of a tournament (L5R_TOURN_FIRES=1) can be read by family.
//   switch   a name present in BotContext.Rules, read through Rule(x, name) / BotContext.Has;
//   flag     a switch the Contender factory turns into a BotContext property when it builds the bot (BotContext.Contender);
//   knob     a number given as "name=value" (BotContext.Knob), with its default at the place it is read.
public static class RuleRegistry
{
    public sealed record Entry(string Name, bool Switch, bool Knob, bool Flag, string Where, string About);

    private static Entry R(string name, string where, string about) => new(name, true, false, false, where, about);   // a switch
    private static Entry F(string name, string where, string about) => new(name, true, false, true, where, about);    // a flag: becomes a property at construction
    private static Entry K(string name, string where, string about) => new(name, false, true, false, where, about);   // a knob
    private static Entry RK(string name, string where, string about) => new(name, true, true, false, where, about);   // both: the switch turns it on, the knob weighs it

    /// <summary>Every name, in the order of the families of docs/bots/strategy/RULE_FAMILIES.md: Dynasty, attack, defence, inside the battle, the Favor and Honor, the search, the Read.</summary>
    public static readonly IReadOnlyList<Entry> Entries =
    [
        // ---- Dynasty: buying, keeping and discarding (family F) ----
        R("econ", "purchase", "fair-value Holdings (cost at most fair above production) are bought first in the early turns, and a province card payable next turn is not discarded"),
        R("econ2", "purchase", "econ keeps buying fair Holdings up to ten"),
        R("econctx", "purchase", "econ pauses while the bot's Provinces are contested"),
        R("econT2", "purchase", "econ's early turns end after turn 2 instead of 4"),
        K("econN", "purchase", "econ stops at this many Holdings (8)"),
        K("econT", "purchase", "econ's last early turn (4; 2 under econT2)"),
        K("fair", "purchase", "how far above its production a Holding's cost may be and still count as fair (1)"),
        R("body1", "purchase", "econ yields to an affordable Personality when the bot has none after its second turn"),
        R("bodyfirst", "purchase", "with bfH Holdings standing, a Holding is not bought before a Personality"),
        K("bfH", "purchase", "bodyfirst's Holding count (3)"),
        R("firstbody", "purchase", "from the second turn the first Personality purchase is favoured over economy"),
        R("threatbody", "purchase", "under threat a Personality is bought before economy"),
        R("tb2", "purchase", "threatbody also buys a second body in the turn one was recruited"),
        R("border", "purchase", "the once-per-game Border Keep refresh is kept for a turn when nothing in the row can be paid for"),
        R("discardbad", "discard", "a Holding costing two or more above its production is discarded once six Holdings stand"),
        R("nokeep", "discard", "the discard rule's keeps (fair Holdings, affordable Personalities) are both off"),
        R("nokeepH", "discard", "the discard rule does not keep fair Holdings"),
        R("nokeepP", "discard", "the discard rule does not keep affordable Personalities"),
        R("keepready", "discard", "an affordable Personality is measured against the Gold ready now, not the capacity"),
        R("keepbody", "discard", "an affordable Personality is never cycled away at the Dynasty discard"),
        R("holdcap", "purchase", "with twenty or more Gold capacity no further Holding is bought"),
        R("slate", "action", "an Open action costing Gold the plan's slate needs is barred (lost games; off)"),
        R("equipok", "action", "equipping a Follower or Item with Force is exempt from slate's bar"),
        RK("rkw", "purchase", "the slate values a Personality carrying a keyword the deck needs and the bot lacks; the knob is the value (25)"),
        RK("roff", "purchase", "the slate values Force that closes the gap to the opponent's Provinces; the knob is the value per Force (6)"),
        RK("rdef", "purchase", "the slate values Force that closes the gap to the opponent's army; the knob is the value per Force (6)"),
        K("crowd", "purchase", "Holdings from which a further Holding is worth half (6)"),
        K("hdev", "purchase", "a Holding's worth under a Develop intent (1.2)"),
        K("hatk", "purchase", "a Holding's worth under an Attack intent (0.7)"),
        K("hdef", "purchase", "a Holding's worth under a Defend intent (0.6)"),
        K("pbase", "purchase", "a Personality's base worth in the slate (60)"),
        K("pf", "purchase", "a Personality's worth per Force in the slate (16)"),
        R("pressahead", "plan", "well ahead on Provinces and bodies the plan's intent is Attack, not Develop"),
        // ---- the attack: whether, where, with whom (family A) ----
        R("lean", "attack", "the smallest razing armies attack and everything else stays home; no lean plan: nobody attacks"),
        R("lean2", "attack", "lean, falling back to the ordinary attack when no lean plan exists; the Cavalry adds nobody when the armies already suffice"),
        R("lean3", "attack", "lean2, but the Cavalry adds nobody only when the armies already raze"),
        R("maxraze", "attack", "outside an Honor race the infantry assignment that razes the most Provinces is taken"),
        R("lethal", "attack", "with lethalN or fewer enemy Provinces left the max-raze plan is pursued to the end"),
        K("lethalN", "attack", "lethal's Province count (2)"),
        R("favoraware", "attack", "a visible Imperial Favor that would send the lone army's biggest unit home stops that attack"),
        R("deter", "attack", "spare attackers stay home when they are enough to stop the opponent's whole army attacking next turn"),
        R("guard", "attack", "units needed at home to stop the counter-attack razing a last Province are taken out of the attack"),
        K("guardN", "attack", "the most units guard keeps home (2)"),
        R("aggro", "attack", "an attack scoring under 85 is boosted and the aggressive assignment (every unit that beats a Province) is used"),
        R("aggro2", "attack", "aggro, with the plan's assignment kept when it exists"),
        R("raid", "attack", "an undefended board is attacked more eagerly and the raid boost is added"),
        R("raidboost", "attack", "only raid's boost, without the eagerness"),
        R("hostileatk", "attack", "an attack the opponent's visible Battle actions would stop is barred"),
        R("provaim", "attack", "a Strength cut used outside a battle names the opponent's Province"),
        R("provtarget", "attack", "a Strength-lowering ability is held unless the bot attacks a Province"),
        R("clock", "attack", "the Honor race begins at clockH of the opponent's Honor instead of 40"),
        K("clockH", "attack", "clock's threshold (34)"),
        K("swing", "attack", "the estimator's swing per enemy trick (TrickSwing)"),
        // ---- the defence: whom to send, whom to keep (family D) ----
        R("presence", "defence", "when an attack would raze and the bot's removals would stop it, the cheapest unit that is needed is sent (the Rule of Presence)"),
        R("presence2", "defence", "presence counts abilities with a wide attacker target"),
        R("p2noduel", "defence", "presence2 does not count duels"),
        R("presdepth", "defence", "the presence plan counts the removals the attacker's hand can answer (pdh)"),
        K("pdh", "defence", "presdepth's share of the attacker's hand assumed to answer, in percent (50)"),
        R("pdstrong", "defence", "the presence plan offers the strongest unit first"),
        R("defwin", "defence", "the defence assigns only the units that win by dwM"),
        K("dwM", "defence", "defwin's margin (0)"),
        R("nogift", "defence", "no defenders are given to a battle while the opponent is at nogiftH Honor and the bot holds more than two Provinces"),
        K("nogiftH", "defence", "nogift's Honor threshold (20)"),
        R("hgift", "defence", "no defenders are given within hgN Honor of the opponent's win"),
        K("hgN", "defence", "hgift's distance from 40 (10)"),
        R("hostilehand", "defence", "a defence the opponent's visible Battle actions would break is barred"),
        R("hh1", "defence", "hostileatk and hostilehand assume one hostile card per performer instead of two"),
        R("cavblind", "defence", "before the Cavalry declares, the strongest defenders the attacker's hand can remove are planned as absent and stay home"),
        R("lastprov", "defence", "the last Province is never given up by a self-harming ability"),
        R("nopresence", "estimate", "the estimator gives the opponent no swing at a battle where nobody defends"),
        R("phantom", "estimate", "an opposed or bowed performer's conditional bonus does not count against an undefended Province"),
        // ---- inside the battle: which action, when (family E) ----
        R("conf", "battle", "Battle actions are scored by the confidence they add to the fight"),
        R("cutoff", "battle", "when the opponent can cut off the bot's removals, the deciding unit is removed now with whatever can do it"),
        K("cuth", "battle", "cutoff's share of the enemy hand assumed to cut, in percent (50)"),
        K("cutk", "battle", "cutoff's removals that must remain (1)"),
        R("keepup", "battle", "an ability paid for by bowing is used by a performer nobody can straighten, not by one who can be"),
        R("nofoeboost", "battle", "a helping ability is never aimed at the enemy's side"),
        R("selfboost", "battle", "a Force boost paid for by bowing its own source is not used"),
        R("selfharm", "battle", "a harmful ability never targets the bot's own Personality or Follower"),
        R("lobbyok", "battle", "selfharm exempts bowing a Personality to take the Favor"),
        R("selflock", "battle", "a 'may not' effect never locks the bot's own card"),
        R("nostraighten", "battle", "a straighten is not used on a unit that is not bowed"),
        R("openbow", "action", "in the Action phase an enemy Personality is bowed only when the bot attacks this turn (or on the opponent's turn, always)"),
        R("noop0", "battle", "a Force penalty is not played on a target already at 0 Force"),
        R("overkill", "battle", "a destroy or bow is not played when the battle is already won by okm"),
        K("okm", "battle", "overkill's margin (6)"),
        R("tiecheck", "battle", "a cost that bows or removes the bot's own unit at a battle is not paid when its Force there would fall to the enemy's or below"),
        R("handkeep", "action", "in the Open phase a hand card below Bow rank is kept while the hand is short by hkN"),
        K("hkN", "action", "handkeep's shortfall (2)"),
        R("holdbo", "action", "a Battle-or-Open card is held for the battle in the Open phase"),
        R("purpose", "action", "an Open-phase straighten or bow needs an attack to follow"),
        R("spread", "equip", "an attachment goes to the next unit once one already beats the strongest Province and every defender"),
        R("spreadwide", "equip", "once one unit beats the strongest Province alone, further attachments go to the weakest unbowed Personality"),
        R("spreadvwide", "equip", "a Personality with no attachment gets one before any gets a second (a Kensai may carry two Weapons)"),
        R("svwgate", "equip", "spreadvwide only once a unit beats the weakest Province"),
        R("ownprov", "honor", "an ability that costs a Province is used only below 36 Honor"),
        R("ownprov2", "honor", "ownprov's threshold is ownprovH, or no Province held"),
        K("ownprovH", "honor", "ownprov2's threshold (36)"),
        // ---- the Favor, Honor and duels (family I) ----
        R("shinden", "honor", "the Shinden choice takes Honor while the opponent is below 34"),
        R("honorclock", "honor", "a self-gain or opponent-lose choice goes to whichever is nearer 40"),
        R("dhdrive", "honor", "with the opponent at dhH Honor or below, dishonouring abilities are pressed"),
        K("dhH", "honor", "dhdrive's threshold (10)"),
        R("nobowgift", "honor", "a decider choice does not gift Honor that takes the opponent to nbgH"),
        K("nbgH", "honor", "nobowgift's threshold (34)"),
        R("favorshame", "favor", "the Favor is not discarded without the bot's own gain"),
        R("favorhold", "favor", "the Favor is kept when the battle is already held by fhM"),
        K("fhM", "favor", "favorhold's margin (2)"),
        R("lobby", "favor", "the Favor is taken by bowing only with lobbyN or fewer unbowed Personalities at home"),
        K("lobbyN", "favor", "lobby's count (2)"),
        R("lobbyatk", "favor", "on its own turn before an attack the Favor is not taken by bowing a Personality with Force"),
        R("lobbycycle", "favor", "after lobbying, the Favor cycles cards while the enemy has Force in play"),
        R("cycle", "favor", "the Favor's cycle is always used"),
        R("duelodds", "duel", "a duel is issued only with a stat edge of doM"),
        K("doM", "duel", "duelodds' margin (1)"),
        R("duelplay", "duel", "focus and strike by the expected stats (dsE per enemy focus, dsM margin)"),
        K("dsE", "duel", "duelplay's stat per enemy focus (3)"),
        K("dsM", "duel", "duelplay's margin (3)"),
        R("selfplayer", "honor", "a player-targeted Honor ability aims a gain at the bot and a loss at the opponent"),
        // ---- flags: switches the Contender factory turns into properties (BotContext.Contender) ----
        F("surereact", "battle", "UseSureReactions: a reaction is played only when its condition is sure"),
        F("trades", "attack", "UseTradeAttacks: trade attacks"),
        F("battlepolicy", "battle", "UseBattlePolicy: the issue #788 battle policy"),
        F("noplanner", "plan", "UsePlanner off: the champion's turn without a plan"),
        F("notriage", "battle", "UsePlannerTriage off"),
        F("notargets", "battle", "TriageTargets off: no target ranking"),
        F("noprinted", "battle", "TriagePrintedFirst off: hand cards before printed abilities"),
        F("pattack", "attack", "PlannerAttack: the swing-based attack estimate decides whether and where to attack"),
        F("pdiscard", "discard", "PlannerDiscard: the interview's discard rule"),
        F("reserve", "plan", "PlannerReserve: Gold held back for Battle tricks"),
        // ---- the search (TrainingOpponent.Lookahead.cs) ----
        R("look", "search", "battle lookahead: the best few battle actions are played out on redealt samples"),
        K("lkK", "search", "candidates searched (3)"),
        K("lkN", "search", "samples per candidate (4)"),
        K("lkM", "search", "margin a searched action needs over the Contender's own choice (0)"),
        R("lkAtk", "search", "whether to attack is searched"),
        R("lkDef", "search", "the defenders' assignment is searched"),
        R("lkVar", "search", "with lkDef, the defence shapes are compared too"),
        K("lkDrop", "search", "after half the samples a candidate trailing by more than this is dropped (0, off)"),
        R("lkOpp", "search", "the other seat is played by the champion in playouts"),
        K("lkT", "search", "a search's time limit in seconds (10)"),
        K("lkB", "search", "a search's work budget in playout decisions (0, none)"),
        K("lkBB", "search", "the budget of a search played through later turns (lkB times twice lkBH)"),
        R("lkAny", "search", "at the time limit, decide on the samples every candidate completed"),
        K("lkP", "search", "a Province's worth (12)"),
        K("lkU", "search", "a unit's worth per Gold Cost plus Force (1)"),
        K("lkH", "search", "Honor's worth (1.5)"),
        K("lkC", "search", "a card in hand's worth (1.5)"),
        R("lkVal", "search", "the end state is scored by ValueModel's win chance"),
        R("lkPS", "search", "Provinces are worth more as fewer remain"),
        R("lkHS", "search", "Honor past lkHT is worth lkHx times more"),
        K("lkHT", "search", "lkHS's threshold (30)"),
        K("lkHx", "search", "lkHS's multiplier (3)"),
        R("lkDedup", "search", "a move offered twice is searched once"),
        R("lkPlan", "search", "the attack is chosen by search at the declaration and carried out at the infantry declaration"),
        R("lkShapes", "search", "with lkPlan, two more attack shapes are searched"),
        R("lkFar", "search", "with lkPlan, the attack is played out through the opponent's next attack"),
        R("lkIntent", "search", "playouts are scored for the plan's intent, weighted lkIw"),
        K("lkIw", "search", "lkIntent's weight (1.5)"),
        R("lkAct", "search", "an Action-phase move is searched too"),
        R("lkBuy", "search", "a Dynasty purchase is chosen by search (lkBK purchases on lkBN samples)"),
        K("lkBK", "search", "lkBuy's purchases searched (3)"),
        K("lkBN", "search", "lkBuy's samples (3)"),
        K("lkG", "search", "a Holding's production worth in the search (3 under lkBuy, else 0)"),
        R("lkMix", "search", "with lkBuy, the best Holding and the best Personality are always searched"),
        K("lkBH", "search", "turns a purchase playout runs ahead (2)"),
        R("lkClock", "search", "a playout ending with the next player at 40 Honor is their win; one within lkCl rounds of it scores -lkCw"),
        K("lkCl", "search", "lkClock's rounds (1)"),
        K("lkCw", "search", "lkClock's penalty (500)"),
        // ---- the Read (docs/bots/CONAN_STRATEGY_BRIEF.md section 4) ----
        R("read", "read", "the Read in shadow: ledger, belief, exchange, threat, questions and posture, logged, driving nothing"),
        R("readD", "defence", "family D: the defence assignment is read from the Read (screened null on block 9700; off)"),
        R("rdDefend", "defence", "with readD, a costly hold is not conceded while a Province is spare"),
        R("readA", "attack", "family A: the attack and its shape are read from the Read's opportunity (screened null on block 9800; off)"),
        R("raShape", "attack", "with readA, only the shape: the Contender decides attack or hold"),
        R("readE", "battle", "family E: at the bot's own Battle priority the Fight decides from the Read's exchange: pass while the standing result is the one wanted, else the cheapest sufficient action"),
        R("reOpen", "battle", "with readE, what an action reaches and whether anything can is read against their open hand (shown answers only); the pass test keeps their likely hand"),
    ];

    public static readonly IReadOnlyDictionary<string, Entry> ByName = Entries.ToDictionary(e => e.Name, StringComparer.Ordinal);

    /// <summary>Why this token cannot be in a rule set, or null when it can: a switch must be declared; "name=value" needs a declared knob and a number.</summary>
    public static string? Problem(string token)
    {
        var eq = token.IndexOf('=');
        if (eq < 0) return ByName.TryGetValue(token, out var e) && e.Switch ? null : $"'{token}' is not a rule";
        var name = token[..eq];
        if (!ByName.TryGetValue(name, out var k) || !k.Knob) return $"'{name}' is not a knob";
        return double.TryParse(token[(eq + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? null : $"'{token}' is not a number";
    }

    /// <summary>Throws when any token is unknown, naming every offender: a misspelt rule fails the run instead of silently playing the baseline.</summary>
    public static void Check(IEnumerable<string> tokens)
    {
        var problems = tokens.Select(Problem).Where(p => p is not null).ToArray();
        if (problems.Length > 0) throw new ArgumentException($"unknown rule(s): {string.Join("; ", problems)}. Every rule and knob is declared in RuleRegistry.cs.");
    }

    /// <summary>The knob's name in a "name=value" token, or the token itself for a switch.</summary>
    public static string NameOf(string token) { var eq = token.IndexOf('='); return eq < 0 ? token : token[..eq]; }
}
