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
}

