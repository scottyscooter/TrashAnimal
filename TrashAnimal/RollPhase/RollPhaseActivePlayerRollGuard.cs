namespace TrashAnimal.RollPhase;

/// <summary>
/// Shared validation for Shiny and Feesh: the game must be in <see cref="GameState.RollPhase"/>, phase one must still be active,
/// and only the current turn's player may perform the play (otherwise returns a user-facing error).
/// </summary>
internal static class RollPhaseActivePlayerRollGuard
{
    public static bool TryEnsureRollPhaseActivePlayer(RollPhasePlayContext context, int playerIndex, out string? error)
    {
        error = null;
        if (context.CurrentState != GameState.RollPhase)
            throw new InvalidOperationException(
                $"Invalid state for this action. Expected {GameState.RollPhase} but was {context.CurrentState}.");

        if (!context.IsPhaseOneActive)
        {
            error = "Shiny/Feesh may only be played during RollPhase of the active player's turn.";
            return false;
        }

        if (playerIndex != context.CurrentPlayerIndex)
        {
            error = "Only the active player may play Shiny or Feesh.";
            return false;
        }

        return true;
    }
}
