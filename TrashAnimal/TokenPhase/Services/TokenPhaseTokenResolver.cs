using TrashAnimal.GameLog;

// TokenPhaseTokenResolver owns "start/resolve a token," used by both the CLI's delegate-driven
// path (TryStartToken) and the API's explicit-choice path (TryStartHandStealWithVictim,
// TryResolveStealAutoWithNoTargets). Any token that can't produce its normal effect (Steal with no hand
// candidates, Bandit with an empty deck) auto-resolves with a TokenResolvedWithNoEffectEvent instead of
// leaving the token stuck and unresolvable. Finishing/repeating an in-progress pass (including MmmPie's
// "resolve this token twice") is owned by TokenPhaseTokenCompletionEngine — see that type's doc comment.

namespace TrashAnimal.TokenPhase;

internal sealed class TokenPhaseTokenResolver : ITokenPhaseTokenCompletion
{
    private readonly GameSession _session;
    private readonly TokenPhaseCardEligibility _eligibility;
    private readonly TokenPhaseViewBuilder _viewBuilder;
    private readonly TokenPhaseBanditHandler _bandit;
    private readonly TokenPhaseStealHandler _steal;
    private readonly TokenPhaseTokenCompletionEngine _completion;

    public TokenPhaseTokenResolver(
        GameSession session,
        TokenPhaseCardEligibility eligibility,
        TokenPhaseViewBuilder viewBuilder)
    {
        _session = session;
        _eligibility = eligibility;
        _viewBuilder = viewBuilder;
        _bandit = new TokenPhaseBanditHandler(session, eligibility, this);
        _steal = new TokenPhaseStealHandler(session);
        _completion = new TokenPhaseTokenCompletionEngine(session, _bandit, _steal, viewBuilder);
    }

    internal TokenPhaseBanditHandler BanditHandler => _bandit;

