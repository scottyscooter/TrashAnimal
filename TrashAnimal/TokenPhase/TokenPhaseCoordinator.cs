namespace TrashAnimal.TokenPhase;

/// <summary>Token resolution phase: remaining roll tokens, per-token substeps, and eligibility for card plays.</summary>
internal sealed class TokenPhaseCoordinator
{
    private readonly TokenPhaseCardEligibility _eligibility = new();
    private readonly TokenPhaseViewBuilder _viewBuilder;
    private readonly TokenPhaseInterruptCardPlay _interruptCards;
    private readonly TokenPhaseTokenResolver _tokenResolver;
    private readonly TokenPhaseAllowedActionsProvider _allowedActions;
    private readonly TokenPhaseGameActionDispatcher _gameActions;
    private TokenPhaseState? _state;

    public TokenPhaseCoordinator(GameSession session)
    {
        _viewBuilder = new TokenPhaseViewBuilder(session, _eligibility);
        _interruptCards = new TokenPhaseInterruptCardPlay(session, _eligibility);
        _tokenResolver = new TokenPhaseTokenResolver(session, _eligibility, _viewBuilder);
        _allowedActions = new TokenPhaseAllowedActionsProvider(session, _interruptCards);
        _gameActions = new TokenPhaseGameActionDispatcher(session, _interruptCards, _tokenResolver);
    }

    public bool IsActive => _state is not null;

    public void Begin(IReadOnlyList<TokenAction> tokens)
    {
        _state = new TokenPhaseState(tokens);
    }

    public void Clear()
    {
        _state = null;
    }

    public TokenPhaseView BuildView(int viewPlayerIndex) => _viewBuilder.BuildView(_state, viewPlayerIndex);

    public IReadOnlyList<TokenAction> GetRecycleReplacementOptions()
    {
        if (_state is null)
            return Array.Empty<TokenAction>();
        return _viewBuilder.GetRecycleOptions(_state);
    }

    public IReadOnlyList<GameAction> GetAllowedActions(int playerIndex)
    {
        if (_state is null)
            return Array.Empty<GameAction>();
        return _allowedActions.GetAllowedActions(_state, playerIndex);
    }

    public bool TryApplyGameAction(int playerIndex, GameAction action, out string? error)
    {
        error = null;
        if (_state is null)
        {
            error = "Token phase is not active.";
            return false;
        }

        return _gameActions.TryApplyGameAction(playerIndex, action, _state, out error);
    }

    public bool TryBanditPass(int opponentIndex, out string? error)
    {
        error = null;
        if (_state is null || _state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Not awaiting a Bandit response.";
            return false;
        }

        return _tokenResolver.BanditHandler.TryBanditPass(opponentIndex, _state, out error);
    }

    public bool TryBanditStashMatchingCard(int opponentIndex, Guid cardId, out string? error)
    {
        error = null;
        if (_state is null || _state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Not awaiting a Bandit response.";
            return false;
        }

        return _tokenResolver.BanditHandler.TryBanditStashMatchingCard(opponentIndex, cardId, _state, out error);
    }

    public bool TryStashTrashPickCard(int playerIndex, Guid cardId, out string? error)
    {
        error = null;
        if (_state is null || _state.Step != TokenPhaseStep.StashTrashPickCard)
        {
            error = "Not choosing a StashTrash stash card.";
            return false;
        }

        return _tokenResolver.TryStashTrashPickCard(playerIndex, cardId, _state, out error);
    }

    public bool TryDoubleStashSubmit(int playerIndex, IReadOnlyList<Guid> cardIds, out string? error)
    {
        error = null;
        if (_state is null || _state.Step != TokenPhaseStep.DoubleStashChoosingCards)
        {
            error = "Not in DoubleStash resolution.";
            return false;
        }

        return _tokenResolver.TryDoubleStashSubmit(playerIndex, cardIds, _state, out error);
    }

    public bool TryRecycleReplacementPick(int playerIndex, TokenAction replacement, out string? error)
    {
        error = null;
        if (_state is null || _state.Step != TokenPhaseStep.RecycleChoosingReplacement)
        {
            error = "Not choosing a Recycle replacement.";
            return false;
        }

        return _tokenResolver.TryRecycleReplacementPick(playerIndex, replacement, _state, out error);
    }

    /// <summary>Called when a steal attempt ends and the session returns to <see cref="GameState.TokenPhase"/>.</summary>
    public void OnStealResolvedWhileInTokenPhase(bool stealTokenWasActive)
    {
        if (_state is null)
            return;

        if (stealTokenWasActive)
            _ = _tokenResolver.TryFinishCurrentTokenPassOrRepeat(_state, out _);
    }

    public bool ActiveTokenIsSteal => _state?.ActiveToken == TokenAction.Steal;
}
