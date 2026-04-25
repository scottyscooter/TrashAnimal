namespace TrashAnimal;

public interface IPlayerController
{
    string DisplayName { get; }

    GameAction ChooseAction(GameView view, IReadOnlyList<GameAction> allowedActions);

    /// <summary>Only called when <see cref="GameState.AwaitingYumYum"/> and this player is the responder.</summary>
    bool ChoosePlayYumYum(GameView view);

    /// <summary>Only called when the player plays Feesh and a discard-card choice is required.</summary>
    Card? ChooseFeeshCard(GameView view, IReadOnlyList<Card> discardPile);

    /// <summary>Called when the active player plays Shiny; pick which opponent's stash to steal from. Only called when <paramref name="opponentIndicesWithNonEmptyStash"/> is non-empty.</summary>
    int ChooseShinyStealVictim(GameView view, IReadOnlyList<int> opponentIndicesWithNonEmptyStash);

    /// <summary>Called when the thief must pick a card after the victim passes on a steal attempt. Only called when <paramref name="slots"/> is non-empty.</summary>
    Guid ChooseStealCard(GameView view, IReadOnlyList<StealPickSlot> slots);
}

