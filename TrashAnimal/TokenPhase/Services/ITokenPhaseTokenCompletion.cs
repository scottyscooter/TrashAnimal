namespace TrashAnimal.TokenPhase;

/// <summary>Completes the active token after subflows (e.g. Bandit window) without coupling handlers to <see cref="TokenPhaseCoordinator"/>.</summary>
internal interface ITokenPhaseTokenCompletion
{
    bool TryFinishCurrentTokenPassOrRepeat(TokenPhaseState state, out string? error);
}
