namespace TrashAnimal.Api.Updates;

public sealed class StubLobbyUpdatePublisher : ILobbyUpdatePublisher
{
    public Task PublishAsync(LobbyUpdateEnvelope envelope) => Task.CompletedTask;
}
