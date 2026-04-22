namespace TrashAnimal;

public sealed class PhaseTwoNoop : IPhaseTwo
{
    public void ResolvePhaseTwo(int playerIndex, IReadOnlyList<TokenAction> tokens)
    {
        // Intentionally no-op for scaffolding.
        Console.WriteLine($"Phase 2 resolved for player {playerIndex} with tokens: {string.Join(", ", tokens)}");
    }
}

