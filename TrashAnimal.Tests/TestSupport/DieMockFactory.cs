using Moq;

namespace TrashAnimal.Tests.TestSupport;

/// <summary>
/// Builds a <see cref="Mock{T}"/> of <see cref="Die"/> that hands back tokens from a predefined
/// sequence, then falls back to <see cref="TokenAction.StashTrash"/> once the sequence is
/// exhausted. Used by tests that need deterministic roll outcomes without relying on a seeded
/// PRNG or a hand-written <c>Die</c> subclass.
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
