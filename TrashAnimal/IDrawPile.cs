namespace TrashAnimal;

public interface IDrawPile
{
    /// <summary>Number of cards currently in the pile (updates as <see cref="DealCards"/> removes cards).</summary>
    int GetDeckCount();

    /// <summary>Draws up to <paramref name="count"/> cards from the top; may return fewer if the pile is exhausted.</summary>
    IEnumerable<Card> DealCards(int count);
}
