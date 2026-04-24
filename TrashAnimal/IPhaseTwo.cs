namespace TrashAnimal;

/// <summary>Placeholder for TokenPhase resolution (tokens, actions, scoring).</summary>
public interface IPhaseTwo
{
    void ResolvePhaseTwo(int playerIndex, IReadOnlyList<TokenAction> tokens);
}
