namespace TrashAnimal.Api.Updates;

public interface IGameUpdatePublisher
{
    Task PublishAsync(GameUpdateEnvelope envelope);
}
