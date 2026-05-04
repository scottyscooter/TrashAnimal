using System.Collections.Concurrent;

namespace TrashAnimal.Api.Sessions;

public sealed class InMemoryGameSessionRepository : IGameSessionRepository
{
    private readonly ConcurrentDictionary<Guid, GameSessionEntry> _sessions = new();

    public void Add(Guid gameId, GameSessionEntry entry)
    {
        if (!_sessions.TryAdd(gameId, entry))
            throw new InvalidOperationException($"A game session with id '{gameId}' already exists.");
    }

    public GameSessionEntry? TryGet(Guid gameId) =>
        _sessions.TryGetValue(gameId, out var entry) ? entry : null;

    public void Remove(Guid gameId) =>
        _sessions.TryRemove(gameId, out _);
}
