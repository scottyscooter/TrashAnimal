namespace TrashAnimal;

public interface IPlayerController
{
    string DisplayName { get; }

    GameAction ChooseAction(GameView view, IReadOnlyList<GameAction> allowedActions);

    /// <summary>Only called when <see cref="GameState.AwaitingYumYum"/> and this player is the responder.</summary>
    bool ChoosePlayYumYum(GameView view);

    /// <summary>Only called when the player plays Feesh and a discard-card choice is required.</summary>
    Card? ChooseFeeshCard(GameView view, IReadOnlyList<Card> discardPile);
}

