using System.Diagnostics.CodeAnalysis;

namespace TrashAnimal;

public sealed class Player
{
    public Player(int index, string? name = null)
    {
        Index = index;
        Name = name ?? $"Player {index + 1}";
    }

    public int Index { get; }
    public string Name { get; }
    public Hand Hand { get; } = new();
    public StashPile StashPile { get; } = new();

    public void AddToStash(Card card, bool faceUp) => StashPile.Add(card, faceUp);

    public bool TryRemoveFromStashByCardId(Guid cardId, [NotNullWhen(true)] out Card? card) =>
        StashPile.TryRemoveByCardId(cardId, out card);

    public bool TryRemoveFromHandByCardId(Guid cardId, [NotNullWhen(true)] out Card? card) =>
        Hand.TryRemoveCard(cardId, out card);

    public bool TryRemoveCard(CardName name, [NotNullWhen(true)] out Card? card) =>
        Hand.TryRemoveCard(name, out card);

    /// <param name="markReceivedOnOwnerCurrentTurn">True when this player is the current turn holder and the draw should count as received on this turn.</param>
    public void AddCards(IEnumerable<Card> cards, bool markReceivedOnOwnerCurrentTurn = false) =>
        Hand.AddRange(cards, markReceivedOnOwnerCurrentTurn);
}
