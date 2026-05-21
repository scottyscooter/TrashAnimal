using Microsoft.AspNetCore.SignalR;

namespace TrashAnimal.Api.Hubs;

/// <summary>
/// SignalR hub that clients connect to on game load to receive push notifications.
/// Clients join a per-game group and receive a <see cref="GameUpdatedEvent"/> notification
/// after each successful command. The hub is push-only: game commands must be submitted
/// through the REST API, never through this hub.
/// </summary>
/// <remarks>
/// <b>Client connection protocol:</b>
/// <list type="number">
///   <item>Connect to <c>/hubs/game</c> using the SignalR client library.</item>
///   <item>Register a handler for the <see cref="GameUpdatedEvent"/> event name.</item>
///   <item>Call <see cref="JoinGameAsync"/> with the <c>gameId</c> to start receiving
///         notifications for that game.</item>
///   <item>On receiving <c>GameUpdated</c>, call <c>GET /games/{gameId}/view</c> to
///         refresh local state. The hub payload is a trigger only — full state lives in REST.</item>
///   <item>On reconnect, compare the cached <c>revision</c> against the latest view's
///         <c>revision</c> and refresh if behind.</item>
///   <item>Call <see cref="LeaveGameAsync"/> when the player navigates away or the game ends.</item>
/// </list>
///
/// <b>GameUpdated event payload</b> (JSON):
/// <code>
/// {
///   "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "revision": 12,
///   "actingPlayerSeat": 1,
///   "currentGameState": "TokenPhase"
/// }
/// </code>
/// </remarks>
public sealed class GameHub : Hub
{
    /// <summary>
    /// The name of the server-to-client event pushed after each successful command.
    /// Clients must register a handler for this exact string.
    /// </summary>
    public const string GameUpdatedEvent = "GameUpdated";

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
