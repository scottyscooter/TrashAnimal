namespace TrashAnimal;

public sealed class HandEntry
{
    public HandEntry(Card card, bool newlyAdded)
    {
        Card = card;
        NewlyAdded = newlyAdded;
    }

    public Card Card { get; }

    /// <summary>
    /// Tracks whether the player received this card on their current turn (the whole turn, not only RollPhase).
    /// Cleared for every entry in that player's hand at the start of their phase 1 (RollPhase).
    /// </summary>
    public bool NewlyAdded { get; set; }
}
