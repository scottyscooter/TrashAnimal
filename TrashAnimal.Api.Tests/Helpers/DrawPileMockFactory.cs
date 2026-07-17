using Moq;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// Builds a <see cref="Mock{T}"/> of <see cref="IDrawPile"/> backed by a controllable, in-memory
/// stock of identical cards. Used by integration tests that need a deterministic deck size
/// without depending on the real <see cref="Deck"/>'s shuffling.
/// </summary>
internal static class DrawPileMockFactory
{
    internal static Mock<IDrawPile> CreateWithCards(int count, CardName name = CardName.Nanners)
    {
        var stock = Enumerable.Range(0, count).Select(_ => new Card(name)).ToList();
        var drawPileMock = new Mock<IDrawPile>();

        drawPileMock.Setup(pile => pile.GetDeckCount()).Returns(() => stock.Count);
        drawPileMock.Setup(pile => pile.DealCards(It.IsAny<int>()))
            .Returns((int requestedCount) =>
            {
                if (requestedCount <= 0)
                    return [];

                var dealt = stock.Take(Math.Min(requestedCount, stock.Count)).ToList();
                stock.RemoveRange(0, dealt.Count);
                return dealt;
            });

        return drawPileMock;
    }
}
