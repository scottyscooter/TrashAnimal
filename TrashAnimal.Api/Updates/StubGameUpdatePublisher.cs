namespace TrashAnimal.Api.Updates;

public sealed class StubGameUpdatePublisher : IGameUpdatePublisher
{
    public Task PublishAsync(GameUpdateEnvelope envelope) => Task.CompletedTask;
}
