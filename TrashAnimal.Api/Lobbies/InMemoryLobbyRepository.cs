using System.Collections.Concurrent;

namespace TrashAnimal.Api.Lobbies;

/// <summary>
/// In-memory lobby store, mirroring <see
/// cref="TrashAnimal.Api.Sessions.InMemoryGameSessionRepository"/>. Like that repository, entries
/// are never evicted — this is a conscious carry-forward of the same accepted limitation, not an
/// oversight; see <c>TrashAnimal.Api/CLAUDE.md</c>'s Lobbies section for the rationale (lobbies
/// abandoned pre-start simply accumulate for the lifetime of the process, same as finished games).
/// </summary>
public sealed class InMemoryLobbyRepository : ILobbyRepository
{
    private readonly ConcurrentDictionary<Guid, LobbyEntry> _lobbies = new();

    public void Add(Guid lobbyId, LobbyEntry entry)
    {
        if (!_lobbies.TryAdd(lobbyId, entry))
            throw new InvalidOperationException($"A lobby with id '{lobbyId}' already exists.");
    }

    public LobbyEntry? TryGet(Guid lobbyId) =>
        _lobbies.TryGetValue(lobbyId, out var entry) ? entry : null;

    public void Remove(Guid lobbyId) =>
        _lobbies.TryRemove(lobbyId, out _);
}
