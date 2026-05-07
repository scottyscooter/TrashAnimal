namespace TrashAnimal.Api.Sessions;

public sealed class GameSessionEntry
{
    public GameSessionEntry(GameSession session, Die die)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Die = die ?? throw new ArgumentNullException(nameof(die));
    }

    public GameSession Session { get; }
    public Die Die { get; }
    public int Revision { get; private set; }

    /// <summary>
    /// Per-session mutual exclusion lock. All engine mutations must be performed while
    /// holding this semaphore to prevent concurrent modification of a non-thread-safe
    /// <see cref="GameSession"/>.
    /// </summary>
    public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);

    public int IncrementRevision() => ++Revision;
}
