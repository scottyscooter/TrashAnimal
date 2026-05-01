namespace TrashAnimal.TokenPhase;

internal sealed class TokenPhaseTokenResolver : ITokenPhaseTokenCompletion
{
    private readonly GameSession _session;
    private readonly TokenPhaseCardEligibility _eligibility;
    private readonly TokenPhaseViewBuilder _viewBuilder;
    private readonly TokenPhaseBanditHandler _bandit;

    public TokenPhaseTokenResolver(
        GameSession session,
        TokenPhaseCardEligibility eligibility,
        TokenPhaseViewBuilder viewBuilder)
    {
        _session = session;
        _eligibility = eligibility;
        _viewBuilder = viewBuilder;
        _bandit = new TokenPhaseBanditHandler(session, eligibility, this);
    }

    internal TokenPhaseBanditHandler BanditHandler => _bandit;

    public bool TryFinishCurrentTokenPassOrRepeat(TokenPhaseState state, out string? error) =>
        FinishCurrentTokenPassOrRepeat(state, out error);

    public bool TryStartToken(TokenAction token, TokenPhaseState state, out string? error)
    {
        error = null;
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

        if (token == TokenAction.Steal)
        {
            if (!StartHandSteal(out error))
                return false;

            state.RemainingTokens.Remove(token);
            state.ActiveToken = token;
            return true;
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
                RunDoubleTrashDraws();
                return FinishCurrentTokenPassOrRepeat(state, out error);

            case TokenAction.Bandit:
                if (!_bandit.StartBandit(state, out error))
                {
                    state.RemainingTokens.Add(token);
                    state.ActiveToken = null;
                    return false;
                }

                return true;

            case TokenAction.Recycle:
                state.Step = TokenPhaseStep.RecycleChoosingReplacement;
                return true;

            default:
                error = "Unsupported token.";
                return false;
        }
    }

    public bool TryStashTrashDraw(TokenPhaseState state, out string? error)
    {
        error = null;
        if (state.Step != TokenPhaseStep.StashTrashChooseBranch)
        {
            error = "Not resolving StashTrash.";
            return false;
        }

        var drawn = _session.DrawPile.DealCards(1).ToList();
        _session.CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);
        _session.RegisterDrawOutcome(drawn);
        return FinishCurrentTokenPassOrRepeat(state, out error);
    }

    public bool TryStashTrashEnterStashMode(TokenPhaseState state, out string? error)
    {
        error = null;
        if (state.Step != TokenPhaseStep.StashTrashChooseBranch)
        {
            error = "Not resolving StashTrash.";
            return false;
        }

        state.Step = TokenPhaseStep.StashTrashPickCard;
        return true;
    }

    public bool TryStashTrashPickCard(int playerIndex, Guid cardId, TokenPhaseState state, out string? error)
    {
        error = null;
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
        return FinishCurrentTokenPassOrRepeat(state, out error);
    }

    public bool TryDoubleStashSubmit(int playerIndex, IReadOnlyList<Guid> cardIds, TokenPhaseState state, out string? error)
    {
        error = null;
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
        }

        return FinishCurrentTokenPassOrRepeat(state, out error);
    }

    public bool TryRecycleReplacementPick(int playerIndex, TokenAction replacement, TokenPhaseState state, out string? error)
    {
        error = null;
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

        if (state.InitialTokensSnapshot.Contains(replacement))
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
        return FinishCurrentTokenPassOrRepeat(state, out error);
    }

    private void RunDoubleTrashDraws()
    {
        var drawn = _session.DrawPile.DealCards(2).ToList();
        _session.CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);
        _session.RegisterDrawOutcome(drawn);
    }

    private bool StartHandSteal(out string? error)
    {
        error = null;
        if (_session.ChooseTokenHandStealVictim is null)
        {
            error = "No token-steal victim selector configured.";
            return false;
        }

        var candidates = OpponentIndicesWithNonEmptyHand(_session.Players, _session.CurrentPlayerIndex).ToList();
        if (candidates.Count == 0)
        {
            error = "No opponent has a card in hand to steal.";
            return false;
        }

        var victimIndex = _session.ChooseTokenHandStealVictim(_session.CurrentPlayerIndex, candidates);
        if (!candidates.Contains(victimIndex))
        {
            error = "Token steal victim selection is invalid.";
            return false;
        }

        if (_session.Players[victimIndex].Hand.Count == 0)
        {
            error = "Selected victim has an empty hand.";
            return false;
        }

        _session.Steal.Begin(_session.CurrentPlayerIndex, victimIndex, StealTargetZone.Hand);
        _session.ArmStealResumeState(GameState.TokenPhase);
        _session.SetGameState(GameState.AwaitingStealResponse);
        return true;
    }

    private static IEnumerable<int> OpponentIndicesWithNonEmptyHand(IReadOnlyList<Player> players, int thiefIndex)
    {
        for (var i = 0; i < players.Count; i++)
        {
            if (i == thiefIndex)
                continue;
            if (players[i].Hand.Count > 0)
                yield return i;
        }
    }

    private bool FinishCurrentTokenPassOrRepeat(TokenPhaseState state, out string? error)
    {
        error = null;
        if (state.ResolveTokenTwice && state.ActiveToken is { } activeForRepeat)
        {
            state.ResolveTokenTwice = false;
            RestartSubflow(activeForRepeat, state, out error);
            return error is null;
        }

        state.ActiveToken = null;
        state.Step = TokenPhaseStep.ChoosingNextToken;
        state.ResetBanditWindow();

        if (state.RemainingTokens.Count == 0)
            _session.CompleteTokenPhaseAndEndTurn();

        return true;
    }

    private bool RestartSubflow(TokenAction token, TokenPhaseState state, out string? error)
    {
        error = null;
        state.ResetBanditWindow();

        switch (token)
        {
            case TokenAction.StashTrash:
                state.Step = TokenPhaseStep.StashTrashChooseBranch;
                return true;

            case TokenAction.DoubleStash:
                state.Step = TokenPhaseStep.DoubleStashChoosingCards;
                return true;

            case TokenAction.DoubleTrash:
                RunDoubleTrashDraws();
                return FinishCurrentTokenPassOrRepeat(state, out error);

            case TokenAction.Bandit:
                return _bandit.StartBandit(state, out error);

            case TokenAction.Steal:
                return StartHandSteal(out error);

            case TokenAction.Recycle:
                state.Step = TokenPhaseStep.RecycleChoosingReplacement;
                return true;

            default:
                error = "Unsupported token for repeat.";
                return false;
        }
    }
}
