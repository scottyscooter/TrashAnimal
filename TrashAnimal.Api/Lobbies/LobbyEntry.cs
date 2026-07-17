namespace TrashAnimal.Api.Lobbies;

/// <summary>
/// In-memory lobby state: the seats claimed so far, plus whether/into-what-game it has started.
/// Mirrors <see cref="TrashAnimal.Api.Sessions.GameSessionEntry"/>'s locking discipline — all
/// mutable state is exposed only via private setters/methods, and callers (<see
/// cref="TrashAnimal.Api.Application.LobbyApplicationService"/>) must hold <see cref="Lock"/> for
/// the full read-check-mutate sequence of any join/start operation to avoid two joins racing the
/// seat limit/nickname-uniqueness check, or a double-start creating two games.
/// </summary>
public sealed class LobbyEntry
{
    public const int MaxSeats = 4;

    private readonly List<LobbySeat> _seats = new();

    public LobbyEntry(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }
    public IReadOnlyList<LobbySeat> Seats => _seats;
    public int Revision { get; private set; }
    public bool IsStarted { get; private set; }
    public Guid? GameId { get; private set; }

    /// <summary>
    /// Per-lobby mutual exclusion lock. Callers must hold this for the entire read-check-mutate
    /// sequence of <c>Join</c>/<c>Start</c> operations — see the type-level remarks.
    /// </summary>
    public SemaphoreSlim Lock { get; } = new SemaphoreSlim(1, 1);

    public bool HasNickname(string nickname) =>
        _seats.Any(s => string.Equals(s.Nickname, nickname, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a new seat at the next available index and bumps the revision.</summary>
    public LobbySeat AddSeat(string nickname)
    {
        var seat = new LobbySeat(_seats.Count, nickname, Guid.NewGuid().ToString("N"));
        _seats.Add(seat);
        IncrementRevision();
        return seat;
    }

    /// <summary>Marks this lobby started with the created game's id and bumps the revision.</summary>
    public void MarkStarted(Guid gameId)
    {
        IsStarted = true;
        GameId = gameId;
        IncrementRevision();
    }

    public int IncrementRevision() => ++Revision;
}
