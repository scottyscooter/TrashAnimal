namespace TrashAnimal.Api.Lobbies;

public interface ILobbyRepository
{
    void Add(Guid lobbyId, LobbyEntry entry);
    LobbyEntry? TryGet(Guid lobbyId);
    void Remove(Guid lobbyId);
}
