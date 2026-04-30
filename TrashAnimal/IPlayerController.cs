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

    /// <summary>Bandit: pass (stash=false) or stash one matching card (stash=true, cardId set).</summary>
    void ChooseBanditResponse(GameView view, out bool stash, out Guid? cardId);

    /// <summary>DoubleStash: 0–2 stash-eligible cards from hand.</summary>
    IReadOnlyList<Guid> ChooseDoubleStashCardIds(GameView view, IReadOnlyList<(Guid Id, CardName Name)> stashable);

    /// <summary>StashTrash stash branch: one stash-eligible card from hand.</summary>
    Guid ChooseStashTrashStashCard(GameView view, IReadOnlyList<(Guid Id, CardName Name)> stashable);

    /// <summary>Recycle token: pick a replacement token type.</summary>
    TokenAction ChooseRecycleReplacement(GameView view, IReadOnlyList<TokenAction> options);
}

