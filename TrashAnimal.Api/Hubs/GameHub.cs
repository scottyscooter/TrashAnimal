using Microsoft.AspNetCore.SignalR;

namespace TrashAnimal.Api.Hubs;

/// <summary>
/// SignalR hub that clients connect to on game load.
/// Clients join a per-game group to receive push notifications when the game state changes.
/// The hub is read-only: game commands must be submitted through the REST API.
/// </summary>
public sealed class GameHub : Hub
{
    /// <summary>
    /// Called by a client after connect to subscribe to updates for a specific game.
    /// Adds the caller's connection to the SignalR group for that game.
    /// </summary>
    public async Task JoinGameAsync(Guid gameId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(gameId));
    }

    /// <summary>
    /// Called by a client to stop receiving updates for a specific game.
    /// </summary>
    public async Task LeaveGameAsync(Guid gameId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNameFor(gameId));
    }

    /// <summary>
    /// Returns the SignalR group name for a given game.
    /// Used by <see cref="TrashAnimal.Api.Updates.SignalRGameUpdatePublisher"/> to target the right group.
    /// </summary>
    internal static string GroupNameFor(Guid gameId) => $"game:{gameId}";
}
