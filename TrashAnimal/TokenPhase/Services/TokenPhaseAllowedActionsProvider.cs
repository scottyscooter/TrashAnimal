namespace TrashAnimal.TokenPhase;

internal sealed class TokenPhaseAllowedActionsProvider
{
    private readonly GameSession _session;
    private readonly TokenPhaseInterruptCardPlay _interruptCards;

    public TokenPhaseAllowedActionsProvider(GameSession session, TokenPhaseInterruptCardPlay interruptCards)
    {
        _session = session;
        _interruptCards = interruptCards;
    }

    public IReadOnlyList<GameAction> GetAllowedActions(TokenPhaseState state, int playerIndex)
    {
        if (state.Step == TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            var responder = TokenPhaseBanditHandler.GetCurrentResponderIndex(state);
            if (responder != playerIndex)
                return Array.Empty<GameAction>();

            return new[] { GameAction.TokenBanditMatchPass };
        }

        if (playerIndex != _session.CurrentPlayerIndex)
            return Array.Empty<GameAction>();

        var actions = new List<GameAction>();

        switch (state.Step)
        {
            case TokenPhaseStep.ChoosingNextToken:
                if (_interruptCards.CanPlayMmmPie(state))
                    actions.Add(GameAction.PlayMmmPieTokenPhase);
                if (_interruptCards.CanPlayShinyTokenPhase(state))
                    actions.Add(GameAction.PlayShinyTokenPhase);
                if (_interruptCards.CanPlayFeeshTokenPhase(state))
                    actions.Add(GameAction.PlayFeeshTokenPhase);

                foreach (var t in state.RemainingTokens.OrderBy(x => x))
                    actions.Add(TokenPhaseGameActionMapping.ToResolveGameAction(t));

                break;

            case TokenPhaseStep.StashTrashChooseBranch:
                actions.Add(GameAction.TokenStashTrashDrawOne);
                actions.Add(GameAction.TokenStashTrashStashMode);
                if (_interruptCards.CanPlayMmmPie(state))
                    actions.Add(GameAction.PlayMmmPieTokenPhase);
                if (_interruptCards.CanPlayShinyTokenPhase(state))
                    actions.Add(GameAction.PlayShinyTokenPhase);
                if (_interruptCards.CanPlayFeeshTokenPhase(state))
                    actions.Add(GameAction.PlayFeeshTokenPhase);
                break;

            case TokenPhaseStep.StashTrashPickCard:
                if (_interruptCards.CanPlayMmmPie(state))
                    actions.Add(GameAction.PlayMmmPieTokenPhase);
                if (_interruptCards.CanPlayShinyTokenPhase(state))
                    actions.Add(GameAction.PlayShinyTokenPhase);
                if (_interruptCards.CanPlayFeeshTokenPhase(state))
                    actions.Add(GameAction.PlayFeeshTokenPhase);
                break;

            case TokenPhaseStep.DoubleStashChoosingCards:
                actions.Add(GameAction.TokenDoubleStashSubmit);
                if (_interruptCards.CanPlayMmmPie(state))
                    actions.Add(GameAction.PlayMmmPieTokenPhase);
                if (_interruptCards.CanPlayShinyTokenPhase(state))
                    actions.Add(GameAction.PlayShinyTokenPhase);
                if (_interruptCards.CanPlayFeeshTokenPhase(state))
                    actions.Add(GameAction.PlayFeeshTokenPhase);
                break;

            case TokenPhaseStep.RecycleChoosingReplacement:
                break;
        }

        return actions;
    }
}
