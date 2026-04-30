namespace TrashAnimal.TokenPhase;

internal sealed class TokenPhaseGameActionDispatcher
{
    private readonly GameSession _session;
    private readonly TokenPhaseInterruptCardPlay _interruptCards;
    private readonly TokenPhaseTokenResolver _tokenResolver;

    public TokenPhaseGameActionDispatcher(
        GameSession session,
        TokenPhaseInterruptCardPlay interruptCards,
        TokenPhaseTokenResolver tokenResolver)
    {
        _session = session;
        _interruptCards = interruptCards;
        _tokenResolver = tokenResolver;
    }

    public bool TryApplyGameAction(int playerIndex, GameAction action, TokenPhaseState state, out string? error)
    {
        error = null;
        if (state.Step == TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Use Bandit pass/stash methods for this step.";
            return false;
        }

        if (playerIndex != _session.CurrentPlayerIndex)
        {
            error = "Only the active player may act during TokenPhase.";
            return false;
        }

        return action switch
        {
            GameAction.PlayMmmPieTokenPhase => _interruptCards.TryPlayMmmPie(state, out error),
            GameAction.PlayShinyTokenPhase => _interruptCards.TryPlayShinyTokenPhase(state, out error),
            GameAction.PlayFeeshTokenPhase => _interruptCards.TryPlayFeeshTokenPhase(state, out error),
            GameAction.ResolveTokenStashTrash => _tokenResolver.TryStartToken(TokenAction.StashTrash, state, out error),
            GameAction.ResolveTokenDoubleStash => _tokenResolver.TryStartToken(TokenAction.DoubleStash, state, out error),
            GameAction.ResolveTokenDoubleTrash => _tokenResolver.TryStartToken(TokenAction.DoubleTrash, state, out error),
            GameAction.ResolveTokenBandit => _tokenResolver.TryStartToken(TokenAction.Bandit, state, out error),
            GameAction.ResolveTokenSteal => _tokenResolver.TryStartToken(TokenAction.Steal, state, out error),
            GameAction.ResolveTokenRecycle => _tokenResolver.TryStartToken(TokenAction.Recycle, state, out error),
            GameAction.TokenStashTrashDrawOne => _tokenResolver.TryStashTrashDraw(state, out error),
            GameAction.TokenStashTrashStashMode => _tokenResolver.TryStashTrashEnterStashMode(state, out error),
            GameAction.TokenDoubleStashSubmit => _tokenResolver.TryDoubleStashSubmit(
                _session.CurrentPlayerIndex,
                Array.Empty<Guid>(),
                state,
                out error),
            _ => UnknownAction(out error)
        };
    }

    private static bool UnknownAction(out string? error)
    {
        error = "Unknown or unsupported TokenPhase action.";
        return false;
    }
}
