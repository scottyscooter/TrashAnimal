using Moq;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// Builds a <see cref="Mock{T}"/> of <see cref="Die"/> that hands back tokens from a predefined
/// sequence, then falls back to <see cref="TokenAction.StashTrash"/> once the sequence is
/// exhausted. Used by integration tests that need predictable die outcomes without relying on a
/// seeded PRNG.
/// </summary>
internal static class DieMockFactory
{
    internal static Mock<Die> CreateSequenced(params TokenAction[] sequence)
    {
        var remainingRolls = new Queue<TokenAction>(sequence);
        var dieMock = new Mock<Die>(Random.Shared);
        dieMock.Setup(die => die.Roll())
            .Returns(() => remainingRolls.Count > 0 ? remainingRolls.Dequeue() : TokenAction.StashTrash);
        return dieMock;
    }
}
