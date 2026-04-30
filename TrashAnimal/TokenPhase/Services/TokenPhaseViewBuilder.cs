namespace TrashAnimal.TokenPhase;

internal sealed class TokenPhaseViewBuilder
{
    private readonly GameSession _session;
    private readonly TokenPhaseCardEligibility _eligibility;

    public TokenPhaseViewBuilder(GameSession session, TokenPhaseCardEligibility eligibility)
    {
        _session = session;
        _eligibility = eligibility;
    }

    public TokenPhaseView BuildView(TokenPhaseState? state, int viewPlayerIndex)
    {
        if (state is null)
            return new TokenPhaseView(
                TokenPhaseStep.ChoosingNextToken,
                Array.Empty<TokenAction>(),
                null,
                null,
                null,
                Array.Empty<(Guid, CardName)>(),
                Array.Empty<TokenAction>());

        var remaining = state.RemainingTokens.OrderBy(t => t).ToList();
        var stashPrompt = GetStashableHandTuplesForView(state, viewPlayerIndex);
        var recycleOpts = GetRecycleOptions(state);
        var banditResponder = TokenPhaseBanditHandler.GetCurrentResponderIndex(state);

        return new TokenPhaseView(
            state.Step,
            remaining,
            state.ActiveToken,
            state.BanditRevealedName,
            banditResponder,
            stashPrompt,
            recycleOpts);
    }

    public IReadOnlyList<TokenAction> GetRecycleOptions(TokenPhaseState state)
    {
        var list = new List<TokenAction>();
        foreach (var value in Enum.GetValues<TokenAction>())
        {
            if (value == TokenAction.Recycle)
                continue;
            if (state.InitialTokensSnapshot.Contains(value))
                continue;
            list.Add(value);
        }

        return list.OrderBy(t => t).ToList();
    }

    private IReadOnlyList<(Guid CardId, CardName Name)> GetStashableHandTuplesForView(TokenPhaseState state, int viewPlayerIndex)
    {
        if (state.Step == TokenPhaseStep.BanditAwaitOpponentResponse)
        {
            var idx = TokenPhaseBanditHandler.GetCurrentResponderIndex(state);
            if (idx != viewPlayerIndex || state.BanditRevealedName is null)
                return Array.Empty<(Guid, CardName)>();

            return _session.Players[viewPlayerIndex].Hand
                .Where(e => e.Card.Name == state.BanditRevealedName && _eligibility.CanOfferCardForStashPrompt(e.Card.Name))
                .Select(e => (e.Card.Id, e.Card.Name))
                .ToList();
        }

        if (viewPlayerIndex != _session.CurrentPlayerIndex)
            return Array.Empty<(Guid, CardName)>();

        if (state.Step is not (TokenPhaseStep.StashTrashChooseBranch or TokenPhaseStep.StashTrashPickCard or TokenPhaseStep.DoubleStashChoosingCards))
            return Array.Empty<(Guid, CardName)>();

        return _session.CurrentPlayer.Hand
            .Where(e => _eligibility.CanOfferCardForStashPrompt(e.Card.Name))
            .Select(e => (e.Card.Id, e.Card.Name))
            .ToList();
    }
}
