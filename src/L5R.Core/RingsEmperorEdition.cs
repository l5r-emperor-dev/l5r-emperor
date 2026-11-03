namespace L5R.Core;

// ======================================================================================================================================================================================
// RING OF WATER (Emperor Edition scan, oracle-l5r-6339): "Battle: Bow this card or discard it from your hand, and choose your performing Personality: Move him home or to a battlefield with one or
// more enemy units. If he moved, straighten his unit." The ability is two offered actions (ring-of-water-move-home, ring-of-water-move-battlefield) in CardAbilities.cs, sharing the Ring's one use.
// The battlefield is chosen with the action (GameCommand.Province, MoveSpec.EnemyBattlefield). Moving him to the CURRENT battlefield is an Absent Battle action (the Exception to the Rules of
// Presence and Location, as for Back to the Front); any other battlefield needs him at the current battlefield, which is checked here.
// ======================================================================================================================================================================================

public sealed partial class MatchEngine
{
    private void ValidateRingOfWaterLate(string id, GameCommand command)
    {
        if (id != "ring-of-water-move-battlefield" || command.PerformerId is null || command.Province is not int destination || _state.CurrentBattleProvince is not int current) return;
        if (destination != current) Require(AtBattle(Card(command.PerformerId)), "To move to a different battlefield he must be at the current battlefield.");
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterRingsEmperorEditionHooks() =>
        EngineHooks.ValidateLate.Register("ValidateRingOfWaterLate", 11200, static (engine, context) => engine.ValidateRingOfWaterLate(context.id, context.command));
}
