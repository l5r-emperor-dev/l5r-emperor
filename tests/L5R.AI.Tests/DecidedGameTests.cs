using L5R.AI;
using L5R.Core;

// Issue #1283 (Mendon): in its own Dynasty Phase the bot, at every level, passes on buying from its provinces once the game is settled before anything bought could act: the opponent wins
// by Honor or Enlightenment when their next turn begins, or the bot is at -20 or lower Honor (it loses at the start of its End Phase, which follows). Not when the Dynasty Phase comes before
// the Attack Phase this turn, since a purchase can still attack then.
internal static class DecidedGameTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static readonly CardDefinition PersonDef = BattlePolicyTests.Person();
    private static readonly CardView Body = new("buy-p", PersonDef with { GoldCost = 3 }, 0, Zone.Province, true, false, null, null, PersonDef.Force);

    private static MatchView Dynasty(Func<PlayerView, PlayerView>? self = null, Func<PlayerView, PlayerView>? opponent = null, string[]? phaseOrder = null)
    {
        var view = MatchEngine.CreateStarterMatch().GetView(0);
        var me = view.Players[0] with { Honor = 10, Home = [], Army = [], Provinces = [new ProvinceView(0, 5, false, Body, null)], Hand = [], HandCount = 0 };
        var them = view.Players[1] with { Honor = 10, Home = [], Army = [] };
        return view with { Phase = Phase.Dynasty, ActivePlayerId = 0, PriorityPlayerId = 0, PendingChoice = null, TurnNumber = 8, FavorPlayerId = null, PhaseOrder = phaseOrder, Players = [self?.Invoke(me) ?? me, opponent?.Invoke(them) ?? them] };
    }

    private static readonly ActionOption[] Offered =
    [
        new("purchase", "Purchase buy-p", true, null, null, Body.Id, Quote: new PurchaseQuote(Body.Id, 3, false, false, false, null, [new PaymentOption(["gold-source"], 3, 3, 0)])),
        new("endDynasty", "End Dynasty", true, null, null),
    ];

    private static void Expect(MatchView view, string want, string why)
    {
        foreach (var level in new[] { BotLevel.Easy, BotLevel.Moderate, BotLevel.Hard })
        {
            var command = TrainingOpponent.Choose(view, Offered, level.NewMemory()).Command;
            Check(command?.Type == want, $"{level.Label()}: {why} — chose {command?.Type ?? "nothing"}");
        }
    }

    public static readonly (string, Action)[] Tests =
    [
        ("an undecided game: every level buys the Personality (the control)", () => Expect(Dynasty(), "purchase", "nothing is settled")),
        ("the opponent wins by Honor or Enlightenment when their turn begins: every level ends the Dynasty Phase without buying", () =>
            Expect(Dynasty(opponent: p => p with { Honor = 40, WinsWhenTurnBegins = true }), "endDynasty", "the opponent's next turn begins with a win")),
        ("the bot at -20 Honor loses as its End Phase begins: every level ends the Dynasty Phase without buying", () =>
            Expect(Dynasty(self: p => p with { Honor = -20, DishonorLossPending = true }), "endDynasty", "-20 Honor")),
        ("a Dishonor loss already marked still settles it after Honor climbs back to -15", () =>
            Expect(Dynasty(self: p => p with { Honor = -15, DishonorLossPending = true }), "endDynasty", "the marked Dishonor loss")),
        ("-19 Honor and an opponent at 39 settle nothing: every level still buys", () =>
            Expect(Dynasty(self: p => p with { Honor = -19 }, opponent: p => p with { Honor = 39 }), "purchase", "nothing is settled")),
        ("with the Dynasty Phase before the Attack Phase this turn (The Deciding Moment), every level still buys: the Personality can attack", () =>
            Expect(Dynasty(opponent: p => p with { Honor = 40, WinsWhenTurnBegins = true }, phaseOrder: ["Straighten", "Events", "Action", "Dynasty", "Attack", "End"]), "purchase", "the phases are reversed")),
    ];
}
