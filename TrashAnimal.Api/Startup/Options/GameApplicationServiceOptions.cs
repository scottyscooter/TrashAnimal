namespace TrashAnimal.Api.Startup.Options;

public sealed class GameApplicationServiceOptions
{
    /// <summary>
    /// Number of cards dealt to each player seat at game start, indexed by seat position (0-based).
    /// Must contain exactly one entry per supported player count (2–4 players).
    /// </summary>
    public IEnumerable<int> StartingHandCounts { get; set; } = [3, 4, 5, 6];
}