    public bool TryFinishCurrentTokenPassOrRepeat(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken) =>
        _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);

    public bool TryStartToken(TokenAction token, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;

        if (state.Step != TokenPhaseStep.ChoosingNextToken)
        {
            error = "Pick a token only when choosing the next token.";
            return false;
        }

        if (!state.RemainingTokens.Contains(token))
        {
            error = "That token is not available.";
            return false;
        }

        if (!state.TokenResolutionStartLocked)
            state.TokenResolutionStartLocked = true;

        TokenPhaseTokenLogRecording.RecordTokenResolutionStarted(_session, token);

        if (token == TokenAction.Steal)
        {
            if (!_steal.HasCandidates())
            {
                state.RemainingTokens.Remove(token);
                state.ActiveToken = null;
                TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
                var finished = _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
                resolvedWithNoEffectToken = token;
                return finished;
            }

            // Starting a Steal (first-pick, candidates present) always requires a chosen victim; callers
            // must use the explicit-choice entry point (GameSession.TryStartTokenStealWithVictimChoice /
            // TokenPhaseCoordinator.TryStartHandStealWithVictim) instead of the plain ResolveTokenSteal
            // action, same as the CLI and the API/frontend both already do.
            error = "A steal victim must be selected; use the explicit steal-victim API.";
            return false;
        }

        state.RemainingTokens.Remove(token);
        state.ActiveToken = token;

        switch (token)
        {
            case TokenAction.StashTrash:
                state.Step = TokenPhaseStep.StashTrashChooseBranch;
                return true;

            case TokenAction.DoubleStash:
                state.Step = TokenPhaseStep.DoubleStashChoosingCards;
                return true;

            case TokenAction.DoubleTrash:
                return _completion.TryRunDoubleTrashAndFinish(state, out error, out resolvedWithNoEffectToken);

            case TokenAction.Bandit:
                if (!_bandit.StartBandit(state, out error))
                {
                    state.ActiveToken = null;
                    TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
                    var finished = _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
                    resolvedWithNoEffectToken = token;
                    return finished;
                }

                return true;

            case TokenAction.Recycle:
                if (_viewBuilder.GetRecycleOptions(state).Count == 0)
                {
                    state.ActiveToken = null;
                    TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, token);
                    var recycleFinished = _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
                    resolvedWithNoEffectToken = token;
                    return recycleFinished;
                }

                state.Step = TokenPhaseStep.RecycleChoosingReplacement;
                return true;

            default:
                error = "Unsupported token.";
                return false;
        }
    }

    public bool TryStashTrashDraw(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (state.Step != TokenPhaseStep.StashTrashChooseBranch)
        {
            error = "Not resolving StashTrash.";
            return false;
        }

        var drawn = _session.DrawPile.DealCards(1).ToList();
        _session.CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);
        _session.RegisterDrawOutcome(drawn);
        TokenPhaseTokenLogRecording.RecordCardsDrawnPrivately(_session, drawn);
        return _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryStashTrashEnterStashMode(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (state.Step != TokenPhaseStep.StashTrashChooseBranch)
        {
            error = "Not resolving StashTrash.";
            return false;
        }

        if (!_session.CurrentPlayer.Hand.Any(e => _eligibility.CanOfferCardForStashPrompt(e.Card.Name)))
        {
            error = "You have no cards that can be stashed.";
            return false;
        }

        state.Step = TokenPhaseStep.StashTrashPickCard;
        return true;
    }

    public bool TryStashTrashPickCard(int playerIndex, Guid cardId, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (state.Step != TokenPhaseStep.StashTrashPickCard)
        {
            error = "Not choosing a StashTrash stash card.";
            return false;
        }

        if (playerIndex != _session.CurrentPlayerIndex)
        {
            error = "Only the active player may stash.";
            return false;
        }

        if (!_session.CurrentPlayer.TryRemoveFromHandByCardId(cardId, out var card) || card is null)
        {
            error = "Card is not in your hand.";
            return false;
        }

        if (!_eligibility.CanOfferCardForStashPrompt(card.Name))
        {
            error = "That card cannot be stashed.";
            return false;
        }

        _session.CurrentPlayer.AddToStash(card, faceUp: false);
        TokenPhaseTokenLogRecording.RecordCardsStashed(_session, new[] { card }, wasFaceUp: false);
        return _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryDoubleStashSubmit(int playerIndex, IReadOnlyList<Guid> cardIds, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (state.Step != TokenPhaseStep.DoubleStashChoosingCards)
        {
            error = "Not in DoubleStash resolution.";
            return false;
        }

        if (playerIndex != _session.CurrentPlayerIndex)
        {
            error = "Only the active player may resolve DoubleStash.";
            return false;
        }

        if (cardIds.Count > 2)
        {
            error = "DoubleStash allows at most two cards.";
            return false;
        }

        var distinct = cardIds.Distinct().ToList();
        if (distinct.Count != cardIds.Count)
        {
            error = "Duplicate card ids.";
            return false;
        }

        var stashedCards = new List<Card>();
        foreach (var id in cardIds)
        {
            if (!_session.CurrentPlayer.TryRemoveFromHandByCardId(id, out var card) || card is null)
            {
                error = "Each id must refer to a card in your hand.";
                return false;
            }

            if (!_eligibility.CanOfferCardForStashPrompt(card.Name))
            {
                error = "One of the cards cannot be stashed.";
                return false;
            }

            _session.CurrentPlayer.AddToStash(card, faceUp: false);
            stashedCards.Add(card);
        }

        if (stashedCards.Count > 0)
            TokenPhaseTokenLogRecording.RecordCardsStashed(_session, stashedCards, wasFaceUp: false);

        return _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
    }

    /// <summary>
    /// Auto-resolves the Steal token when zero opponents have any card in hand to steal, instead of leaving
    /// it stuck and unresolvable. Re-checks candidates defensively (never trusts the caller) before fizzling.
    /// Used by the API's explicit-choice path (<see cref="GameSession.ApiSupport"/>); the CLI's delegate-driven
    /// path already routes through the same fizzle handling inside <see cref="TryStartToken"/>/the completion engine.
    /// </summary>
    public bool TryResolveStealAutoWithNoTargets(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (_steal.HasCandidates())
        {
            error = "Opponents have cards available to steal; a victim must be selected.";
            return false;
        }

        state.RemainingTokens.Remove(TokenAction.Steal);
        state.ActiveToken = null;
        TokenPhaseTokenLogRecording.RecordTokenResolvedWithNoEffect(_session, TokenAction.Steal);
        var finished = _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out _);
        resolvedWithNoEffectToken = TokenAction.Steal;
        return finished;
    }

    /// <summary>
    /// Begins the Steal token's hand-steal with an already-chosen victim — used for both the first pick
    /// (API/CLI explicit-choice path) and an MmmPie repeat parked in
    /// <see cref="TokenPhaseStep.StealChoosingVictim"/>. Performs the same RemainingTokens/ActiveToken
    /// exhaustion bookkeeping <see cref="TryStartToken"/> does for every other token, so Steal exhausts
    /// correctly regardless of which entry point started it. Never itself fizzles (candidates are required
    /// to already exist), so <paramref name="resolvedWithNoEffectToken"/> is always null.
    /// </summary>
    public bool TryStartHandStealWithVictim(int victimIndex, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (!_steal.StartWithVictim(victimIndex, out error))
            return false;

        state.RemainingTokens.Remove(TokenAction.Steal);
        state.ActiveToken = TokenAction.Steal;
        // Once the steal actually begins, GameSession.State drives the flow (AwaitingStealResponse), not
        // the TokenPhase step; ChoosingNextToken is the correct neutral value here, matching what
        // TryFinishCurrentTokenPassOrRepeat sets once the steal resolves. Harmless on the first-pick path,
        // where the step is already ChoosingNextToken.
        state.Step = TokenPhaseStep.ChoosingNextToken;
        return true;
    }

    public bool TryRecycleReplacementPick(int playerIndex, TokenAction replacement, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        if (state.Step != TokenPhaseStep.RecycleChoosingReplacement)
        {
            error = "Not choosing a Recycle replacement.";
            return false;
        }

        if (playerIndex != _session.CurrentPlayerIndex)
        {
            error = "Only the active player may choose Recycle.";
            return false;
        }

        if (replacement == TokenAction.Recycle)
        {
            error = "Cannot recycle into Recycle.";
            return false;
        }

        if (state.TokensIneligibleForRecycle.Contains(replacement))
        {
            error = "You did not have that token at the start of TokenPhase.";
            return false;
        }

        var opts = _viewBuilder.GetRecycleOptions(state);
        if (!opts.Contains(replacement))
        {
            error = "Invalid replacement token.";
            return false;
        }

        state.RemainingTokens.Add(replacement);
        state.TokensIneligibleForRecycle.Add(replacement);
        return _completion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
    }
}
