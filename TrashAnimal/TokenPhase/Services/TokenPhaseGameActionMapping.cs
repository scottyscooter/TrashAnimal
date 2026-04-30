namespace TrashAnimal.TokenPhase;

internal static class TokenPhaseGameActionMapping
{
    internal static GameAction ToResolveGameAction(TokenAction token) => token switch
    {
        TokenAction.StashTrash => GameAction.ResolveTokenStashTrash,
        TokenAction.DoubleStash => GameAction.ResolveTokenDoubleStash,
        TokenAction.DoubleTrash => GameAction.ResolveTokenDoubleTrash,
        TokenAction.Bandit => GameAction.ResolveTokenBandit,
        TokenAction.Steal => GameAction.ResolveTokenSteal,
        TokenAction.Recycle => GameAction.ResolveTokenRecycle,
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null)
    };
}
