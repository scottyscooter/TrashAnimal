using Microsoft.AspNetCore.SignalR;
using TrashAnimal.Api.Hubs;

namespace TrashAnimal.Api.Updates;

/// <summary>
/// Publishes game update notifications to all clients subscribed to a game's SignalR group.
/// Clients receive a lightweight <see cref="GameUpdateEnvelope"/> and respond by refreshing
/// their view via GET /games/{gameId}/view.
/// </summary>
public sealed class SignalRGameUpdatePublisher : IGameUpdatePublisher
{
    private readonly IHubContext<GameHub> _hubContext;

    public SignalRGameUpdatePublisher(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(GameUpdateEnvelope envelope)
    {
        var groupName = GameHub.GroupNameFor(envelope.GameId);
        return _hubContext.Clients.Group(groupName).SendAsync("GameUpdated", envelope);
    }
}
