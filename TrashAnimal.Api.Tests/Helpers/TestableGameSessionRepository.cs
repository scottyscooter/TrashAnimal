using TrashAnimal.Api.Sessions;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// An <see cref="IGameSessionRepository"/> that delegates to an in-memory store and adds a
/// <see cref="RegisterSession"/> helper so integration tests can pre-populate specific sessions
/// (with controlled draw piles, sequenced dice, or pre-built game state) before making HTTP calls.
/// </summary>
public sealed class TestableGameSessionRepository : IGameSessionRepository
{
    private readonly InMemoryGameSessionRepository _inner = new();

    public void RegisterSession(Guid gameId, GameSession session, Die die) =>
        _inner.Add(gameId, new GameSessionEntry(session, die));

    public void Add(Guid gameId, GameSessionEntry entry) => _inner.Add(gameId, entry);

    public GameSessionEntry? TryGet(Guid gameId) => _inner.TryGet(gameId);

    public void Remove(Guid gameId) => _inner.Remove(gameId);
}
