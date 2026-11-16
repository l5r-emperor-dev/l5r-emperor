namespace L5R.Core;

public sealed partial class MatchEngine
{
    /// <summary>Isolated real-engine review positions. Never injected into decks or online rooms.</summary>
    public static MatchEngine CreateDecisionShowcase(string kind)
    {
        if (kind is "preserving" or "rin" or "conqueror" or "straighten" or "discard") return CreateGenericDecisionShowcase(kind);
        if (kind == "kamina") return CreateKaminaShowcase();
        if (kind.StartsWith("otomo", StringComparison.Ordinal)) return CreateOtomoTokenShowcase(kind == "otomo-favor"); // issue #1275: a Favor token paid in place of the Imperial Favor
        if (kind.StartsWith("p25-", StringComparison.Ordinal)) return CreateDecisionInfraShowcase(kind); // P25 (DecisionInfraShowcase.cs)
        if (kind is not ("yumaru" or "fukui" or "fukui-bowed")) throw new ArgumentException("Unknown decision showcase.", nameof(kind));
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Battle; state.ActivePlayerId = 0; state.PriorityPlayerId = 0;
        state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.PendingChoice = null;
        CardInstance Put(string definition, int owner, Zone zone, string? parent = null)
        {
            var card = new CardInstance { Id = "decision-review-" + state.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = 0, AttachedTo = parent };
            state.Cards.Add(card.Id, card); return card;
        }
        var enemy = Put("oracle-l5r-3636", 1, Zone.Battlefield);
        var source = Put(kind == "yumaru" ? "oracle-l5r-5181" : "oracle-l5r-4375", 0, Zone.Battlefield);
        if (kind == "yumaru") Put("oracle-l5r-6481", 0, Zone.Attached, source.Id);
        else Put("oracle-l5r-9785", 0, Zone.Battlefield).Bowed = kind == "fukui-bowed";
        var engine = FromTestState(state);
        if (kind == "yumaru")
        {
            var action = engine.QueryActions(0).Single(a => a.Enabled && a.CardId == source.Id && a.Command?.AbilityId == "yumaru-move-home");
            var result = engine.Execute(action.Command! with { TargetId = enemy.Id });
            if (!result.Accepted || engine.State.PendingChoice?.Type != "effectChoice") throw new InvalidOperationException("Yumaru showcase failed to open its engine choice.");
        }
        return engine;
    }
    // Akodo Kamina (issue #691): "bow Kamina, or discard a card", in the Action Phase with two cards in hand.
    private static MatchEngine CreateKaminaShowcase()
    {
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        foreach (var id in state.Players[0].Hand.ToArray()) { state.Cards.Remove(id); }
        state.Players[0].Hand.Clear();
        CardInstance Put(string definition, int owner, Zone zone)
        {
            var card = new CardInstance { Id = "kamina-review-" + state.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand };
            state.Cards.Add(card.Id, card); if (zone == Zone.Hand) state.Players[owner].Hand.Add(card.Id); return card;
        }
        Put("oracle-l5r-224", 0, Zone.Home); Put("training-crane-samurai", 0, Zone.Home);
        Put("oracle-l5r-1450", 0, Zone.Hand); Put("oracle-l5r-5523", 0, Zone.Hand);
        return FromTestState(state);
    }
    // Kyuden Otomo - exp (issue #1275): one Favor token on the Stronghold, Doji Shunya at home (Limited: discard the Imperial Favor) and a card in hand for the Favor's own Limited action;
    // the Imperial Favor held or not.
    private static MatchEngine CreateOtomoTokenShowcase(bool holdsFavor)
    {
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Action; state.ActivePlayerId = state.PriorityPlayerId = 0; state.PendingChoice = null;
        state.FavorPlayerId = holdsFavor ? 0 : null;
        var otomo = state.Cards[state.Players[0].StrongholdId];
        otomo.DefinitionId = "oracle-l5r-4646";
        otomo.Tokens.Add(new CardToken("otomo-review-token", "Favor", "otomo-review", null, 0));
        foreach (var id in state.Players[0].Hand.Skip(1).ToArray()) { state.Cards.Remove(id); state.Players[0].Hand.Remove(id); }
        var shunya = new CardInstance { Id = "otomo-review-shunya", DefinitionId = "oracle-l5r-2158", OwnerId = 0, Zone = Zone.Home, FaceUp = true };
        state.Cards.Add(shunya.Id, shunya);
        var wrath = new CardInstance { Id = "otomo-review-wrath", DefinitionId = "oracle-l5r-8127", OwnerId = 0, Zone = Zone.Hand };
        state.Cards.Add(wrath.Id, wrath); state.Players[0].Hand.Add(wrath.Id);
        var enemy = new CardInstance { Id = "otomo-review-enemy", DefinitionId = "training-crane-samurai", OwnerId = 1, Zone = Zone.Home, FaceUp = true };
        state.Cards.Add(enemy.Id, enemy);
        return FromTestState(state);
    }
    private static MatchEngine CreateGenericDecisionShowcase(string kind)
    {
        var state = CreatePublishedTestMatch(42).State;
        state.Phase = Phase.Battle; state.ActivePlayerId = state.PriorityPlayerId = 0;
        state.CurrentBattleProvince = 0; state.UnresolvedBattlefields = [0]; state.PendingChoice = null;
        CardInstance Put(string definition, int owner, Zone zone)
        {
            var card = new CardInstance { Id = "generic-review-" + state.Cards.Count, DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = true, ProvinceIndex = zone == Zone.Battlefield ? 0 : null };
            state.Cards.Add(card.Id, card); if (zone == Zone.Hand) state.Players[owner].Hand.Add(card.Id); return card;
        }
        void Execute(MatchEngine e, GameCommand command)
        { if (!e.Execute(command).Accepted) throw new InvalidOperationException("Generic decision fixture trigger failed: " + kind); }
        if (kind == "straighten")
        {
            state.Phase = Phase.Dynasty;
            Put("training-choose-straighten-samurai", 1, Zone.Home).Bowed = true;
            var e = FromTestState(state); Execute(e, new("endDynasty", 0, e.Version)); return e;
        }
        if (kind == "discard")
        {
            state.Phase = Phase.Action; state.CurrentBattleProvince = null;
            state.PendingForcedDiscards.Add(new(0, "Daigotsu Bofana"));
            var e = FromTestState(state); e.OpenOverconfidentDrawWindow(1); return e;
        }
        if (kind == "conqueror")
        {
            foreach (var id in new[] { "oracle-l5r-5181", "oracle-l5r-2060" })
            { var c = Put(id, 0, Zone.Battlefield); c.GrantedKeywords.Add(new("Conqueror", "review fixture", "Turn")); }
            Put("training-lion-samurai", 1, Zone.Battlefield).Bowed = true;
            var e = FromTestState(state); Execute(e, new("pass", 0, e.Version)); Execute(e, new("pass", 1, e.Version)); return e;
        }
        var target = Put("oracle-l5r-3636", 1, kind == "rin" ? Zone.Home : Zone.Battlefield);
        var source = Put(kind == "rin" ? "oracle-l5r-7151" : "oracle-l5r-6060", 0, kind == "rin" ? Zone.Home : Zone.Hand);
        if (kind == "rin") { state.Phase = Phase.Action; state.CurrentBattleProvince = null; }
        else Put("oracle-l5r-2060", 0, Zone.Battlefield);
        var engine = FromTestState(state);
        Execute(engine, new("useAbility", 0, engine.Version, CardId: source.Id, TargetId: target.Id, AbilityId: kind == "rin" ? "rin-shame" : "preserving-honor")); return engine;
    }

}
