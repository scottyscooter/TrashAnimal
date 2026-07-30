namespace TrashAnimal.TokenPhase;

/// <summary>Completes the active token after subflows (e.g. Bandit window) without coupling handlers to <see cref="TokenPhaseCoordinator"/>.</summary>
internal interface ITokenPhaseTokenCompletion
{
    /// <param name="resolvedWithNoEffectToken">Set to the token that fizzled (produced no effect) if
    /// completing/repeating this pass caused a fizzle; null otherwise.</param>
    bool TryFinishCurrentTokenPassOrRepeat(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken);
}
