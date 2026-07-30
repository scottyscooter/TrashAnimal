using TrashAnimal.TokenPhase;

namespace TrashAnimal;

/// <summary>
/// Thin one-line delegations from <see cref="GameSession"/>'s public surface to
/// <see cref="TokenPhaseCoordinator"/>. Split out of <c>GameSession.cs</c> (kept over this repo's 400-line
/// split-immediately guidance) since these are a cohesive, self-contained group of pass-through wrappers.
/// </summary>
public sealed partial class GameSession
{
    public bool TryBanditPass(int opponentIndex, out string? error, out TokenAction? resolvedWithNoEffectToken) =>
        _tokenPhaseCoordinator.TryBanditPass(opponentIndex, out error, out resolvedWithNoEffectToken);

    public bool TryBanditStashMatchingCard(int opponentIndex, Guid cardId, out string? error, out TokenAction? resolvedWithNoEffectToken) =>
        _tokenPhaseCoordinator.TryBanditStashMatchingCard(opponentIndex, cardId, out error, out resolvedWithNoEffectToken);

    public bool TryTokenPhaseStashTrashPickCard(int playerIndex, Guid cardId, out string? error, out TokenAction? resolvedWithNoEffectToken) =>
        _tokenPhaseCoordinator.TryStashTrashPickCard(playerIndex, cardId, out error, out resolvedWithNoEffectToken);

    public bool TryTokenPhaseDoubleStash(int playerIndex, IReadOnlyList<Guid> cardIds, out string? error, out TokenAction? resolvedWithNoEffectToken) =>
        _tokenPhaseCoordinator.TryDoubleStashSubmit(playerIndex, cardIds, out error, out resolvedWithNoEffectToken);

    public bool TryTokenPhaseRecyclePick(int playerIndex, TokenAction replacement, out string? error, out TokenAction? resolvedWithNoEffectToken) =>
        _tokenPhaseCoordinator.TryRecycleReplacementPick(playerIndex, replacement, out error, out resolvedWithNoEffectToken);

    public IReadOnlyList<TokenAction> GetTokenPhaseRecycleOptions() =>
        _tokenPhaseCoordinator.GetRecycleReplacementOptions();
}
