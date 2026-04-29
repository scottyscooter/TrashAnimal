using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace TrashAnimal;

public sealed class StashPile : IReadOnlyList<StashEntry>
{
    private readonly List<StashEntry> _entries = new();

    public int Count => _entries.Count;

    public StashEntry this[int index] => _entries[index];

    public void Add(Card card, bool faceUp) =>
        _entries.Add(new StashEntry(card, faceUp));

    public void Clear() => _entries.Clear();

    public bool TryRemoveByCardId(Guid cardId, [NotNullWhen(true)] out Card? card)
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

    public IEnumerator<StashEntry> GetEnumerator() => _entries.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
