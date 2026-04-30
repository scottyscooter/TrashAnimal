namespace TrashAnimal;

public sealed class AiController : IPlayerController
{
    private readonly Random _rng;

    public AiController(string displayName, Random? rng = null)
    {
        DisplayName = displayName;
        _rng = rng ?? Random.Shared;
    }

    public string DisplayName { get; }

    public GameAction ChooseAction(GameView view, IReadOnlyList<GameAction> allowedActions)
    {
        // Extremely simple bot: if it can stop safely, it sometimes stops. Otherwise random legal.
        if (allowedActions.Contains(GameAction.StopRolling) && !view.ForcedRollRemaining && !view.IsBusted)
        {
            var stopChance = view.PhaseOneTokens.Count >= 3 ? 0.7 : 0.3;
            if (_rng.NextDouble() < stopChance)
                return GameAction.StopRolling;
        }

        return allowedActions[_rng.Next(allowedActions.Count)];
    }

    public bool ChoosePlayYumYum(GameView view)
    {
        // Simple: 30% chance to play if possible.
        return _rng.NextDouble() < 0.3;
    }

    public Card? ChooseFeeshCard(GameView view, IReadOnlyList<Card> discardPile)
    {
        if (discardPile.Count == 0)
            return null;

        return discardPile[_rng.Next(discardPile.Count)];
    }

    public int ChooseShinyStealVictim(GameView view, IReadOnlyList<int> opponentIndicesWithNonEmptyStash)
    {
        ArgumentOutOfRangeException.ThrowIfZero(opponentIndicesWithNonEmptyStash.Count);
        return opponentIndicesWithNonEmptyStash[_rng.Next(opponentIndicesWithNonEmptyStash.Count)];
    }

    public Guid ChooseStealCard(GameView view, IReadOnlyList<StealPickSlot> slots)
    {
        ArgumentOutOfRangeException.ThrowIfZero(slots.Count);
        return slots[_rng.Next(slots.Count)].CardId;
    }

    public void ChooseBanditResponse(GameView view, out bool stash, out Guid? cardId)
    {
        stash = false;
        cardId = null;
        var tp = view.TokenPhase;
        if (tp?.StashableHandCardsForCurrentPrompt is not { Count: > 0 } stashable)
            return;

        if (_rng.NextDouble() < 0.4)
            return;

        stash = true;
        cardId = stashable[_rng.Next(stashable.Count)].CardId;
    }

    public IReadOnlyList<Guid> ChooseDoubleStashCardIds(GameView view, IReadOnlyList<(Guid Id, CardName Name)> stashable)
    {
        var count = stashable.Count == 0 ? 0 : _rng.Next(3);
        if (count == 0)
            return Array.Empty<Guid>();

        return stashable.OrderBy(_ => _rng.Next()).Take(count).Select(s => s.Id).ToList();
    }

    public Guid ChooseStashTrashStashCard(GameView view, IReadOnlyList<(Guid Id, CardName Name)> stashable)
    {
        ArgumentOutOfRangeException.ThrowIfZero(stashable.Count);
        return stashable[_rng.Next(stashable.Count)].Id;
    }

    public TokenAction ChooseRecycleReplacement(GameView view, IReadOnlyList<TokenAction> options)
    {
        ArgumentOutOfRangeException.ThrowIfZero(options.Count);
        return options[_rng.Next(options.Count)];
    }
}

