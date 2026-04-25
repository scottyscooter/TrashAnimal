namespace TrashAnimal.RollPhase;

/// <summary>
/// Roll-phase <see cref="GameAction"/> backed by a specific card rule (not steal-response actions).
/// </summary>
public interface IGameplayHandler
{
    GameAction Action { get; }

    bool IsActionable(in RollPhaseOfferSnapshot snapshot);

    bool TryExecute(RollPhasePlayContext context, int playerIndex, out string? error);
}
