using TrashAnimal.GameLog;

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

    public bool TryBanditPass(int opponentIndex, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
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

        return AdvanceBanditWindow(state, out error, out resolvedWithNoEffectToken);
    }

    public bool TryBanditStashMatchingCard(int opponentIndex, Guid cardId, TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
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
        _session.RecordLogEvent(new CardStashedEvent(
            0, _session.TurnNumber, opponentIndex, new[] { card.Id }, new[] { card.Name }, WasFaceUp: true));

        var drawn = _session.DrawPile.DealCards(1).ToList();
        _session.CurrentPlayer.AddCards(drawn, markReceivedOnOwnerCurrentTurn: true);
        _session.RegisterDrawOutcome(drawn);
        if (drawn.Count > 0)
        {
            _session.RecordLogEvent(new CardDrawnPrivatelyEvent(
                0,
                _session.TurnNumber,
                _session.CurrentPlayerIndex,
                drawn.Select(c => c.Id).ToList(),
                drawn.Select(c => c.Name).ToList()));
        }

        return AdvanceBanditWindow(state, out error, out resolvedWithNoEffectToken);
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
        _session.RegisterDrawOutcome(drawn);
        _session.RecordLogEvent(new CardDrawnFaceUpEvent(0, _session.TurnNumber, _session.CurrentPlayerIndex, card.Name));

        var order = new List<int>();
        foreach (var idx in _session.EnumerateOpponentIndicesClockwise())
            order.Add(idx);

        state.BanditOpponentOrder = order;
        state.BanditOpponentIndexInOrder = 0;
        state.Step = TokenPhaseStep.BanditAwaitOpponentResponse;

        var initialResponder = GetCurrentResponderIndex(state);
        if (initialResponder.HasValue)
            _session.RecordLogEvent(new BanditResponseWindowAdvancedEvent(
                0, _session.TurnNumber, _session.CurrentPlayerIndex, initialResponder.Value, card.Name));

        return true;
    }

    private bool AdvanceBanditWindow(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        error = null;
        resolvedWithNoEffectToken = null;
        state.BanditOpponentIndexInOrder++;
        if (state.BanditOpponentIndexInOrder >= state.BanditOpponentOrder.Count)
            return FinishBanditToken(state, out error, out resolvedWithNoEffectToken);

        var nextResponder = GetCurrentResponderIndex(state);
        if (nextResponder.HasValue && state.BanditRevealedName.HasValue)
            _session.RecordLogEvent(new BanditResponseWindowAdvancedEvent(
                0, _session.TurnNumber, _session.CurrentPlayerIndex, nextResponder.Value, state.BanditRevealedName.Value));

        return true;
    }

    private bool FinishBanditToken(TokenPhaseState state, out string? error, out TokenAction? resolvedWithNoEffectToken)
    {
        state.ResetBanditWindow();
        return _tokenCompletion.TryFinishCurrentTokenPassOrRepeat(state, out error, out resolvedWithNoEffectToken);
    }
}
