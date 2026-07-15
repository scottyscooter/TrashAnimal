namespace TrashAnimal.Api.Startup.Options;

public sealed class GameApplicationServiceOptions
{
    /// <summary>
    /// Number of cards dealt to each player seat at game start, indexed by seat position (0-based).
    /// Must contain exactly one entry per supported player count (2–4 players).
    /// No default here — configuration binding appends to (rather than replaces) a non-empty
    /// default collection, so the real default lives in appsettings.json.
    /// </summary>
    public int[] StartingHandCounts { get; set; } = [];
}
