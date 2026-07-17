using Microsoft.AspNetCore.SignalR;
using TrashAnimal.Api.Hubs;

namespace TrashAnimal.Api.Updates;

/// <summary>
/// Publishes lobby update notifications to all clients subscribed to a lobby's SignalR group.
/// Unlike <see cref="SignalRGameUpdatePublisher"/>, the full <see
/// cref="Contracts.Responses.LobbyView"/> is pushed directly — no client re-fetch is required.
/// </summary>
public sealed class SignalRLobbyUpdatePublisher : ILobbyUpdatePublisher
{
    private readonly IHubContext<LobbyHub> _hubContext;

    public SignalRLobbyUpdatePublisher(IHubContext<LobbyHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(LobbyUpdateEnvelope envelope)
    {
        var groupName = LobbyHub.GroupNameFor(envelope.LobbyId);
        return _hubContext.Clients.Group(groupName).SendAsync(LobbyHub.LobbyUpdatedEvent, envelope);
    }
}
