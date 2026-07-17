namespace TrashAnimal.Api.Updates;

public interface ILobbyUpdatePublisher
{
    Task PublishAsync(LobbyUpdateEnvelope envelope);
}
