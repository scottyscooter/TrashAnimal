namespace TrashAnimal.Api.Sessions;

public interface IGameSessionRepository
{
    void Add(Guid gameId, GameSessionEntry entry);
    GameSessionEntry? TryGet(Guid gameId);
    void Remove(Guid gameId);
}
