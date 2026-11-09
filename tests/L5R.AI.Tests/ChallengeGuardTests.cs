using L5R.AI;
using L5R.Core;

// Issue #1203: the champion does not challenge a Personality whose duel stat is higher than the challenger's; a tie is still allowed.
internal static class ChallengeGuardTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Challenge guard (issue #1203): no challenge when the target's Force, Chi or Personal Honor is higher, one on a tie or when the challenger is higher, and a switch turns it off", Guard),
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
        Check(Issued(Duel(c => c with { Force = 5 }, c => c with { Force = 5 }), "training-duel-force", on()) == "useAbility", "a Force tie is challenged");
        Check(Issued(Duel(c => c with { Force = 5 }, c => c with { Force = 4 }), "training-duel-force", on()) == "useAbility", "a higher own Force is challenged");
        var weaker = Duel(c => c with { Force = 4 }, c => c with { Force = 5 });
        Check(Issued(weaker, "training-duel-force", on()) == "pass", "a target with higher Force is not challenged");
        Check(Issued(weaker, "training-duel-force", off()) == "useAbility", "with the guard off the same challenge is issued");
        // Personal Honor.
        Check(Issued(Duel(c => WithHonor(c, 3), c => WithHonor(c, 3)), "training-duel-honor", on()) == "useAbility", "a Personal Honor tie is challenged");
        Check(Issued(Duel(c => WithHonor(c, 2), c => WithHonor(c, 3)), "training-duel-honor", on()) == "pass", "a target with higher Personal Honor is not challenged");
        Check(Issued(Duel(c => WithHonor(c, 2) with { Dishonorable = false }, c => WithHonor(c, 3) with { Dishonorable = true }), "training-duel-honor", on()) == "useAbility", "a dishonorable target counts as 0 Personal Honor");
        // Chi (the default stat).
        Check(Issued(Duel(c => c with { Chi = 3 }, c => c with { Chi = 3 }), "training-recon-duel", on()) == "useAbility", "a Chi tie is challenged");
        Check(Issued(Duel(c => c with { Chi = 2 }, c => c with { Chi = 3 }), "training-recon-duel", on()) == "pass", "a target with higher Chi is not challenged");
        // Only challenges: an ordinary ability against the same stronger target is not blocked.
        var bow = BattlePolicyTests.Use(weaker, "training-bow-at-battle", "own-main", null, ["enemy-main"]);
        Check(TrainingOpponent.Choose(weaker, [BattlePolicyTests.Pass(weaker), bow], on()).Command?.Type == TrainingOpponent.Choose(weaker, [BattlePolicyTests.Pass(weaker), bow], off()).Command?.Type, "an action that is not a challenge is not affected");
    }
}
