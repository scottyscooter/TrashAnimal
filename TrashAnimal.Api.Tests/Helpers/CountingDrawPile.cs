namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// A minimal draw pile backed by a fixed-size list of identical cards. Suitable for integration
/// tests that need a controllable deck without depending on the real <see cref="Deck"/> shuffling.
/// </summary>
internal sealed class CountingDrawPile : IDrawPile
{
    private readonly List<Card> _stock;

    internal CountingDrawPile(int count, CardName name = CardName.Nanners)
    {
        _stock = Enumerable.Range(0, count).Select(_ => new Card(name)).ToList();
    }

    public int GetDeckCount() => _stock.Count;

    public IEnumerable<Card> DealCards(int count)
    {
        if (count <= 0)
            yield break;

        var available = Math.Min(count, _stock.Count);
        for (var i = 0; i < available; i++)
        {
            var card = _stock[0];
            _stock.RemoveAt(0);
            yield return card;
        }
    }
}
