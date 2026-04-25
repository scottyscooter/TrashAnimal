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
    public List<Card> Hand { get; } = new();
    public List<StashEntry> StashPile { get; } = new();

    public void AddToStash(Card card, bool faceUp) =>
        StashPile.Add(new StashEntry(card, faceUp));

    public bool TryRemoveFromStashByCardId(Guid cardId, [NotNullWhen(true)] out Card? card)
    {
        var i = StashPile.FindIndex(e => e.Card.Id == cardId);
        if (i < 0)
        {
            card = null;
            return false;
        }

        card = StashPile[i].Card;
        StashPile.RemoveAt(i);
        return true;
    }

    public bool TryRemoveFromHandByCardId(Guid cardId, [NotNullWhen(true)] out Card? card)
    {
        var i = Hand.FindIndex(c => c.Id == cardId);
        if (i < 0)
        {
            card = null;
            return false;
        }

        card = Hand[i];
        Hand.RemoveAt(i);
        return true;
    }

    public bool TryRemoveCard(CardName name, [NotNullWhen(true)] out Card? card)
    {
        var i = Hand.FindIndex(c => c.Name == name);
        if (i < 0)
        {
            card = null;
            return false;
        }

        card = Hand[i];
        Hand.RemoveAt(i);
        return true;
    }

    public void AddCards(IEnumerable<Card> cards) => Hand.AddRange(cards);
}
