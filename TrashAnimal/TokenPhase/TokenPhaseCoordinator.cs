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
        _allowedActions = new TokenPhaseAllowedActionsProvider(session, _interruptCards, _eligibility);
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

    public bool TryApplyGameAction(int playerIndex, GameAction action, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null)
        {
            error = "Token phase is not active.";
            return false;
        }

        return _gameActions.TryApplyGameAction(playerIndex, action, _state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryBanditPass(int opponentIndex, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null || _state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Not awaiting a Bandit response.";
            return false;
        }

        return _tokenResolver.BanditHandler.TryBanditPass(opponentIndex, _state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryBanditStashMatchingCard(int opponentIndex, Guid cardId, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null || _state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Not awaiting a Bandit response.";
            return false;
        }

        return _tokenResolver.BanditHandler.TryBanditStashMatchingCard(opponentIndex, cardId, _state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryStashTrashPickCard(int playerIndex, Guid cardId, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null || _state.Step != TokenPhaseStep.StashTrashPickCard)
        {
            error = "Not choosing a StashTrash stash card.";
            return false;
        }

        return _tokenResolver.TryStashTrashPickCard(playerIndex, cardId, _state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryPlayShinyWithVictimChoice(int victimIndex, out string? error)
    {
        error = null;
        if (_state is null)
        {
            error = "Token phase is not active.";
            return false;
        }
        return _interruptCards.TryPlayShinyTokenPhaseWithVictimChoice(_state, victimIndex, out error);
    }

    public bool TryPlayFeeshWithCardChoice(Guid discardCardId, out string? error)
    {
        error = null;
        if (_state is null)
        {
            error = "Token phase is not active.";
            return false;
        }
        return _interruptCards.TryPlayFeeshTokenPhaseWithCardChoice(_state, discardCardId, out error);
    }

    public bool TryDoubleStashSubmit(int playerIndex, IReadOnlyList<Guid> cardIds, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null || _state.Step != TokenPhaseStep.DoubleStashChoosingCards)
        {
            error = "Not in DoubleStash resolution.";
            return false;
        }

        return _tokenResolver.TryDoubleStashSubmit(playerIndex, cardIds, _state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryRecycleReplacementPick(int playerIndex, TokenAction replacement, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null || _state.Step != TokenPhaseStep.RecycleChoosingReplacement)
        {
            error = "Not choosing a Recycle replacement.";
            return false;
        }

        return _tokenResolver.TryRecycleReplacementPick(playerIndex, replacement, _state, out error, out resolvedWithNoEffectToken);
    }

    /// <summary>Auto-resolves the Steal token when zero opponents have a card in hand to steal. See
    /// <see cref="TokenPhaseTokenResolver.TryResolveStealAutoWithNoTargets"/>.</summary>
    public bool TryResolveStealAutoWithNoTargets(out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null)
        {
            error = "Token phase is not active.";
            return false;
        }

        return _tokenResolver.TryResolveStealAutoWithNoTargets(_state, out error, out resolvedWithNoEffectToken);
    }

    /// <summary>Begins the Steal token's hand-steal with an already-chosen victim (the API's explicit-choice
    /// path). See <see cref="TokenPhaseTokenResolver.TryStartHandStealWithVictim"/>.</summary>
    public bool TryStartHandStealWithVictim(int victimIndex, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null)
        {
            error = "Token phase is not active.";
            return false;
        }

        return _tokenResolver.TryStartHandStealWithVictim(victimIndex, _state, out error, out resolvedWithNoEffectToken);
    }

    /// <summary>Called when a steal attempt ends and the session returns to <see cref="GameState.TokenPhase"/>.
    /// Returns false with <paramref name="error"/> set if finishing/repeating the token pass failed (e.g. an
    /// MmmPie repeat of Steal parking in <see cref="TokenPhaseStep.StealChoosingVictim"/> is not itself a
    /// failure and returns true) — see <see cref="TokenPhaseTokenCompletionEngine"/>.</summary>
    public bool OnStealResolvedWhileInTokenPhase(bool stealTokenWasActive, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_state is null || !stealTokenWasActive)
            return true;

        return _tokenResolver.TryFinishCurrentTokenPassOrRepeat(_state, out error, out resolvedWithNoEffectToken);
    }

    public bool ActiveTokenIsSteal => _state?.ActiveToken == TokenAction.Steal;
}
