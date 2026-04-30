namespace TrashAnimal.TokenPhase;

internal sealed class TokenPhaseBanditHandler
{
    private readonly GameSession _session;
    private readonly TokenPhaseCardEligibility _eligibility;
    private readonly ITokenPhaseTokenCompletion _tokenCompletion;

    public TokenPhaseBanditHandler(
        GameSession session,
        TokenPhaseCardEligibility eligibility,
        ITokenPhaseTokenCompletion tokenCompletion)
    {
        _session = session;
        _eligibility = eligibility;
        _tokenCompletion = tokenCompletion;
    }

    public static int? GetCurrentResponderIndex(TokenPhaseState state)
    {
        if (state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
            return null;
        if (state.BanditOpponentOrder.Count == 0 || state.BanditOpponentIndexInOrder >= state.BanditOpponentOrder.Count)
            return null;
        return state.BanditOpponentOrder[state.BanditOpponentIndexInOrder];
    }

    public bool TryBanditPass(int opponentIndex, TokenPhaseState state, out string? error)
    {
        error = null;
        if (state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Not awaiting a Bandit response.";
            return false;
        }

        if (GetCurrentResponderIndex(state) != opponentIndex)
        {
            error = "Only the current Bandit responder may act.";
            return false;
        }

        AdvanceBanditWindow(state);
        return true;
    }

    public bool TryBanditStashMatchingCard(int opponentIndex, Guid cardId, TokenPhaseState state, out string? error)
    {
        error = null;
        if (state.Step != TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            error = "Not awaiting a Bandit response.";
            return false;
        }

        if (GetCurrentResponderIndex(state) != opponentIndex)
        {
            error = "Only the current Bandit responder may stash.";
            return false;
        }

        var revealed = state.BanditRevealedName;
        if (revealed is null)
        {
            error = "Bandit reveal is missing.";
            return false;
        }

        var opponent = _session.Players[opponentIndex];
        if (!opponent.TryRemoveFromHandByCardId(cardId, out var card) || card is null)
        {
            error = "Card is not in that player's hand.";
            return false;
        }

        if (card.Name != revealed.Value)
        {
            error = "Stashed card must match the revealed Bandit card.";
            return false;
        }

        if (!_eligibility.CanOfferCardForStashPrompt(card.Name))
        {
            error = "That card cannot be stashed.";
            return false;
        }

        opponent.AddToStash(card, faceUp: true);

        var drawn = _session.DrawPile.DealCards(1).ToList();
        _session.CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);

        AdvanceBanditWindow(state);
        return true;
    }

    public bool StartBandit(TokenPhaseState state, out string? error)
    {
        error = null;
        var drawn = _session.DrawPile.DealCards(1).ToList();
        if (drawn.Count == 0)
        {
            error = "Deck is empty.";
            return false;
        }

        var card = drawn[0];
        state.BanditRevealedName = card.Name;
        _session.CurrentPlayer.AddCards(new[] { card }, markReceivedOnOwnerCurrentTurn: true);

        var order = new List<int>();
        foreach (var idx in _session.EnumerateOpponentIndicesClockwise())
            order.Add(idx);

        state.BanditOpponentOrder = order;
        state.BanditOpponentIndexInOrder = 0;
        state.Step = TokenPhaseStep.BanditAwaitOpponentResponse;
        return true;
    }

    private void AdvanceBanditWindow(TokenPhaseState state)
    {
        state.BanditOpponentIndexInOrder++;
        if (state.BanditOpponentIndexInOrder >= state.BanditOpponentOrder.Count)
            FinishBanditToken(state);
    }

    private void FinishBanditToken(TokenPhaseState state)
    {
        state.ResetBanditWindow();
        _ = _tokenCompletion.TryFinishCurrentTokenPassOrRepeat(state, out _);
    }
}
