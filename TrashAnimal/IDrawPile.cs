namespace TrashAnimal;

public interface IDrawPile
{
    /// <summary>Draws up to <paramref name="count"/> cards from the top; may return fewer if the pile is exhausted.</summary>
    IEnumerable<Card> DealCards(int count);
}
