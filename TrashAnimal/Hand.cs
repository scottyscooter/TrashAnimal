using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TrashAnimal;

public sealed class Hand : IReadOnlyList<HandEntry>
{
    private readonly List<HandEntry> _entries = new();

    public int Count => _entries.Count;

    public HandEntry this[int index] => _entries[index];

    public void Add(Card card, bool newlyAdded = false) => _entries.Add(new HandEntry(card, newlyAdded));

    public void AddRange(IEnumerable<Card> cards, bool newlyAdded = false)
    {
        foreach (var card in cards)
            _entries.Add(new HandEntry(card, newlyAdded));
    }

    public void Clear() => _entries.Clear();

    /// <summary>Clears <see cref="HandEntry.NewlyAdded"/> on every entry (call when this player's phase 1 begins).</summary>
    public void ClearNewlyAddedFlags()
    {
        foreach (var entry in _entries)
            entry.NewlyAdded = false;
    }

    public bool TryRemoveCard(Guid cardId, [NotNullWhen(true)] out Card? card)
    {
        var index = _entries.FindIndex(e => e.Card.Id == cardId);
        if (index < 0)
        {
            card = null;
            return false;
        }

        card = _entries[index].Card;
        _entries.RemoveAt(index);
        return true;
    }

    public bool TryRemoveCard(CardName name, [NotNullWhen(true)] out Card? card)
    {
        var index = _entries.FindIndex(e => e.Card.Name == name);
        if (index < 0)
        {
            card = null;
            return false;
        }

        card = _entries[index].Card;
        _entries.RemoveAt(index);
        return true;
    }

    public IEnumerator<HandEntry> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
