using L5R.AI;
using L5R.Core;

// Issues #1203 and #1209: the champion challenges only a lower duel stat (or a tie when a Duelist challenges a non-Duelist), and answers a challenge from a lower stat by accepting and striking at once.
internal static class ChallengeGuardTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Duel ties (issue #1209): a Duelist challenges a non-Duelist it ties, nobody else challenges a tie", DuelistTies),
        ("Duel replies (issue #1209): challenged by a lower duel stat the champion accepts and strikes at once, on both decision paths; otherwise unchanged", AcceptAndStrike),
        ("Challenge guard (issue #1203): no challenge when the target's Force, Chi or Personal Honor is higher or ties (see the Duelist exception below), one when the challenger is higher, and a switch turns it off", Guard),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    private static MatchView Duel(Func<CardView, CardView> own, Func<CardView, CardView> enemy)
    {
        var v = BattlePolicyTests.Battle(4, 4, 6);
        return v with { Players = [v.Players[0] with { Army = [own(v.Players[0].Army[0])] }, v.Players[1] with { Army = [enemy(v.Players[1].Army[0])] }] };
    }

    private static string Issued(MatchView v, string ability, BotContext memory)
    {
        var duel = BattlePolicyTests.Use(v, ability, "own-main", "enemy-main", ["enemy-main"]);
        return TrainingOpponent.Choose(v, [BattlePolicyTests.Pass(v), duel], memory).Command?.Type ?? "none";
    }

    private static CardView WithHonor(CardView c, int honor) => c with { Definition = c.Definition! with { PersonalHonor = honor } };

    private static void Guard()
    {
        // The context remembers what the bot has done, so every decision gets a fresh one.
        var on = () => new BotContext(); var off = () => new BotContext { ChallengeGuard = false };
        // Force: the stat is the card's current Force on the table.
        Check(Issued(Duel(c => c with { Force = 5 }, c => c with { Force = 5 }), "training-duel-force", on()) == "pass", "a Force tie between ordinary Personalities is not challenged");
        Check(Issued(Duel(c => c with { Force = 5 }, c => c with { Force = 4 }), "training-duel-force", on()) == "useAbility", "a higher own Force is challenged");
        var weaker = Duel(c => c with { Force = 4 }, c => c with { Force = 5 });
        Check(Issued(weaker, "training-duel-force", on()) == "pass", "a target with higher Force is not challenged");
        Check(Issued(weaker, "training-duel-force", off()) == "useAbility", "with the guard off the same challenge is issued");
        // Personal Honor.
        Check(Issued(Duel(c => WithHonor(c, 3), c => WithHonor(c, 3)), "training-duel-honor", on()) == "pass", "a Personal Honor tie between ordinary Personalities is not challenged");
        Check(Issued(Duel(c => WithHonor(c, 2), c => WithHonor(c, 3)), "training-duel-honor", on()) == "pass", "a target with higher Personal Honor is not challenged");
        Check(Issued(Duel(c => WithHonor(c, 2) with { Dishonorable = false }, c => WithHonor(c, 3) with { Dishonorable = true }), "training-duel-honor", on()) == "useAbility", "a dishonorable target counts as 0 Personal Honor");
        // Chi (the default stat).
        Check(Issued(Duel(c => c with { Chi = 3 }, c => c with { Chi = 3 }), "training-recon-duel", on()) == "pass", "a Chi tie between ordinary Personalities is not challenged");
        Check(Issued(Duel(c => c with { Chi = 2 }, c => c with { Chi = 3 }), "training-recon-duel", on()) == "pass", "a target with higher Chi is not challenged");
        // Only challenges: an ordinary ability against the same stronger target is not blocked.
        var bow = BattlePolicyTests.Use(weaker, "training-bow-at-battle", "own-main", null, ["enemy-main"]);
        Check(TrainingOpponent.Choose(weaker, [BattlePolicyTests.Pass(weaker), bow], on()).Command?.Type == TrainingOpponent.Choose(weaker, [BattlePolicyTests.Pass(weaker), bow], off()).Command?.Type, "an action that is not a challenge is not affected");
    }

    private static CardView Duelist(CardView c) => c with { Definition = c.Definition! with { Keywords = ["Duelist"] } };
    private static CardView Plain(CardView c) => c with { Definition = c.Definition! with { Keywords = [] } };

    private static void DuelistTies()
    {
        BotContext on() => new();
        Check(Issued(Duel(c => Duelist(c) with { Force = 5 }, c => Plain(c) with { Force = 5 }), "training-duel-force", on()) == "useAbility", "a Duelist challenges a non-Duelist it ties");
        Check(Issued(Duel(c => Duelist(c) with { Force = 5 }, c => Duelist(c) with { Force = 5 }), "training-duel-force", on()) == "pass", "two Duelists on a tie: not challenged");
        Check(Issued(Duel(c => Plain(c) with { Force = 5 }, c => Duelist(c) with { Force = 5 }), "training-duel-force", on()) == "pass", "a non-Duelist does not challenge a Duelist it ties");
        Check(Issued(Duel(c => Duelist(c) with { Force = 4 }, c => Plain(c) with { Force = 5 }), "training-duel-force", on()) == "pass", "a Duelist still does not challenge a higher stat");
    }

    private static MatchView Challenged(string stage, int challengerStat, int ownStat, int focusCount = 0)
    {
        var v = BattlePolicyTests.Battle(4, 4, 6);
        var own = v.Players[0].Army[0] with { Chi = ownStat }; var foe = v.Players[1].Army[0] with { Chi = challengerStat };
        return v with
        {
            Phase = Phase.Action,
            CurrentBattleProvince = null,
            PriorityPlayerId = 0,
            ActivePlayerId = 1,
            PendingDuel = new DuelView(1, 0, "enemy-main", "own-main", "Chi", stage, 0, false),
            Players = [v.Players[0] with { Army = [own], FocusCount = focusCount }, v.Players[1] with { Army = [foe] }],
        };
    }

    private static ActionOption Cmd(MatchView v, string type, string? card = null) => new(type, type, true, null, new(type, 0, v.Version, CardId: card));

    private static void AcceptAndStrike()
    {
        string Pick(MatchView v, params ActionOption[] options) => TrainingOpponent.Choose(v, options, new BotContext()).Command?.Type ?? "none";
        string PickDuel(MatchView v, params ActionOption[] options) => DuelOpponent.Choose(v, options)?.Type ?? "none";
        var ask = Challenged("AwaitingAccept", 2, 3);
        ActionOption[] reply = [Cmd(ask, "declineDuel"), Cmd(ask, "acceptDuel")];
        Check(Pick(ask, reply) == "acceptDuel" && PickDuel(ask, reply) == "acceptDuel", "challenged from below: accepts");
        var focusing = Challenged("Focusing", 2, 3);
        ActionOption[] turn = [Cmd(focusing, "focus", "own-card"), Cmd(focusing, "strike")];
        Check(Pick(focusing, turn) == "strike" && PickDuel(focusing, turn) == "strike", "challenged from below: strikes at once, before focusing");
        var focused = Challenged("Focusing", 2, 3, focusCount: 1);
        Check(Pick(focused, [Cmd(focused, "strike")]) == "strike", "a strike after the first focus is still just the ordinary choice, not forced");
        var tie = Challenged("Focusing", 3, 3);
        Check(PickDuel(tie, [Cmd(tie, "focus", "own-card"), Cmd(tie, "strike")]) == "focus", "challenged on a tie: the ordinary play (focus first), not an immediate strike");
        var above = Challenged("Focusing", 4, 3);
        Check(PickDuel(above, [Cmd(above, "focus", "own-card"), Cmd(above, "strike")]) == "focus", "challenged from above: the ordinary play");
        var noStrike = Challenged("Focusing", 2, 3);
        Check(PickDuel(noStrike, [Cmd(noStrike, "focus", "own-card")]) == "focus", "when the rules offer no Strike yet it focuses as before");
    }
}
