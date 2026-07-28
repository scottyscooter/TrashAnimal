using TrashAnimal.Api.Sessions;

namespace TrashAnimal.Api.Tests.Helpers;

/// <summary>
/// A real, fully-functional <see cref="IGameSessionRepository"/> that delegates to an in-memory
/// store and adds a <see cref="RegisterSession"/> helper so integration tests can pre-populate
/// specific sessions (built with mocked <see cref="Die"/>/<see cref="IDrawPile"/> leaf
/// dependencies via <see cref="DieMockFactory"/>/<see cref="DrawPileMockFactory"/>, or pre-built
/// game state) before making HTTP calls. This class itself is not a mock — the repository
/// contract is exercised for real; only the session's own collaborators are mocked.
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
