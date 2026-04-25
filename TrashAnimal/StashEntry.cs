namespace TrashAnimal;

public sealed class StashEntry
{
    public StashEntry(Card card, bool isFaceUp)
    {
        Card = card;
        IsFaceUp = isFaceUp;
    }

    public Card Card { get; }
    public bool IsFaceUp { get; }
}
